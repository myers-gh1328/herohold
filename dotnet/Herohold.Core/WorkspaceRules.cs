namespace Herohold.Core;

/// <summary>Detached, validated player actions. No navigation or calculation spends resources.</summary>
public static class WorkspaceRules
{
    public static CharacterState SetExhaustion(CharacterState c, int level)
    {
        if (level is < 0 or > 6) throw new ArgumentOutOfRangeException(nameof(level), "Exhaustion must stay between 0 and 6. Resolve its consequences at the table.");
        var result=DomainCopy.Clone(c) with { Exhaustion = level };
        if(level==6){result=AdjustHp(result,-result.CurrentHp);result=result with{Sheet=result.Sheet with{DeathSaves=result.Sheet.DeathSaves with{Dead=true,Stable=false}}};}
        return result;
    }
    public static void Validate(CharacterState c) => WorkspaceValidation.Character(c);
    public static CharacterState SetDefenses(CharacterState c, List<DamageDefense> defenses)
    {
        var updated = DomainCopy.Clone(c) with { Defenses = defenses.Select(x => x with { }).ToList() };
        WorkspaceValidation.Character(updated); return updated;
    }
    public static CharacterState SetKnownTarget(CharacterState c, KnownTargetState target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var updated = DomainCopy.Clone(c) with { KnownTarget = target with { Defenses = target.Defenses.Select(x => x with { }).ToList() } };
        WorkspaceValidation.Character(updated); return updated;
    }
    public static CharacterState AddEquipment(CharacterState c, EquipmentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var updated = DomainCopy.Clone(c);
        updated.Equipment.Add(SheetRules.CloneItem(item));
        updated = SheetRules.ApplyEffectiveIncapacitation(updated);
        WorkspaceValidation.Character(updated); return updated;
    }
    public static CharacterState UpdateEquipment(CharacterState c, EquipmentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var updated = DomainCopy.Clone(c);
        var index = updated.Equipment.FindIndex(x => x.Id == item.Id);
        if (index < 0) throw new ArgumentException("That inventory item no longer exists.");
        updated.Equipment[index] = SheetRules.CloneItem(item);
        updated = SheetRules.ApplyEffectiveIncapacitation(updated);
        WorkspaceValidation.Character(updated); return updated;
    }
    public static CharacterState RemoveEquipment(CharacterState c, string id)
    {
        var updated = DomainCopy.Clone(c);
        if (updated.Equipment.RemoveAll(x => x.Id == id) == 0) throw new ArgumentException("That inventory item no longer exists.");
        return SheetRules.ApplyEffectiveIncapacitation(updated);
    }

    public static CharacterState AdjustHp(CharacterState c, int delta)
    {
        var updated = DomainCopy.Clone(c);
        var hp = (long)c.CurrentHp + delta;
        if (hp < 0 || hp > c.MaximumHp) throw new ArgumentOutOfRangeException(nameof(delta), "Current HP must stay between zero and maximum HP.");
        if(hp>0&&c.Sheet.DeathSaves.Dead)throw new ArgumentException("A dead character must be revived or explicitly corrected before ordinary healing.");
        return updated with { CurrentHp = (int)hp,
            Sheet = hp>0 ? updated.Sheet with{DeathSaves=new()} : updated.Sheet,
            Effects = hp == 0 ? updated.Effects.Where(effect => !effect.Concentration).ToList() : updated.Effects,
            Play = hp == 0 ? updated.Play with { HunterTarget = string.Empty } : updated.Play };
    }
    public static CharacterState AdjustResource(CharacterState c, string id, int delta)
    {
        if(id=="hitdice"&&c.Play.HitDiePools.Count>0)
        {
            if(c.Play.HitDiePools.Count!=1)throw new ArgumentException("Choose which Hit Die pool to adjust; different die sizes are tracked separately.");
            return HitDiePoolRules.Adjust(c,c.Play.HitDiePools[0].Id,delta);
        }
        var updated = DomainCopy.Clone(c);
        var index = updated.Resources.FindIndex(x => x.Id == id);
        if (index < 0) throw new ArgumentException("That resource no longer exists.", nameof(id));
        var resource = updated.Resources[index];
        var remaining = (long)resource.Remaining + delta;
        if (remaining < 0 || remaining > resource.Maximum) throw new ArgumentOutOfRangeException(nameof(delta), "Resource uses must stay between zero and the pool's maximum.");
        updated.Resources[index] = resource with { Remaining = (int)remaining };
        return updated;
    }

    public static CharacterState AdjustQuantity(CharacterState c, string itemId, int delta)
    {
        var updated = DomainCopy.Clone(c);
        var index = updated.Equipment.FindIndex(x => x.Id == itemId);
        if (index < 0) throw new ArgumentException("That inventory item no longer exists.", nameof(itemId));
        var item = updated.Equipment[index];
        var quantity = (long)item.Quantity + delta;
        if (quantity is < 0 or > 100000) throw new ArgumentOutOfRangeException(nameof(delta), "Item quantity must stay between 0 and 100,000.");
        updated.Equipment[index] = item with { Quantity = (int)quantity, Equipped = quantity > 0 && item.Equipped, Attuned = quantity > 0 && item.Attuned };
        return SheetRules.ApplyEffectiveIncapacitation(updated);
    }

    public static CharacterState SetEquipment(CharacterState c, string itemId, bool equipped, bool attuned)
    {
        var updated = DomainCopy.Clone(c);
        var index = updated.Equipment.FindIndex(x => x.Id == itemId);
        if (index < 0) throw new ArgumentException("That inventory item no longer exists.", nameof(itemId));
        var item = updated.Equipment[index];
        if ((equipped || attuned) && item.Quantity == 0) throw new ArgumentException("An item with zero quantity cannot be equipped or attuned.");
        if (attuned && !item.RequiresAttunement) throw new ArgumentException("That item does not require attunement.");
        if (attuned && updated.Equipment.Count(x => x.Id != itemId && x.Attuned) >= 3)
            throw new ArgumentException("The character already has three attuned items.");
        updated.Equipment[index] = item with { Equipped = equipped, Attuned = attuned };
        return SheetRules.ApplyEffectiveIncapacitation(updated);
    }

    public static CharacterState AddNote(CharacterState c, string title, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (title.Length > 200 || text.Length > 12000) throw new ArgumentException("Notes allow a 200-character title and 12,000-character text.");
        var updated = DomainCopy.Clone(c);
        if (updated.Notes.Count >= 1000) throw new ArgumentException("The workspace already has 1,000 notes.");
        updated.Notes.Add(new() { Id = Guid.NewGuid().ToString("N"), Title = title.Trim(), Text = text.Trim(), CreatedAt = DateTimeOffset.UtcNow });
        return updated;
    }
    public static CharacterState AddEffect(CharacterState c, ActiveEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentException.ThrowIfNullOrWhiteSpace(effect.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(effect.Name);
        if (effect.Name.Length > 200 || effect.Source is null || effect.Source.Length > 500 || effect.Note is null || effect.Note.Length > 2000)
            throw new ArgumentException("Effect text is too long or missing.");
        if (effect.RemainingRounds is <= 0 or > 10000) throw new ArgumentOutOfRangeException(nameof(effect), "Counted effects need 1–10,000 remaining rounds; use manual duration otherwise.");
        var updated = DomainCopy.Clone(c);
        if (updated.Effects.Any(x => x.Id == effect.Id)) throw new ArgumentException("That effect ID already exists.");
        if (effect.Concentration && updated.CurrentHp == 0) throw new ArgumentException("Concentration cannot be started at zero HP; resolve recovery from unconsciousness first.");
        if (effect.Concentration && (IsIncapacitatingCondition(effect.Name) || SheetRules.EffectiveEffects(updated).Any(x => IsIncapacitatingCondition(x.Name))))
            throw new ArgumentException("Concentration cannot be maintained while incapacitated.");
        if (effect.Concentration || IsIncapacitatingCondition(effect.Name)) updated.Effects.RemoveAll(x => x.Concentration);
        if (updated.Effects.Count >= 100) throw new ArgumentException("The workspace already has 100 effects.");
        updated.Effects.Add(effect with { Name = effect.Name.Trim() });
        if (!updated.Effects.Any(x => x.Concentration && x.Source == "spell:hunters-mark"))
            updated = updated with { Play = updated.Play with { HunterTarget = string.Empty } };
        return updated;
    }
    public static CharacterState RemoveEffect(CharacterState c, string effectId)
    {
        var updated = DomainCopy.Clone(c);
        if (updated.Effects.RemoveAll(x => x.Id == effectId) == 0) throw new ArgumentException("That effect no longer exists.", nameof(effectId));
        if (!updated.Effects.Any(x => x.Concentration && x.Source == "spell:hunters-mark"))
            updated = updated with { Play = updated.Play with { HunterTarget = string.Empty } };
        return updated;
    }
    public static CharacterState AdvanceTurn(CharacterState c)
    {
        if(c.Combat.EncounterActive)throw new ArgumentException("Use the combat turn controls while an encounter is active.");
        var updated = DomainCopy.Clone(c);
        if (c.Turn == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(c), "The turn counter cannot advance further.");
        var effects = updated.Effects.Where(x => x.RemainingRounds is null or > 1)
            .Select(x => x.RemainingRounds is null ? x : x with { RemainingRounds = x.RemainingRounds - 1 }).ToList();
        return updated with
        {
            Turn = c.Turn + 1, Effects = effects,
            Play = updated.Play with { DreadUsedThisTurn = false,
                HunterTarget = effects.Any(x => x.Concentration && x.Source == "spell:hunters-mark") ? updated.Play.HunterTarget : string.Empty }
        };
    }

    internal static bool IsIncapacitatingCondition(string? name) => new[] { "Incapacitated", "Paralyzed", "Stunned", "Petrified", "Unconscious" }
        .Contains(name?.Trim(), StringComparer.OrdinalIgnoreCase);
}
