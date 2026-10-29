using Herohold.Core;
namespace Herohold.Rules;

public sealed record BackgroundAbilityBonus
{
    public string Name { get; init; } = "Background";
    public IReadOnlyList<Ability> AllowedAbilities { get; init; } = Enum.GetValues<Ability>();
    public Ability? Primary { get; init; }
    public Ability? Secondary { get; init; }
    public int PrimaryBonus { get; init; } = 2;
    public int SecondaryBonus { get; init; } = 1;
    public bool SplitEvenly { get; init; }
}
public sealed record ScoreAdjustment
{
    public string SourceId { get; init; } = "";
    public string SourceName { get; init; } = "";
    public IReadOnlyDictionary<Ability,int> Increases { get; init; } = new Dictionary<Ability,int>();
    public int Maximum { get; init; } = 20;
}
public sealed record AbilityScoreInput
{
    public string ClassName { get; init; } = "";
    public int Level { get; init; } = 1;
    public IReadOnlyDictionary<Ability,int> BaseScores { get; init; } = new Dictionary<Ability,int>();
    public BackgroundAbilityBonus? Background { get; init; }
    public IReadOnlyList<ScoreAdjustment> Adjustments { get; init; } = [];
}
public sealed record AbilityScoreSummary
{
    public IReadOnlyDictionary<Ability,int> Scores { get; init; } = new Dictionary<Ability,int>();
    public IReadOnlyDictionary<Ability,int> BackgroundBonuses { get; init; } = new Dictionary<Ability,int>();
    public IReadOnlyDictionary<Ability,int> SourceBonuses { get; init; } = new Dictionary<Ability,int>();
    public IReadOnlyDictionary<Ability,int> Maximums { get; init; } = new Dictionary<Ability,int>();
    public IReadOnlyDictionary<Ability,Calculation> Calculations { get; init; } = new Dictionary<Ability,Calculation>();
}
public enum DivineOrder { Protector, Thaumaturge }
public enum PrimalOrder { Magician, Warden }
public sealed record NumericFeatSelection
{
    public string Name { get; init; } = "";
    public string SkillProficiency { get; init; } = "";
    public string Expertise { get; init; } = "";
    public string Skill { get; init; } = "";
    public Ability? Ability { get; init; }
}
public sealed record StandingInput
{
    public string ClassName { get; init; } = "";
    public string Subclass { get; init; } = "";
    public int Level { get; init; } = 1;
    public IReadOnlyDictionary<Ability,int> Scores { get; init; } = new Dictionary<Ability,int>();
    public IReadOnlyList<EquipmentItem> Equipment { get; init; } = [];
    public IReadOnlyList<Ability> SaveProficiencies { get; init; } = [];
    // Base sets BEFORE selected general-feat grants. Never feed final canonical grants back into this input.
    public IReadOnlyList<string> SkillProficiencies { get; init; } = [];
    public IReadOnlyList<string> Expertise { get; init; } = [];
    public IReadOnlyList<string> OriginFeats { get; init; } = [];
    public IReadOnlyList<NumericFeatSelection> Feats { get; init; } = [];
    public DivineOrder? DivineOrder { get; init; }
    public PrimalOrder? PrimalOrder { get; init; }
}
public sealed record NumericProficiencyGrant(string Source,string Kind,string Target);
public sealed record StandingSummary
{
    public IReadOnlyDictionary<Ability,Calculation> Modifiers { get; init; } = new Dictionary<Ability,Calculation>();
    public IReadOnlyDictionary<Ability,Calculation> Saves { get; init; } = new Dictionary<Ability,Calculation>();
    public IReadOnlyDictionary<string,Calculation> Skills { get; init; } = new Dictionary<string,Calculation>();
    public Calculation Initiative { get; init; } = new(0,[]);
    public IReadOnlyList<Ability> SaveProficiencies { get; init; } = [];
    public IReadOnlyList<string> SkillProficiencies { get; init; } = [];
    public IReadOnlyList<string> Expertise { get; init; } = [];
    public IReadOnlyList<NumericProficiencyGrant> Grants { get; init; } = [];
    public IReadOnlyList<string> UnmodeledStandingFeats { get; init; } = [];
}
