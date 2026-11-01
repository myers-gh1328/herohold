using Herohold.Core;
using Microsoft.EntityFrameworkCore;

namespace Herohold.Storage;

/// <summary>
/// The shared item catalog. Official items are copied from the rules data on start and never edited; homebrew items belong to
/// one user and are shared by all of that user's characters. A character's item rows keep only which item and its own state.
/// </summary>
public sealed partial class SqliteCharacterRecordStore
{
    private const string Legacy = "Legacy";

    public async Task<IReadOnlyList<CatalogItem>> HomebrewItemsAsync(string ownerId, CancellationToken ct = default)
    {
        await using var db = new HeroholdDbContext(options);
        var ids = await db.CatalogItems.AsNoTracking().Where(i => i.OwnerId == ownerId && i.Source == CatalogItem.Homebrew).Select(i => i.Id).ToListAsync(ct);
        return [.. (await CatalogItemsAsync(db, ids, ct)).OrderBy(i => i.Definition.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Copies the official items in. Rows are only added or updated: characters may still own an item the rules data dropped.</summary>
    private void SyncOfficialItems(HeroholdDbContext db)
    {
        if (items.Official.Count == 0) return;
        var stored = db.CatalogItems.Where(i => i.OwnerId == null && i.Source != Legacy).ToDictionary(i => i.Id, StringComparer.Ordinal);
        foreach (var item in items.Official)
        {
            if (item.OwnerId is not null || item.IsHomebrew || item.Source == Legacy)
                throw new InvalidOperationException("Official items have a rules source and no owner.");
            var d = item.Definition;
            if (d.ModifierBonuses.Count > 0 || d.Defenses.Count > 0 || d.PassiveEffects.Count > 0 || d.UseEffects.Count > 0)
                throw new InvalidOperationException("Official items carry no bonuses, defenses or effects of their own.");
            if (!stored.TryGetValue(item.Id, out var row)) db.CatalogItems.Add(row = new CatalogItemRow { Id = item.Id });
            Fill(row, item);
        }
        db.SaveChanges();
    }

    /// <summary>
    /// Items stored before the catalog: each becomes the official item its name matches, or a homebrew item when nothing
    /// matches or it has mechanics of its own. Needs the official items; until they are there the items keep working as they are.
    /// </summary>
    private void LinkLegacyItems(HeroholdDbContext db)
    {
        if (items.Official.Count == 0) return;
        var legacyIds = db.CatalogItems.Where(i => i.Source == Legacy).Select(i => i.Id).ToList();
        if (legacyIds.Count == 0) return;
        using var transaction = db.Database.BeginTransaction();
        foreach (var legacy in CatalogItemsAsync(db, legacyIds, CancellationToken.None).GetAwaiter().GetResult())
        {
            var match = items.Match(legacy.Definition.Name);
            if (match is not null && ItemLinking.FitsOfficial(legacy.Definition, match.Item))
            {
                foreach (var owned in db.Items.Where(i => i.CatalogItemId == legacy.Id))
                {
                    owned.CatalogItemId = match.Item.Id;
                    owned.Quantity *= match.UnitQuantity;
                    if (match.ScrollSpell.Length > 0) owned.ScrollSpell = match.ScrollSpell;
                    owned.Attuned &= match.Item.Definition.RequiresAttunement;
                }
                db.SaveChanges();
                db.CatalogItems.Where(i => i.Id == legacy.Id).ExecuteDelete();
            }
            else
            {
                var homebrew = CatalogItem.NewHomebrewId();
                var row = db.CatalogItems.Single(i => i.Id == legacy.Id);
                db.CatalogItems.Add(new CatalogItemRow { Id = homebrew });
                db.SaveChanges();
                Fill(db.CatalogItems.Single(i => i.Id == homebrew), legacy with { Id = homebrew, Source = CatalogItem.Homebrew, Kind = GuessKind(legacy.Definition), OwnerId = row.OwnerId });
                AddChildren(db, homebrew, legacy.Definition);
                foreach (var owned in db.Items.Where(i => i.CatalogItemId == legacy.Id)) owned.CatalogItemId = homebrew;
                db.SaveChanges();
                db.CatalogItems.Where(i => i.Id == legacy.Id).ExecuteDelete();
            }
        }
        transaction.Commit();
    }

    /// <summary>
    /// The record with every item linked: official items take the catalog's definition (they cannot be edited); homebrew items
    /// save their definition for every character that owns them; an unlinked item becomes the official item its name
    /// matches, or a new homebrew item.
    /// </summary>
    private async Task<CharacterRecord> LinkItemsAsync(HeroholdDbContext db, CharacterRecord record, CancellationToken ct)
    {
        var ids = record.Facts.Equipment.Select(i => i.CatalogItemId).Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        var known = await db.CatalogItems.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, StringComparer.Ordinal, ct);
        var official = await CatalogItemsAsync(db, known.Values.Where(r => r.OwnerId is null).Select(r => r.Id), ct);
        var officialById = official.ToDictionary(i => i.Id, StringComparer.Ordinal);
        var saved = new HashSet<string>(StringComparer.Ordinal);
        var linked = new List<EquipmentItem>();
        foreach (var item in record.Facts.Equipment)
        {
            if (officialById.TryGetValue(item.CatalogItemId, out var known1)) { linked.Add(known1.Owned(item)); continue; }
            var homebrewId = item.CatalogItemId;
            // A legacy item still unlinked (no rules data yet) is the owner's own and becomes homebrew when saved.
            var mine = known.TryGetValue(homebrewId, out var row) && row.Source is CatalogItem.Homebrew or Legacy && row.OwnerId == record.OwnerId;
            if (!mine)
            {
                if (items.Match(item.Name) is { } match && ItemLinking.FitsOfficial(item, match.Item))
                {
                    await EnsureOfficialAsync(db, match.Item, ct);
                    linked.Add(match.Item.Owned(item with { Quantity = item.Quantity * match.UnitQuantity, ScrollSpell = match.ScrollSpell.Length > 0 ? match.ScrollSpell : item.ScrollSpell }));
                    continue;
                }
                homebrewId = homebrewId.StartsWith("homebrew:", StringComparison.Ordinal) && !known.ContainsKey(homebrewId) ? homebrewId : CatalogItem.NewHomebrewId();
                db.CatalogItems.Add(row = new CatalogItemRow { Id = homebrewId, Source = CatalogItem.Homebrew, OwnerId = record.OwnerId, Kind = GuessKind(item) });
                known[homebrewId] = row;
            }
            var owned = item with { CatalogItemId = homebrewId };
            if (saved.Add(homebrewId))
            {
                Fill(row!, new CatalogItem { Id = homebrewId, Source = CatalogItem.Homebrew, Kind = row!.Kind.Length > 0 ? row.Kind : GuessKind(item), OwnerId = record.OwnerId, Definition = CatalogItem.DefinitionOf(owned) });
                await db.ItemBonuses.Where(b => b.ItemId == homebrewId).ExecuteDeleteAsync(ct);
                await db.ItemDefenses.Where(b => b.ItemId == homebrewId).ExecuteDeleteAsync(ct);
                await db.ItemEffects.Where(b => b.ItemId == homebrewId).ExecuteDeleteAsync(ct);
                AddChildren(db, homebrewId, owned);
            }
            linked.Add(owned);
        }
        await db.SaveChangesAsync(ct);
        return record with { Facts = record.Facts with { Equipment = linked } };
    }

    /// <summary>An official item a name matched, stored if this database has not seen it yet (a store opened without the rules data).</summary>
    private static async Task EnsureOfficialAsync(HeroholdDbContext db, CatalogItem item, CancellationToken ct)
    {
        if (db.CatalogItems.Local.Any(r => r.Id == item.Id) || await db.CatalogItems.AnyAsync(r => r.Id == item.Id, ct)) return;
        var row = new CatalogItemRow { Id = item.Id };
        Fill(row, item);
        db.CatalogItems.Add(row);
    }

    private static async Task<List<CatalogItem>> CatalogItemsAsync(HeroholdDbContext db, IEnumerable<string> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct(StringComparer.Ordinal).ToList();
        var rows = await db.CatalogItems.AsNoTracking().Where(i => wanted.Contains(i.Id)).ToListAsync(ct);
        var bonuses = (await db.ItemBonuses.AsNoTracking().Where(b => wanted.Contains(b.ItemId)).ToListAsync(ct)).ToLookup(b => b.ItemId);
        var defenses = (await db.ItemDefenses.AsNoTracking().Where(b => wanted.Contains(b.ItemId)).ToListAsync(ct)).ToLookup(b => b.ItemId);
        var effects = (await db.ItemEffects.AsNoTracking().Where(b => wanted.Contains(b.ItemId)).ToListAsync(ct)).ToLookup(b => b.ItemId);
        List<ActiveEffect> Effects(string id, string owner) => [.. effects[id].Where(e => e.Owner == owner).OrderBy(e => e.Position)
            .Select(e => new ActiveEffect { Id = e.EffectId, Name = e.Name, Source = e.Source, Note = e.Note, RemainingRounds = e.RemainingRounds, Concentration = e.Concentration })];
        return [.. rows.Select(r => new CatalogItem
        {
            Id = r.Id, Source = r.Source, Kind = r.Kind, OwnerId = r.OwnerId,
            Definition = new EquipmentItem
            {
                Name = r.Name, Description = r.Description, RequiresAttunement = r.RequiresAttunement, UnitWeightLb = r.UnitWeightLb,
                Charges = r.HasCharges ? new ItemCharges { UseCost = r.ChargeUseCost, Maximum = r.ChargeMaximum, RecoveryNote = r.ChargeRecoveryNote } : null,
                UseAction = Enum.Parse<ItemUseAction>(r.UseAction), Consumable = r.Consumable, BaseWeapon = r.BaseWeapon, AttackBonus = r.AttackBonus, DamageBonus = r.DamageBonus,
                HealingDice = r.HealingDiceCount is { } count && r.HealingDiceSides is { } sides ? new(count, sides) : null, HealingBonus = r.HealingBonus,
                ThrownRangeFt = r.ThrownRangeFt, ThrownDamage = r.ThrownDamageCount is { } thrownCount && r.ThrownDamageSides is { } thrownSides ? new(thrownCount, thrownSides) : null, ThrownDamageType = r.ThrownDamageType,
                ModifierBonuses = bonuses[r.Id].OrderBy(b => b.Position).ToDictionary(b => Enum.Parse<Ability>(b.Ability), b => b.Value),
                Defenses = [.. defenses[r.Id].OrderBy(d => d.Position).Select(d => new DamageDefense { Kind = d.Kind, Type = d.Type, Source = d.Source, Note = d.Note })],
                PassiveEffects = Effects(r.Id, "passive"), UseEffects = Effects(r.Id, "use")
            }
        })];
    }

    private static void Fill(CatalogItemRow row, CatalogItem item)
    {
        var d = item.Definition;
        row.Source = item.Source; row.Kind = item.Kind; row.OwnerId = item.OwnerId; row.Name = d.Name; row.Description = d.Description;
        row.RequiresAttunement = d.RequiresAttunement; row.UnitWeightLb = d.UnitWeightLb; row.HasCharges = d.Charges is not null;
        row.ChargeUseCost = d.Charges?.UseCost ?? 0; row.ChargeMaximum = d.Charges?.Maximum ?? 0; row.ChargeRecoveryNote = d.Charges?.RecoveryNote ?? "";
        row.UseAction = d.UseAction.ToString(); row.Consumable = d.Consumable; row.BaseWeapon = d.BaseWeapon; row.AttackBonus = d.AttackBonus; row.DamageBonus = d.DamageBonus;
        row.HealingDiceCount = d.HealingDice?.Count; row.HealingDiceSides = d.HealingDice?.Sides; row.HealingBonus = d.HealingBonus;
        row.ThrownRangeFt = d.ThrownRangeFt; row.ThrownDamageCount = d.ThrownDamage?.Count; row.ThrownDamageSides = d.ThrownDamage?.Sides; row.ThrownDamageType = d.ThrownDamageType;
    }

    private static void AddChildren(HeroholdDbContext db, string id, EquipmentItem d)
    {
        db.ItemBonuses.AddRange(d.ModifierBonuses.Select((b, i) => new ItemBonusRow { ItemId = id, Position = i, Ability = b.Key.ToString(), Value = b.Value }));
        db.ItemDefenses.AddRange(d.Defenses.Select((x, i) => new ItemDefenseRow { ItemId = id, Position = i, Kind = x.Kind, Type = x.Type, Source = x.Source, Note = x.Note }));
        IEnumerable<ItemEffectRow> Effects(string owner, List<ActiveEffect> effects) => effects.Select((e, i) => new ItemEffectRow
            { ItemId = id, Owner = owner, Position = i, EffectId = e.Id, Name = e.Name, Source = e.Source, Note = e.Note, RemainingRounds = e.RemainingRounds, Concentration = e.Concentration });
        db.ItemEffects.AddRange(Effects("passive", d.PassiveEffects).Concat(Effects("use", d.UseEffects)));
    }

    private static string GuessKind(EquipmentItem item) => item.BaseWeapon.Length > 0 ? "weapon" : item.Consumable ? "consumable" : "gear";
}
