using Herohold.Core;
using System.Text.Json.Nodes;
namespace Herohold.Rules;

public sealed record OwnedFeatSelection(string SourceId,string FeatId,string Name,int LevelTaken,IReadOnlyDictionary<string,ChoiceValue> Choices);
public sealed record FeatRuleContext
{
    public int Level { get; init; } = 1;
    public IReadOnlyDictionary<string,int> Scores { get; init; } = new Dictionary<string,int>();
    public bool HasSpellcastingFeature { get; init; }
    public IReadOnlyList<string> Features { get; init; } = [];
    public IReadOnlyList<ChoiceProficiencySource> ProficiencySources { get; init; } = [];
    public IReadOnlyDictionary<string,int> SourceLevels { get; init; } = new Dictionary<string,int>();
    public IReadOnlyList<OwnedFeatSelection> OwnedFeats { get; init; } = [];
    public string EditingSourceId { get; init; } = "";
    public IReadOnlyList<string> EligibleWeaponMasteries { get; init; } = [];
    public IReadOnlyList<string> ExistingWeaponMasteries { get; init; } = [];
}
public sealed record FeatEligibility(bool Eligible,bool Selectable,IReadOnlyList<BuildIssue> Issues);
public sealed record FeatChoiceField(string Key,string Label,BuildChoiceMode Mode,int Choose,IReadOnlyList<string> Options);
public sealed record FeatSelectionVerdict(bool Valid,IReadOnlyList<BuildIssue> Issues);
