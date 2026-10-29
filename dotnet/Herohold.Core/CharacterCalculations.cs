namespace Herohold.Core;

/// <summary>Pure standing calculations. Callers supply the spell/weapon's actual governing ability.</summary>
public static class CharacterCalculations
{
    public static int AbilityModifier(int score)
    {
        if (score is < 1 or > 30) throw new ArgumentOutOfRangeException(nameof(score), "Ability scores must be between 1 and 30.");
        return (int)Math.Floor((score - 10) / 2.0);
    }

    public static int ProficiencyBonus(int level)
    {
        if (level is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(level), "Character level must be between 1 and 20.");
        return 2 + (level - 1) / 4;
    }

    public static Calculation Ability(CharacterState c, Ability ability)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (!Enum.IsDefined(ability) || !c.AbilityScores.TryGetValue(ability, out var score))
            throw new ArgumentException("The character must have a score for that ability.", nameof(ability));
        var terms = new List<CalculationTerm> { new($"{ability} score {score}", AbilityModifier(score)) };
        if (c.Equipment.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != c.Equipment.Count)
            throw new ArgumentException("Inventory item IDs must be unique.", nameof(c));
        foreach (var item in c.Equipment.Where(x => x.Quantity > 0 && x.Equipped && (!x.RequiresAttunement || x.Attuned)))
            if (item.ModifierBonuses.TryGetValue(ability, out var value) && value != 0)
                terms.Add(new($"{item.Name} · modifier bonus", value));
        AppendAdjustment(c, "ability:" + ability, terms);
        return Sum(terms);
    }

    public static Calculation AbilityCheck(CharacterState c,Ability ability)
    {var terms=Ability(c,ability).Terms.ToList();AppendExhaustion(c,terms);return Sum(terms);}
    public static Calculation SpellAttack(CharacterState c,Ability ability)
    {var terms=Ability(c,ability).Terms.ToList();terms.Add(new("Spell attack proficiency",ProficiencyBonus(c.Level)));AppendExhaustion(c,terms);return Sum(terms);}
    public static int Speed(CharacterState c)=>Math.Max(0,c.Speed-5*c.Exhaustion);
    private static void AppendExhaustion(CharacterState c,List<CalculationTerm> terms)
    {if(c.Exhaustion>0)terms.Add(new("Exhaustion · level "+c.Exhaustion,-2*c.Exhaustion));}

    public static Calculation Save(CharacterState c, Ability ability)
    {
        var terms = Ability(c, ability).Terms.ToList();
        if (c.SaveProficiencies.Contains(ability)) terms.Add(new($"Saving throw proficiency · level {c.Level}", ProficiencyBonus(c.Level)));
        AppendAdjustment(c, "save:" + ability, terms);
        AppendExhaustion(c,terms);
        return Sum(terms);
    }

    public static Calculation Skill(CharacterState c, string skill, Ability ability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skill);
        var terms = Ability(c, ability).Terms.ToList();
        if (c.Expertise.Contains(skill, StringComparer.OrdinalIgnoreCase))
            terms.Add(new($"{skill} Expertise · twice proficiency", 2 * ProficiencyBonus(c.Level)));
        else if (c.SkillProficiencies.Contains(skill, StringComparer.OrdinalIgnoreCase))
            terms.Add(new($"{skill} proficiency · level {c.Level}", ProficiencyBonus(c.Level)));
        AppendAdjustment(c, "skill:" + skill, terms);
        AppendExhaustion(c,terms);
        return Sum(terms);
    }

    public static Calculation Attack(CharacterState c, Ability ability, bool proficient, int flatBonus = 0)
    {
        var terms = Ability(c, ability).Terms.ToList();
        if (proficient) terms.Add(new($"Weapon proficiency · level {c.Level}", ProficiencyBonus(c.Level)));
        if (flatBonus != 0) terms.Add(new("Declared attack bonus", flatBonus));
        AppendExhaustion(c,terms);
        return Sum(terms);
    }

    public static Calculation Initiative(CharacterState c)
    {
        var terms = Ability(c, Herohold.Core.Ability.Dexterity).Terms.ToList();
        if (RangerRules.IsGloomStalker(c))
            terms.AddRange(Ability(c, Herohold.Core.Ability.Wisdom).Terms.Select(x => x with { Label = "Dread Ambusher · " + x.Label }));
        AppendAdjustment(c, "initiative", terms);
        AppendExhaustion(c,terms);
        return Sum(terms);
    }

    private static void AppendAdjustment(CharacterState c, string key, List<CalculationTerm> terms)
    {
        var match = c.StandingAdjustments.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        WorkspaceValidation.StandingTerms(c);
        var named=c.StandingAdjustmentTerms.FirstOrDefault(x=>x.Key.Equals(key,StringComparison.OrdinalIgnoreCase));
        if(named.Value is not null){terms.AddRange(named.Value.Select(term=>term with{}));return;}
        if (match.Value != 0) terms.Add(new("Additional character features · " + key, match.Value));
    }

    private static Calculation Sum(List<CalculationTerm> terms) => new(terms.Sum(x => x.Value), terms.AsReadOnly());
}
