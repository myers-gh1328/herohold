namespace Herohold.Core;

/// <summary>What the rules work out from a character's build for the sheet. Recalculated on every load; never stored.</summary>
public sealed record DerivedSheet
{
    public List<WeaponAttack> Attacks { get; init; } = [];
    /// <summary>Attacks per Attack action when the rules state it (multiclass characters); otherwise worked out from class levels.</summary>
    public int? AttackActionCount { get; init; }
    public List<FeatureDetail> FeatureDetails { get; init; } = [];
    /// <summary>Descriptions of recorded features that have no structured detail, by feature name.</summary>
    public Dictionary<string, string> FeatureDescriptions { get; init; } = [];
    public List<string> SpeciesTraits { get; init; } = [];
    public string Size { get; init; } = "";
    /// <summary>Ability score increases from feats, Ability Score Improvements and other adjustments.</summary>
    public Dictionary<Ability, int> AbilityAdjustmentBonuses { get; init; } = [];
    public List<string> Languages { get; init; } = [];
    public List<string> ArmorProficiencies { get; init; } = [];
    public List<string> WeaponProficiencies { get; init; } = [];
    public List<string> ToolProficiencies { get; init; } = [];
    public List<string> OriginFeats { get; init; } = [];
    public List<CharacterSense> Senses { get; init; } = [];
    public ArmorClassDetail? ArmorClass { get; init; }
    public List<SpellcastingSource> SpellcastingSources { get; init; } = [];
    /// <summary>The spellbook the sheet shows, including spells the rules add automatically.</summary>
    public List<string> Spellbook { get; init; } = [];
    /// <summary>Spells granted with their own limited uses, so a renamed grant can keep what was spent.</summary>
    public List<GrantedSpellUse> GrantedSpells { get; init; } = [];
}

public sealed record GrantedSpellUse
{
    public string Id { get; init; } = "";
    public string ResourceId { get; init; } = "";
    public int BaseLevel { get; init; }
    /// <summary>The granting feature's display source.</summary>
    public string Source { get; init; } = "";
    /// <summary>The granting feature's build source id, when the rules name one.</summary>
    public string OwnerId { get; init; } = "";
}

/// <summary>A weapon or unarmed attack the character can make with what it has equipped.</summary>
public sealed record WeaponAttack
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>"weapon" or "unarmed".</summary>
    public string Kind { get; init; } = "weapon";
    public Ability Ability { get; init; }
    public List<Ability> AbilityCandidates { get; init; } = [];
    public bool Proficient { get; init; }
    /// <summary>Attack-roll bonus beyond ability and proficiency (Archery, an item's own bonus).</summary>
    public int FlatBonus { get; init; }
    public int AttackBonus { get; init; }
    public DiceFormula? DamageDice { get; init; }
    public int? FixedDamage { get; init; }
    public int DamageModifier { get; init; }
    /// <summary>Damage beyond the ability modifier, so the sheet can add the live modifier.</summary>
    public int DamageModifierAdjustment { get; init; }
    public string DamageType { get; init; } = "";
    public string Range { get; init; } = "";
    public string Notes { get; init; } = "";
}

public sealed record FeatureDetail
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>Where the feature comes from, such as "class:Ranger" or "species:Elf".</summary>
    public string Source { get; init; } = "";
    public List<RelatedFeature> RelatedFeatures { get; init; } = [];
}

/// <summary>An option or sub-feature listed under a feature, such as a Divine Order choice.</summary>
public sealed record RelatedFeature
{
    public string Name { get; init; } = "";
    public string Cost { get; init; } = "";
    public string Description { get; init; } = "";
}

/// <summary>A sense such as Darkvision; a range of 0 means the source gave none.</summary>
public sealed record CharacterSense
{
    public string Type { get; init; } = "";
    public int Range { get; init; }
    public List<string> Sources { get; init; } = [];
}

public sealed record ArmorClassDetail
{
    public int Value { get; init; }
    public string FormulaId { get; init; } = "";
    public string Source { get; init; } = "";
    public string SourceName { get; init; } = "";
    public bool ShieldApplied { get; init; }
    public int DefenseBonus { get; init; }
    public int? MediumArmorDexterityCap { get; init; }
    public List<ArmorClassCandidate> Candidates { get; init; } = [];
}

public sealed record ArmorClassCandidate
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Source { get; init; } = "";
    public int BaseValue { get; init; }
    public int Value { get; init; }
    public bool AllowsShield { get; init; }
    public bool ShieldApplied { get; init; }
    public int DefenseBonus { get; init; }
}

public sealed record SpellcastingSource
{
    public string SourceName { get; init; } = "";
    /// <summary>"class", "species", "feat" or another granting source kind.</summary>
    public string SourceType { get; init; } = "";
    public Ability Ability { get; init; }
    public int SpellSaveDc { get; init; }
    public int SpellAttackBonus { get; init; }
}
