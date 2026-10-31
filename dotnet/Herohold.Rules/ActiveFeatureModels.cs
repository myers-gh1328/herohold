using Herohold.Core;
namespace Herohold.Rules;
/// <param name="RelatedFeatures">The catalog's options and sub-features listed under a class or subclass feature, up to the class level.</param>
public sealed record ActiveFeatureDetail(string Name,string Description,string SourceId,IReadOnlyList<CatalogRelatedFeature> RelatedFeatures);
/// <summary>A class feature whose chosen option can be changed later (for example Hunter's Prey after a rest).</summary>
public sealed record FeatureConfiguration
{
    public string FeatureId { get; init; } = "";
    public string FeatureName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string SubclassName { get; init; } = "";
    public int LevelGained { get; init; }
    public string InitialValue { get; init; } = "";
    public string CurrentValue { get; init; } = "";
    /// <summary>When the option may change: "long_rest" or "short_or_long_rest".</summary>
    public string ChangeTiming { get; init; } = "";
    public IReadOnlyList<string> EligibleOptions { get; init; } = [];
}
public sealed record ActiveFeatureSummary
{
    public IReadOnlyList<string> Features { get; init; } = [];
    public IReadOnlyList<ActiveFeatureDetail> Details { get; init; } = [];
    public IReadOnlyList<DamageDefense> Defenses { get; init; } = [];
    public IReadOnlyList<CharacterSense> Senses { get; init; } = [];
    public IReadOnlyList<ChoiceProficiencySource> Proficiencies { get; init; } = [];
    public IReadOnlyList<FeatureConfiguration> FeatureConfigurations { get; init; } = [];
    public IReadOnlyList<string> SpeciesTraits { get; init; } = [];
    public IReadOnlyList<string> OriginFeats { get; init; } = [];
    public string Size { get; init; } = "";
    public string BackgroundFeature { get; init; } = "";
    public IReadOnlyList<BuildIssue> Issues { get; init; } = [];
}
