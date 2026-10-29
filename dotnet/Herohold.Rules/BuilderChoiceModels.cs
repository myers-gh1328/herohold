using Herohold.Core;
namespace Herohold.Rules;

public enum BuildChoiceMode { Text, Single, Multi, AbilityScores, AsiOrFeat }
public enum BuildChoiceStatus { Resolved, Unresolved, Unsupported }
public enum BuildIssueSeverity { Information, Warning, Error }
public enum ProficiencySourceKind { Skill, Expertise, Save, Language, Tool, Armor, Weapon }
public sealed record BuildIssue(string Code,IReadOnlyList<string> Path,string Message,BuildIssueSeverity Severity=BuildIssueSeverity.Error);
public sealed record ChoiceProficiencySource(string SourceId,ProficiencySourceKind Kind,string Target);
public sealed record BuildChoiceOption
{
    public ChoiceValue Value { get; init; } = new();
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public string Source { get; init; } = "";
    public bool Disabled { get; init; }
    public string Reason { get; init; } = "";
    /// <summary>Choices that come with picking this option, such as a feat's own choices.</summary>
    public IReadOnlyList<BuilderChoiceDefinition> Children { get; init; } = [];
}
