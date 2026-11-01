using Herohold.Core;
using Herohold.Storage;
using Herohold.TestSupport;
using Herohold.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;

/// <summary>Items are one shared catalog: official items from the rules data, homebrew items per owner; characters keep only their own state.</summary>
[TestCategory("RulesData")]
[TestClass]
public sealed class ItemCatalogTests
{
    private string folder = "";
    private string Database => Path.Combine(folder, "herohold.db");
    [TestInitialize] public void Setup() { folder = Path.Combine(Path.GetTempPath(), "herohold-item-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); }
    [TestCleanup] public void Cleanup() { SqliteConnection.ClearAllPools(); if (Directory.Exists(folder)) Directory.Delete(folder, true); }

    private static CatalogItem Official(string id) => TestStores.Items.Official.Single(i => i.Id == id);

    [TestMethod]
    public void TheOfficialCatalogIsThe2024ItemsWithPlainTextWeightsAndKinds()
    {
        var items = TestStores.Items.Official;
        Assert.IsTrue(items.All(i => i.Source is "XPHB" or "XDMG"));
        Assert.IsGreaterThan(700, items.Count);
        Assert.IsFalse(items.Any(i => i.Definition.Description.Contains("{@", StringComparison.Ordinal)), "Rules markup is turned into the text it shows.");
        var longbow = Official("XPHB|Longbow");
        Assert.AreEqual("weapon", longbow.Kind); Assert.AreEqual(2m, longbow.Definition.UnitWeightLb);
        StringAssert.Contains(longbow.Definition.Description, "1d8 Piercing"); StringAssert.Contains(longbow.Definition.Description, "Ammunition");
        var potion = Official("XDMG|Potion of Healing");
        Assert.AreEqual("potion", potion.Kind); Assert.IsTrue(potion.Definition.Consumable); Assert.AreEqual(ItemUseAction.BonusAction, potion.Definition.UseAction);
        StringAssert.Contains(potion.Definition.Description, "2d4 + 2");
        Assert.AreEqual("ammunition", Official("XPHB|Arrow").Kind);
    }

    [TestMethod]
    [DataRow("Rope", "XPHB|Rope", "", 1)]
    [DataRow("arrows", "XPHB|Arrow", "", 1)]
    [DataRow("Arrows (20)", "XPHB|Arrow", "", 20)]
    [DataRow("Scroll of Speak with Dead", "XDMG|Spell Scroll (Level 3)", "Speak with Dead", 1)]
    [DataRow("Scroll of fire bolt", "XDMG|Spell Scroll (Cantrip)", "Fire Bolt", 1)]
    [DataRow("Druidic Focus (sprig of mistletoe)", "XPHB|Sprig of Mistletoe", "", 1)]
    public void ItemNamesFindTheOfficialItemTheyStandFor(string name, string id, string spell, int unit)
    {
        var match = TestStores.Items.Match(name)!;
        Assert.AreEqual(id, match.Item.Id); Assert.AreEqual(spell, match.ScrollSpell); Assert.AreEqual(unit, match.UnitQuantity);
    }

    [TestMethod]
    public void NamesThatAreNotOfficialItemsMatchNothing()
    {
        Assert.IsNull(TestStores.Items.Match("Briarbloom Bow"));
        Assert.IsNull(TestStores.Items.Match("Scroll of Not A Spell"));
        Assert.IsNull(TestStores.Items.Match(""));
    }

    /// <summary>A database from before the catalog, with the kinds of items players had.</summary>
    private void OldDatabase()
    {
        var options = new DbContextOptionsBuilder<HeroholdDbContext>().UseSqlite($"Data Source={Database};Pooling=False").Options;
        using (var db = new HeroholdDbContext(options)) db.GetService<IMigrator>().Migrate("20261028035418_InitialSchema");
        using var connection = new SqliteConnection($"Data Source={Database};Pooling=False"); connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users (Id, DisplayName, CreatedAt) VALUES ('owner', 'Owner', '2026-10-01');
            INSERT INTO characters (Id, OwnerId, Revision, UpdatedAt, Name, ClassName, Subclass, Level, Species, Background, HasPresentation, PrimaryWeapon,
                FeaturedCantrip, HasImportProvenance, HasSpellPicks, HasManualFacts, CurrentHp, TemporaryHp, Turn, Exhaustion, DreadUsedThisTurn, HunterTarget,
                KnownTargetName, KnownTargetKnowledge, HasCoins, CoinsCp, CoinsSp, CoinsEp, CoinsGp, CoinsPp, DeathSaveSuccesses, DeathSaveFailures, Stable, Dead,
                CapacitySource, CarryCoins)
            VALUES ('old-ranger', 'owner', 3, '2026-10-01', 'Old Ranger', 'Ranger', '', 3, 'Human', 'Soldier', 0, '', '', 0, 0, 0, 20, 0, 1, 0, 0, '', '',
                'unknown', 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 'rules', 1);
            INSERT INTO character_items (CharacterId, Position, ItemId, Name, Description, Quantity, Equipped, RequiresAttunement, Attuned, Carried, UnitWeightLb,
                HasCharges, ChargeUseCost, ChargeMaximum, ChargeRemaining, ChargeRecoveryNote, UseAction, Consumable, BaseWeapon, AttackBonus, DamageBonus) VALUES
                ('old-ranger', 0, 'a', 'Arrows', 'Roll20 text', 20, 0, 0, 0, 1, 0.05, 0, 0, 0, 0, '', 'TableManaged', 1, '', 0, 0),
                ('old-ranger', 1, 'b', 'Scroll of Speak with Dead', 'Roll20 text', 1, 0, 0, 0, 1, NULL, 0, 0, 0, 0, '', 'TableManaged', 0, '', 0, 0),
                ('old-ranger', 2, 'c', 'Dragon Slayer Shortsword', 'Bane of dragons.', 1, 1, 0, 0, 1, 2, 0, 0, 0, 0, '', 'TableManaged', 0, 'Shortsword', 1, 1),
                ('old-ranger', 3, 'd', 'Rope', 'A charmed rope.', 1, 0, 0, 0, 1, 5, 0, 0, 0, 0, '', 'TableManaged', 0, '', 0, 0),
                ('old-ranger', 4, 'e', 'Rope', 'A rope that grants strength.', 1, 1, 0, 0, 1, 5, 0, 0, 0, 0, '', 'TableManaged', 0, '', 0, 0);
            INSERT INTO character_list_values (CharacterId, Slot, Owner, List, Position, Key, Value, Number) VALUES
                ('old-ranger', 0, 'item:0', 'modifier_bonuses', -1, NULL, NULL, NULL),
                ('old-ranger', 0, 'item:4', 'modifier_bonuses', 0, 'Strength', NULL, 1);
            INSERT INTO character_effects (CharacterId, Slot, Owner, Position, EffectId, Name, Source, Note, RemainingRounds, Concentration) VALUES
                ('old-ranger', 0, 'item:2:passive', 0, 'fx', 'Dragon sense', '', 'Senses dragons.', NULL, 0);
            """;
        command.ExecuteNonQuery();
    }

    [TestMethod]
    public async Task StoredItemsFromBeforeTheCatalogBecomeOfficialOrHomebrewWithoutLosingAnything()
    {
        OldDatabase();
        var store = TestStores.Records(folder);
        var items = (await store.LoadAsync("old-ranger"))!.Facts.Equipment;
        Assert.AreEqual("XPHB|Arrow", items[0].CatalogItemId); Assert.AreEqual("Arrow", items[0].Name); Assert.AreEqual(20, items[0].Quantity);
        Assert.AreEqual(Official("XPHB|Arrow").Definition.Description, items[0].Description, "A matched item reads the official text.");
        Assert.AreEqual("XDMG|Spell Scroll (Level 3)", items[1].CatalogItemId); Assert.AreEqual("Speak with Dead", items[1].ScrollSpell);
        Assert.AreEqual("XPHB|Rope", items[3].CatalogItemId);
        foreach (var (item, why) in new[] { (items[2], "a different name, bonuses and an effect"), (items[4], "an ability bonus of its own") })
        {
            StringAssert.StartsWith(item.CatalogItemId, "homebrew:", "An item with " + why + " stays the player's own.");
        }
        Assert.AreEqual("Dragon Slayer Shortsword", items[2].Name); Assert.AreEqual("Shortsword", items[2].BaseWeapon); Assert.AreEqual(1, items[2].AttackBonus);
        Assert.AreEqual("Dragon sense", items[2].PassiveEffects.Single().Name);
        Assert.AreEqual(1, items[4].ModifierBonuses[Ability.Strength]); Assert.AreEqual("A rope that grants strength.", items[4].Description);
        var homebrew = await store.HomebrewItemsAsync(CharacterRecord.DefaultOwnerId);
        CollectionAssert.AreEquivalent(new[] { "Dragon Slayer Shortsword", "Rope" }, homebrew.Select(h => h.Definition.Name).ToArray());
        await using var db = new HeroholdDbContext(new DbContextOptionsBuilder<HeroholdDbContext>().UseSqlite($"Data Source={Database};Pooling=False").Options);
        Assert.AreEqual(0, await db.CatalogItems.CountAsync(i => i.Source == "Legacy"), "No item is left half-moved.");
        Assert.AreEqual(0, await db.ListValues.CountAsync(v => v.Owner.StartsWith("item:")), "Item bonuses moved to the item.");
        Assert.AreEqual(0, await db.Effects.CountAsync(v => v.Owner.StartsWith("item:")));
    }

    private async Task<(IWorkspaceStore Store, CharacterState Character)> SavedAsync(string id, params EquipmentItem[] equipment)
    {
        var store = TestStores.Open(folder);
        var character = SampleWorkspace.Create().Character with { Id = id, Equipment = [.. equipment] };
        return (store, (await store.SaveAsync(new WorkspaceSnapshot { Character = character }, 0)).Character);
    }

    [TestMethod]
    public async Task AnOfficialItemKeepsTheRulesTextWhateverASaveSends()
    {
        var (store, saved) = await SavedAsync("rope-owner", new EquipmentItem { Id = "rope", Name = "Rope", Quantity = 2, Description = "Typed over." });
        var rope = saved.Equipment.Single();
        Assert.AreEqual("XPHB|Rope", rope.CatalogItemId); Assert.AreEqual(2, rope.Quantity);
        Assert.AreEqual(Official("XPHB|Rope").Definition.Description, rope.Description, "A typed official name becomes the official item.");
        var edited = saved with { Equipment = [rope with { Description = "Edited.", UnitWeightLb = 99, Quantity = 3 }] };
        var again = (await store.SaveAsync(new WorkspaceSnapshot { Revision = 1, Character = edited }, 1)).Character.Equipment.Single();
        Assert.AreEqual(Official("XPHB|Rope").Definition.Description, again.Description, "Official details cannot be edited.");
        Assert.AreEqual(Official("XPHB|Rope").Definition.UnitWeightLb, again.UnitWeightLb);
        Assert.AreEqual(3, again.Quantity, "The character's own state still changes.");
    }

    [TestMethod]
    public async Task AHomebrewItemIsSharedByAllTheOwnersCharacters()
    {
        var (store, first) = await SavedAsync("first", new EquipmentItem { Id = "charm", Name = "Cinder Ash Charm", Description = "Warm to the touch." });
        var charm = first.Equipment.Single(); StringAssert.StartsWith(charm.CatalogItemId, "homebrew:");
        var second = SampleWorkspace.Create().Character with { Id = "second", Equipment = [charm with { Id = "their-charm", Quantity = 1 }] };
        await store.SaveAsync(new WorkspaceSnapshot { Character = second }, 0);
        await store.SaveAsync(new WorkspaceSnapshot { Revision = 1, Character = first with { Equipment = [charm with { Description = "Glows in the dark." }] } }, 1);
        var theirs = (await store.LoadAsync("second"))!.Character.Equipment.Single();
        Assert.AreEqual(charm.CatalogItemId, theirs.CatalogItemId);
        Assert.AreEqual("Glows in the dark.", theirs.Description, "Editing a homebrew item changes it for every character that owns it.");
        Assert.AreEqual("Cinder Ash Charm", (await store.HomebrewItemsAsync()).Single(h => h.Id == charm.CatalogItemId).Definition.Name);
    }

    [TestMethod]
    public async Task AnItemWithAnOfficialNameButItsOwnBonusesStaysHomebrew()
    {
        var (_, saved) = await SavedAsync("bonus", new EquipmentItem { Id = "blade", Name = "Shortsword", AttackBonus = 1, DamageBonus = 1 });
        var blade = saved.Equipment.Single();
        StringAssert.StartsWith(blade.CatalogItemId, "homebrew:"); Assert.AreEqual(1, blade.AttackBonus);
    }

    [TestMethod]
    public async Task AnOfficialItemWithLongRulesTextCanBeOwned()
    {
        var deck = Official("XDMG|Deck of Many Things");
        Assert.IsGreaterThan(2000, deck.Definition.Description.Length);
        var (store, saved) = await SavedAsync("deck", deck.Owned(new EquipmentItem { Id = "deck" }));
        Assert.AreEqual(deck.Definition.Description, (await store.LoadAsync("deck"))!.Character.Equipment.Single().Description);
    }
}
