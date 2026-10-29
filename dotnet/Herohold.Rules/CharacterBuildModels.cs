using Herohold.Core;
namespace Herohold.Rules;
public sealed record ManualProficiencySources
{
    public IReadOnlyList<string> Skills { get; init; } = [];
    public IReadOnlyList<string> Expertise { get; init; } = [];
    public IReadOnlyList<Ability> Saves { get; init; } = [];
}
public sealed record NativeBuildSummary
{
    public string ClassName { get; init; } = "";
    public string SubclassName { get; init; } = "";
    public AbilityScoreSummary Scores { get; init; } = new();
    public StandingSummary Standing { get; init; } = new();
    public ManualProficiencySources ManualSources { get; init; } = new();
    public IReadOnlyList<ChoiceProficiencySource> ProficiencySources { get; init; } = [];
    public LanguageSummary Languages {get;init;}=new();
    public IReadOnlyList<string> Issues { get; init; } = [];
}
