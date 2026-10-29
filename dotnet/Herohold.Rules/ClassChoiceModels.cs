using System.Text.Json.Nodes;
using Herohold.Core;
namespace Herohold.Rules;

public enum ClassChoiceApplyTarget { None, Expertise, Skill, Save, Language, Tool, SpellGrant, Spellbook }
public sealed record ClassChoiceDependency(string? OptionKey=null,string? OptionValue=null,string? SubclassName=null);
public sealed record ClassChoiceSpellSource(string SpellName,string SourceId,IReadOnlyList<string> CountsAsClasses);
public sealed record ClassChoiceOwnedSpell(string SpellName,string SourceId);
public sealed record ClassChoiceContext
{
    public string ClassName { get; init; } = "";
    public string SubclassName { get; init; } = "";
    public int Level { get; init; } = 1;
    // The finite interpreter accepts only strings/string arrays/null. Invalid shapes remain repairable issues.
    public IReadOnlyDictionary<string,ChoiceValue> ClassOptions { get; init; } = new Dictionary<string,ChoiceValue>();
    public IReadOnlyDictionary<string,string> FeatureConfigurations { get; init; } = new Dictionary<string,string>();
    public IReadOnlyList<ChoiceProficiencySource> ProficiencySources { get; init; } = [];
    public IReadOnlyList<ClassChoiceSpellSource> CantripSources { get; init; } = [];
    public IReadOnlyList<ClassChoiceOwnedSpell> PreparedSpellSources { get; init; } = [];
    public IReadOnlyList<string> Spellbook { get; init; } = [];
    public IReadOnlyList<ClassChoiceOwnedSpell> SavantSpells { get; init; } = [];
    public IReadOnlyList<string> LegacyRogueExpertise { get; init; } = [];
}
public sealed record ClassChoiceDefinition
{
    public string Key { get; init; } = "";
    public string SourceId { get; init; } = "";
    public int SourceLevel { get; init; }
    public string Name { get; init; } = "";
    public BuildChoiceMode Mode { get; init; }
    public int Choose { get; init; }
    public IReadOnlyList<string> NewOptions { get; init; } = [];
    public IReadOnlyList<string> ValidExistingOptions { get; init; } = [];
    public ChoiceValue? PreservedSelection { get; init; }
    public ChoiceValue? EffectiveSelection { get; init; }
    public IReadOnlyList<string> ValidSelected { get; init; } = [];
    public int Remaining { get; init; }
    public BuildChoiceStatus Status { get; init; }
    public ClassChoiceDependency? Dependency { get; init; }
    public ClassChoiceApplyTarget ApplyTo { get; init; }
    public IReadOnlyList<BuildIssue> Issues { get; init; } = [];
}
public sealed record ClassChoiceReport
{
    public IReadOnlyList<ClassChoiceDefinition> Definitions { get; init; } = [];
    public IReadOnlyList<ClassChoiceDefinition> Unresolved { get; init; } = [];
    public IReadOnlyList<BuildIssue> Issues { get; init; } = [];
}
