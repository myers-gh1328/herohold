using Herohold.Core;
namespace Herohold.Rules;

/// <summary>A character the rules work on while drafting: its build and inventory, optionally one class track of a multiclass character.</summary>
public sealed record DraftCharacter(BuildFacts Build,IReadOnlyList<EquipmentItem> Equipment,DraftScope? Scope=null);
/// <summary>A single class track scoped out of a multiclass character: the character's total level, and whether this is an added class.</summary>
public sealed record DraftScope(int TotalLevel,bool AdditionalClass);
/// <summary>A build as the rules settle it (normalized identities, recalculated HP history), with the maximum HP it gives.</summary>
public sealed record SettledBuild(BuildFacts Build,int MaximumHp);

/// <summary>Pure character rules supplied by the native builder; no persistence or runtime bridge.</summary>
public interface ICharacterDraftRules
{
    SettledBuild Recalculate(DraftCharacter character);
    CreationChoiceReport Describe(DraftCharacter character);
    FeatRuleContext FeatContext(DraftCharacter character);
}

public sealed record LevelUpDraft
{
    public string TransactionId { get; init; } = "";
    public string SourceHash { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string ClassTrackId {get;init;}="";
    public int CurrentClassLevel {get;init;}
    public int TargetClassLevel {get;init;}
    public bool AddsNewClass {get;init;}
    public int CurrentLevel { get; init; }
    public int TargetLevel { get; init; }
    public bool Completed { get; init; }
    public IReadOnlyDictionary<string, ChoiceValue> Selections { get; init; } = new Dictionary<string, ChoiceValue>();
}
public sealed record ClassEntryOption(string Name,bool Allowed,string Reason);

public sealed record LevelUpHpOptions
{
    public HitPointMethod Method { get; init; } = HitPointMethod.Fixed;
    public int? Roll { get; init; }
    public bool AddGainedHp { get; init; }
}

public sealed record LevelUpReview
{
    public LevelUpDraft Draft { get; init; } = new();
    /// <summary>The build after the level-up, as settled by the rules.</summary>
    public BuildFacts Build { get; init; } = new();
    public int MaximumHp { get; init; }
    public IReadOnlyList<BuilderChoiceDefinition> PendingChoices { get; init; } = [];
    public IReadOnlyList<string> AutomaticFeatures { get; init; } = [];
    public IReadOnlyList<BuildIssue> Issues { get; init; } = [];
    public bool CanFinalize { get; init; }
    public int HitDie { get; init; }
    public int HpBaseGain { get; init; }
    public int HpIncrease { get; init; }
    public int ProficiencyBonusBefore { get; init; }
    public int ProficiencyBonusAfter { get; init; }
}

public sealed class LevelUpException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
