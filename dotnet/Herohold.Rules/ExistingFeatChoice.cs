namespace Herohold.Rules;

public sealed record ExistingFeatChoice
{
    public string InstanceId { get; init; } = "";
    public string FeatId { get; init; } = "";
    public string Name { get; init; } = "";
    public int? KnownTakenLevel { get; init; }
    // Relative paths into the fixed feat-mode choice value, not array indexes.
    public IReadOnlyList<BuilderChoiceDefinition> Fields { get; init; } = [];
    public IReadOnlyList<BuildIssue> Issues { get; init; } = [];
}
