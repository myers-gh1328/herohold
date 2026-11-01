namespace Herohold.Core;

/// <summary>
/// Items thrown in place of an attack (Acid, Alchemist's Fire, Holy Water, Net, Oil). The target makes a Dexterity save against
/// 8 + your Dexterity modifier + your Proficiency Bonus; what happens to it is resolved at the table.
/// </summary>
public static class ThrownItemRules
{
    public static Calculation SaveDc(CharacterState c)
    {
        List<CalculationTerm> terms=[new("Base",8),..CharacterCalculations.Ability(c,Ability.Dexterity).Terms,new($"Proficiency Bonus · level {c.Level}",CharacterCalculations.ProficiencyBonus(c.Level))];
        return new(terms.Sum(t=>t.Value),terms.AsReadOnly());
    }

    /// <summary>Why the item cannot be thrown now, or null.</summary>
    public static string? Reason(CharacterState c,EquipmentItem item)
    {
        try{Prepare(c,item);return null;}catch(ArgumentException e){return e.Message;}
    }

    /// <summary>Throws one: spends it and, in combat, one attack of your Attack action.</summary>
    public static CharacterState Throw(CharacterState c,string itemId)
    {
        var item=c.Equipment.FirstOrDefault(i=>i.Id==itemId)??throw new ArgumentException("This item no longer exists.");
        var result=WorkspaceRules.AdjustQuantity(Prepare(c,item),item.Id,-1);
        WorkspaceValidation.Character(result);return SheetRules.CloseInspirationWindows(result);
    }

    private static CharacterState Prepare(CharacterState c,EquipmentItem item)
    {
        if(item.ThrownRangeFt is null)throw new ArgumentException("This item is not thrown in place of an attack.");
        if(!item.Carried||item.Quantity<1)throw new ArgumentException("None left in your pack.");
        if(!c.Combat.EncounterActive)return DomainCopy.Clone(c);
        if(!c.Combat.Enabled)throw new ArgumentException("Resume combat mode or end the encounter before throwing items.");
        if(CombatRules.Incapacitated(c))throw new ArgumentException("Incapacitated: you cannot throw this item.");
        if(c.Play.LastRoll is {Resolved:false})throw new ArgumentException("Resolve your previous roll before throwing.");
        return CombatRules.SpendAttack(DomainCopy.Clone(c));
    }
}
