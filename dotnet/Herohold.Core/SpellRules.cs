namespace Herohold.Core;

public static class SpellCatalog
{
    private static readonly SpellDefinition[] Definitions =
    [
        new() { Id="fire-bolt",Name="Fire Bolt",Source="XPHB",Level=0,Modeled=true,EffectKind=SpellEffectKind.Damage,Dice=new(1,10),DamageType="Fire",Range="120 feet",Resolution="spell-attack",AttackRollKind="ranged",AttackPattern=SpellAttackPattern.Single,Scaling=ScalingStatus.NoBenefit,
            CharacterLevelScaling=[new(1,new(1,10)),new(5,new(2,10)),new(11,new(3,10)),new(17,new(4,10))],Description="Make one ranged spell attack. The listed dice are damage for that hit; damage scales with total character level. Resolve the burning-object rider at the table." },
        new() { Id="eldritch-blast",Name="Eldritch Blast",Source="XPHB",Level=0,Modeled=true,EffectKind=SpellEffectKind.Damage,Dice=new(1,10),DamageType="Force",Range="120 feet",Resolution="spell-attack",AttackRollKind="ranged",AttackPattern=SpellAttackPattern.EldritchBlast,Scaling=ScalingStatus.NoBenefit,
            Description="Make one ranged spell attack per beam, with 1d10 Force damage per hit. Total character levels5,11,17 grant two,three,four beams. Resolve additional class and item bonuses separately." },
        new() { Id="scorching-ray",Name="Scorching Ray",Source="XPHB",Level=2,Modeled=true,EffectKind=SpellEffectKind.Damage,Dice=new(2,6),DamageType="Fire",Range="120 feet",Resolution="spell-attack",AttackRollKind="ranged",AttackPattern=SpellAttackPattern.ScorchingRay,Scaling=ScalingStatus.Modeled,
            Description="Make a separate ranged spell attack for each of three rays. Each hit deals2d6 Fire damage. A higher spell slot adds one ray per slot level above2; it does not increase each ray's damage." },
        new() { Id = "fireball", Name = "Fireball", Source = "XPHB", Level = 3, Modeled = true, EffectKind = SpellEffectKind.Damage,
            Dice = new(8, 6), DamageType = "Fire", RadiusFt = 20, SaveAbility = Ability.Dexterity, Range = "150 feet",
            Resolution = "target-save-half", Scaling = ScalingStatus.Modeled, DicePerSlot = 1,
            Description = "Creatures in a 20-foot-radius Sphere make a Dexterity save: 8d6 Fire damage, half on success. Add 1d6 per slot above third. Resolve saves and affected objects at the table." },
        new() { Id = "burning-hands", Name = "Burning Hands", Source = "XPHB", Level = 1, Modeled = true, EffectKind = SpellEffectKind.Damage,
            Dice = new(3, 6), DamageType = "Fire", SaveAbility = Ability.Dexterity, Range = "15-foot Cone",
            Resolution = "target-save-half", Scaling = ScalingStatus.Modeled, DicePerSlot = 1,
            Description = "Creatures in a 15-foot Cone make a Dexterity save: 3d6 Fire damage, half on success. Add 1d6 per slot above first. Resolve saves and affected objects at the table." },
        new() { Id = "thunderwave", Name = "Thunderwave", Source = "XPHB", Level = 1, Modeled = true, EffectKind = SpellEffectKind.Damage,
            Dice = new(2, 8), DamageType = "Thunder", SaveAbility = Ability.Constitution, Range = "15-foot Cube",
            Resolution = "target-save-half", Scaling = ScalingStatus.Modeled, DicePerSlot = 1,
            Description = "Creatures in a 15-foot Cube originating from you make a Constitution save: 2d8 Thunder damage, half on success. Add 1d8 per slot above first. Resolve the failed-save push, objects, and noise at the table." },
        new() { Id = "healing-word", Name = "Healing Word", Source = "XPHB", Level = 1, Modeled = true, EffectKind = SpellEffectKind.Healing,
            Dice = new(2, 4), AddCastingModifier = true, CastingTime = "Bonus action", Range = "60 feet",
            Resolution = "healing", Scaling = ScalingStatus.Modeled, DicePerSlot = 2,
            Description = "A creature you can see within 60 feet regains 2d4 plus your spellcasting modifier HP. Add 2d4 per slot above first. Apply the result to the chosen target manually." },
        new() { Id = "hail-of-thorns", Name = "Hail of Thorns", Level = 1, Modeled = true, EffectKind = SpellEffectKind.Damage,
            Dice = new(1, 10), DamageType = "Piercing", RadiusFt = 5, SaveAbility = Ability.Dexterity,
            Resolution = "target-save-half", Trigger = "ranged-weapon-hit", CastingTime = "Bonus action", Range = "Self", Scaling = ScalingStatus.Modeled, DicePerSlot = 1,
            Description = "After hitting with a ranged weapon, thorns affect the struck creature and creatures within 5 feet. Each makes a Dexterity save; half damage on success." },
        new() { Id = "cure-wounds", Name = "Cure Wounds", Level = 1, Modeled = true, EffectKind = SpellEffectKind.Healing,
            Dice = new(2, 8), AddCastingModifier = true, Resolution = "healing", Range = "Touch", Scaling = ScalingStatus.Modeled, DicePerSlot = 2,
            Description = "A creature you touch regains 2d8 plus your spellcasting modifier HP. Add 2d8 for each slot level above first. Apply the result to the chosen target manually." },
        new() { Id = "fog-cloud", Name = "Fog Cloud", Level = 1, Modeled = true, EffectKind = SpellEffectKind.Utility,
            RadiusFt = 20, Concentration = true, DurationAmount = 1, DurationUnit = "hour", Resolution = "area", Range = "120 feet", Scaling = ScalingStatus.Modeled, RadiusPerSlot = 20,
            Description = "Create a heavily obscuring fog sphere. Increase its radius by 20 feet for each slot level above first. Maintain concentration." },
        new() { Id = "hunters-mark", Name = "Hunter’s Mark", Level = 1, Modeled = true, EffectKind = SpellEffectKind.Damage,
            Dice = new(1, 6), DamageType = "Force", Concentration = true, DurationAmount = 1, DurationUnit = "hour", Resolution = "marked-attack-hit", Trigger = "attack-hit-marked-target",
            CastingTime = "Bonus action", Range = "90 feet", Scaling = ScalingStatus.Modeled,
            DurationThresholds = [new(3, 8, "hour"), new(5, 24, "hour")], Description = "Mark your quarry and maintain concentration. Each qualifying attack hit adds 1d6 Force damage. Higher slots extend duration, not the damage die." },
        new() { Id = "disguise-self", Name = "Disguise Self", Level = 1, Modeled = true, DurationAmount = 1, DurationUnit = "hour",
            Range = "Self", Scaling = ScalingStatus.NoBenefit, Description = "Change your apparent appearance for one hour. Higher slots do not improve this effect." },
        new() { Id = "jump", Name = "Jump", Level = 1, Range = "Touch", Description = "Read the complete spell before applying movement effects. Spend a slot explicitly; its effect remains table-managed." },
        new() { Id = "pass-without-trace", Name = "Pass without Trace", Level = 2, Range = "Self", Concentration = true, Description = "A veil of shadows and silence aids selected creatures. Numerical effects remain table-managed." },
        new() { Id = "lesser-restoration", Name = "Lesser Restoration", Level = 2, Range = "Touch", Description = "End an eligible condition on the chosen creature according to the spell. Remove that effect explicitly after casting." },
        new() { Id = "rope-trick", Name = "Rope Trick", Level = 2, Range = "Touch", DurationAmount = 1, DurationUnit = "hour", Description = "Create an extradimensional refuge. Read the complete spell; the utility effect remains table-managed." },
        new() { Id = "goodberry", Name = "Goodberry", Level = 1, Range = "Self", Description = "Conjure magical berries. Track their use in inventory; the effect remains table-managed." },
        new() { Id = "ensnaring-strike", Name = "Ensnaring Strike", Level = 1, Concentration = true, Description = "Read the full spell and manage the ensuing effect at the table." },
        SaveCantrip("acid-splash", "Acid Splash", 6, "Acid", Ability.Dexterity, "60 feet", 5),
        SaveCantrip("sacred-flame", "Sacred Flame", 8, "Radiant", Ability.Dexterity, "60 feet"),
        SaveCantrip("vicious-mockery", "Vicious Mockery", 6, "Psychic", Ability.Wisdom, "60 feet"),
        SaveCantrip("mind-sliver", "Mind Sliver", 6, "Psychic", Ability.Intelligence, "60 feet"),
        SaveCantrip("thunderclap", "Thunderclap", 6, "Thunder", Ability.Constitution, "Self", 5),
        SaveCantrip("word-of-radiance", "Word of Radiance", 6, "Radiant", Ability.Constitution, "Self", 5)
    ];

    public static IReadOnlyList<SpellDefinition> All => Definitions.Select(Clone).ToArray();

    public static SpellDefinition Find(CharacterState c, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var custom = c.Play.Spells.FirstOrDefault(x => x.Id == id);
        var definition = custom ?? Definitions.FirstOrDefault(x => x.Id == id);
        if(definition is null)throw new ArgumentException("That spell has no catalog record.", nameof(id));
        // Old native saves keep reference records, not a frozen rules-engine version.
        // Derive newly reviewed execution for a trusted source without rewriting its
        // stored definition or reconstructing an already-recorded casting receipt.
        if(custom is not null&&custom.AttackRollKind is null&&custom.AttackPattern==SpellAttackPattern.Unmodeled&&(custom.RulesSource is "XPHB" or "2024"||custom.Source is "XPHB" or "2024")&&Definitions.FirstOrDefault(s=>s.Name.Equals(custom.Name,StringComparison.OrdinalIgnoreCase)&&s.AttackPattern!=SpellAttackPattern.Unmodeled) is {} reviewed)
            definition=custom with{AttackRollKind=reviewed.AttackRollKind,AttackPattern=reviewed.AttackPattern,Modeled=reviewed.Modeled,EffectKind=reviewed.EffectKind,Dice=reviewed.Dice,DamageType=reviewed.DamageType,AddCastingModifier=reviewed.AddCastingModifier,SaveAbility=reviewed.SaveAbility,Scaling=reviewed.Scaling,DicePerSlot=reviewed.DicePerSlot,CharacterLevelScaling=[..reviewed.CharacterLevelScaling],Resolution=reviewed.Resolution,Trigger=reviewed.Trigger};

        var source=ClassPlayRules.SpellSource(c,id);
        return Clone(source is null?definition:definition with{CastingAbility=source.Ability,CastingSource=source.Name});
    }

    private static SpellDefinition Clone(SpellDefinition spell) => spell with { DurationThresholds = [.. spell.DurationThresholds], CharacterLevelScaling = [.. spell.CharacterLevelScaling] };

    // Explicit XPHB scalingLevelDice, damageInflict and savingThrow fields were reviewed
    // together with each spell's text. Conditional riders and exploding damage remain table-managed;
    // finite multi-beam attack counts are modeled independently from per-hit damage.
    private static SpellDefinition SaveCantrip(string id, string name, int sides, string damageType, Ability save, string range, int? radius = null) => new()
    {
        Id = id, Name = name, Source = "XPHB", Level = 0, Modeled = true,
        EffectKind = SpellEffectKind.Damage, Dice = new(1, sides), DamageType = damageType,
        SaveAbility = save, Range = range, RadiusFt = radius, Resolution = "target-save-negates", Scaling = ScalingStatus.NoBenefit,
        CharacterLevelScaling = [new(1, new(1, sides)), new(5, new(2, sides)), new(11, new(3, sides)), new(17, new(4, sides))],
        Description = "Resolve the target's saving throw using the complete spell. This models base damage only; additional class/item bonuses and spell riders remain table-managed."
    };
}

public static class SpellRules
{
    public static bool RequiresCastingConcentration(SpellDefinition spell, bool ritual = false)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return spell.Concentration || ritual || System.Text.RegularExpressions.Regex.IsMatch(spell.CastingTime.Trim(), @"\A[1-9][0-9]* (minutes?|hours?)\z", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
    public static CastingPreview Preview(CharacterState c, string spellId, int slotLevel)
    {
        var spell = SpellCatalog.Find(c, spellId);
        if (spell.Level is < 0 or > 9 || slotLevel < spell.Level || slotLevel > 9 || spell.Level == 0 && slotLevel != 0)
            throw new ArgumentOutOfRangeException(nameof(slotLevel), "Choose an eligible slot level; cantrips do not use spell slots.");
        int? attacks=spell.AttackRollKind is null?0:spell.AttackPattern switch
        {
            SpellAttackPattern.Single=>1,
            SpellAttackPattern.EldritchBlast=>c.Level>=17?4:c.Level>=11?3:c.Level>=5?2:1,
            SpellAttackPattern.ScorchingRay=>3+slotLevel-spell.Level,
            _=>null
        };
        var higher = slotLevel - spell.Level;
        var modeledScaling = spell.Scaling == ScalingStatus.Modeled;
        var effectModeled = spell.Modeled && !(higher > 0 && spell.Scaling == ScalingStatus.Unmodeled);
        var dice = effectModeled && spell.Dice is not null ? spell.Dice with { Count = spell.Dice.Count + (modeledScaling ? higher * spell.DicePerSlot : 0) } : null;
        if (effectModeled && spell.Level == 0 && spell.CharacterLevelScaling.Where(x => x.MinimumCharacterLevel <= c.Level).OrderByDescending(x => x.MinimumCharacterLevel).FirstOrDefault() is { } cantrip)
            dice = cantrip.Dice;
        if (effectModeled && spell.Resolution == "marked-attack-hit") dice = RangerRules.HuntersMarkDamage(c);
        if (dice is not null) PlayDice.ValidateFormula(dice);
        var castingCalculation = CharacterCalculations.Ability(c, spell.CastingAbility ?? c.Play.SpellcastingAbility);
        var proficiency = CharacterCalculations.ProficiencyBonus(c.Level);
        var modifier = effectModeled && spell.AddCastingModifier ? castingCalculation.Total : 0;
        Calculation? dcCalculation = spell.SaveAbility is null ? null : new(8 + proficiency + castingCalculation.Total,
            new[] { new CalculationTerm("Base spell save DC", 8), new CalculationTerm($"Proficiency · level {c.Level}", proficiency) }.Concat(castingCalculation.Terms).ToList().AsReadOnly());
        var duration = spell.DurationThresholds.Where(x => x.MinimumSlotLevel <= slotLevel).OrderByDescending(x => x.MinimumSlotLevel).FirstOrDefault();
        var amount = duration?.Amount ?? spell.DurationAmount;
        var unit = duration?.Unit ?? spell.DurationUnit;
        var note = !spell.Modeled ? "Effect execution is unmodeled; pay the selected resource and resolve the spell using its rules text."
            : higher > 0 && spell.Scaling == ScalingStatus.Unmodeled ? "Higher-slot scaling is unmodeled; confirm the complete effect at the table."
            : higher > 0 && spell.Scaling == ScalingStatus.NoBenefit ? "No higher-slot benefit." : string.Empty;
        if(attacks is >0&&effectModeled)note="Make each spell attack separately. The listed dice are damage for one hit; additional source bonuses and riders remain table-managed.";
        if (effectModeled && spell.Level == 0 && spell.EffectKind == SpellEffectKind.Damage && note.Length == 0)
            note = "Base spell damage only. Resolve the target save, riders, and additional class/item bonuses at the table.";
        if (RequiresCastingConcentration(spell) && !spell.Concentration)
            note += " Casting for at least 1 minute requires Concentration during casting and ends any earlier concentration effect.";
        else if (spell.Ritual)
            note += " Casting as a Ritual requires Concentration during casting and ends any earlier concentration effect.";
        return new()
        {
            AttackRollKind=spell.AttackRollKind,AttackRollCount=attacks,
            CastingModifierCalculation = castingCalculation, SaveDcCalculation = dcCalculation,
            SpellId = spell.Id, Name = spell.Name, SlotLevel = slotLevel, EffectKind = spell.EffectKind, Dice = dice, Modifier = modifier,
            FormulaText = dice is null ? string.Empty : $"{dice.Count}d{dice.Sides}" + (modifier == 0 ? "" : modifier > 0 ? $" + {modifier}" : $" − {-modifier}"),
            DamageType = spell.DamageType, RadiusFt = !effectModeled || spell.RadiusFt is null ? null : spell.RadiusFt + (modeledScaling ? higher * spell.RadiusPerSlot : 0),
            SaveAbility = spell.SaveAbility, SaveDc = dcCalculation?.Total,
            Concentration = spell.Concentration, DurationText = !spell.Modeled && amount == 0 && unit == "instantaneous" ? "See full spell duration (unmodeled)" : amount == 0 ? unit : $"{amount} {unit}", Resolution = spell.Resolution,
            Trigger = spell.Trigger, Note = note, Modeled = effectModeled
        };
    }
}
