using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Core;
using Herohold.Rules;

namespace Herohold.Web.Services;

/// <summary>Everything one calculation of a draft finds, before the character's play facts are added.</summary>
internal sealed record CharacterCalculation
{
    public string Id { get; init; } = "";
    /// <summary>The build as the rules settled it.</summary>
    public BuildFacts Build { get; init; } = new();
    public bool KnownClass { get; init; }
    /// <summary>The draft's own items, or those its starting equipment gives.</summary>
    public IReadOnlyList<EquipmentItem> Equipment { get; init; } = [];
    public bool OwnsInventory { get; init; }
    public List<string> CustomEquipment { get; init; } = [];
    /// <summary>Notes a new or imported draft brings without journal entries.</summary>
    public string Notes { get; init; } = "";
    public Dictionary<Ability, int> Scores { get; init; } = [];
    public Dictionary<Ability, int> Modifiers { get; init; } = [];
    public Dictionary<Ability, int> SaveBonuses { get; init; } = [];
    public Dictionary<string, int> SkillBonuses { get; init; } = new(StringComparer.Ordinal);
    public int Initiative { get; init; }
    public Dictionary<Ability, int> AdjustmentBonuses { get; init; } = [];
    public List<Ability> SaveProficiencies { get; init; } = [];
    public List<string> SkillProficiencies { get; init; } = [];
    public List<string> Expertise { get; init; } = [];
    public List<string> Languages { get; init; } = [];
    public Dictionary<string, List<CalculationTerm>> StandingTerms { get; init; } = new(StringComparer.Ordinal);
    public IReadOnlyList<string> Features { get; init; } = [];
    public IReadOnlyList<string> SpeciesTraits { get; init; } = [];
    public IReadOnlyList<string> OriginFeats { get; init; } = [];
    public IReadOnlyList<CharacterSense> Senses { get; init; } = [];
    public string Size { get; init; } = "";
    public IReadOnlyList<FeatureDetail> FeatureDetails { get; init; } = [];
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Facts { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
    /// <summary>The proficiencies and defenses the build itself gives, without those entered by hand.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> DerivedFacts { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
    public int MaximumHp { get; init; }
    public int CurrentHp { get; init; }
    public int TemporaryHp { get; init; }
    public int HitDie { get; init; } = 8;
    public int ArmorClass { get; init; } = 10;
    public ArmorClassDetail? Armor { get; init; }
    public int Speed { get; init; } = 30;
    public int BaseSpeed { get; init; } = 30;
    public IReadOnlyList<MovementTerm> SpeedTerms { get; init; } = [];
    public IReadOnlyDictionary<string, ChoiceValue> DerivedResources { get; init; } = new Dictionary<string, ChoiceValue>();
    public List<ResourceDefinition> Resources { get; init; } = [];
    public IReadOnlyList<SpellGrantMetadata> Grants { get; init; } = [];
    public IReadOnlyList<SpellGrantSourceIdentity> GrantSources { get; init; } = [];
    public IReadOnlyList<string> GrantedSpellNames { get; init; } = [];
    public IReadOnlyList<string> Spellbook { get; init; } = [];
    public List<SpellDefinition> Spells { get; init; } = [];
    public Ability? SpellcastingAbility { get; init; }
    /// <summary>How the character prepares spells; unset for a multiclass character, whose spell sources each say so.</summary>
    public SpellPreparation? Preparation { get; init; }
    /// <summary>A multiclass character's prepared-spell limit: its initial class's.</summary>
    public int PreparedLimit { get; init; }
    public List<SpellcastingSource> CastingSources { get; init; } = [];
    public List<WeaponAttack> Attacks { get; init; } = [];
    public int? AttackActionCount { get; init; }
    /// <summary>A multiclass character's spell sources, their spells and its Hit Dice pools.</summary>
    public List<SpellSourceState>? SpellSources { get; init; }
    public List<SpellDefinition>? OwnedSpells { get; init; }
    public List<HitDiePool>? HitDiePools { get; init; }
    /// <summary>Problems the rules found in the build, beside the builder's own choice issues.</summary>
    public IReadOnlyList<BuildIssue> Issues { get; init; } = [];
    public IReadOnlyList<BuilderChoiceDefinition> Choices { get; init; } = [];
    public WorkflowValidation Validation { get; init; } = new();
}

/// <summary>A resource the rules give a character, before what was spent is known.</summary>
internal sealed record ResourceDefinition(string Id, string Name, int Maximum, string Recovery, int? SlotLevel = null, string? SourceId = null, int? ShortRestRecoveryAmount = null);

/// <summary>How a spellcaster prepares spells, with spells named rather than identified.</summary>
internal sealed record SpellPreparation
{
    public bool CanChangeCantripsOnLongRest { get; init; }
    public int CantripReplacementLimit { get; init; }
    public IReadOnlyList<string> OwnedCantrips { get; init; } = [];
    public IReadOnlyList<string> EligibleCantrips { get; init; } = [];
    public bool CanChangeOnLongRest { get; init; }
    public int PreparedLimit { get; init; }
    public int? ReplacementLimit { get; init; }
    public IReadOnlyList<string> EligibleSpells { get; init; } = [];
}
