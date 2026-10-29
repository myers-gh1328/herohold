using Herohold.Core;
namespace Herohold.Rules;

public sealed record CreationChoiceContext
{
    public BuildFacts Build { get; init; } = new();
    /// <summary>Whether the character already carries its own inventory, so no starting equipment package is offered.</summary>
    public bool HasInventory { get; init; }
    public ClassChoiceContext ClassContext { get; init; } = new();
    public IReadOnlyList<string> EligibleWeaponMasteries { get; init; } = [];
}
public sealed record BuilderChoiceDefinition
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Section { get; init; } = "class";
    public IReadOnlyList<string> Path { get; init; } = [];
    public BuildChoiceMode Mode { get; init; }
    public int Choose { get; init; } = 1;
    public bool Required { get; init; } = true;
    public IReadOnlyList<BuildChoiceOption> Options { get; init; } = [];
    public ChoiceValue? PreservedSelection { get; init; }
    public BuildChoiceStatus Status { get; init; }
    public ChoiceDetails Details { get; init; } = new();
    public IReadOnlyList<BuildIssue> Issues { get; init; } = [];
}
/// <summary>What a builder editor needs beyond the options, and what later rules read back from a choice.</summary>
public sealed record ChoiceDetails
{
    /// <summary>A specific editor when the mode alone does not say, such as "feat_selection".</summary>
    public string? UiType { get; init; }
    public int? Minimum { get; init; }
    public int? Maximum { get; init; }
    /// <summary>The value the rules use when it differs from what the player stored (defaults, locked additions).</summary>
    public ChoiceValue? EffectiveSelection { get; init; }
    /// <summary>For a level-up increment, the whole selection the choice adds to.</summary>
    public ChoiceValue? EffectiveTotalSelection { get; init; }
    /// <summary>Values added by a feature that the player cannot remove.</summary>
    public IReadOnlyList<string> LockedValues { get; init; } = [];
    /// <summary>The build source that owns this choice and the level it came at.</summary>
    public string SourceId { get; init; } = "";
    public int? SourceLevel { get; init; }
    public string FeatInstanceId { get; init; } = "";
    /// <summary>For a level-up increment, the values already chosen and the total count wanted.</summary>
    public IReadOnlyList<string> ExistingValues { get; init; } = [];
    public int? TargetChoose { get; init; }
    /// <summary>A replacement choice (swap one known option for another) and where the replaced value lives.</summary>
    public bool Replacement { get; init; }
    public IReadOnlyList<string> ReplacementPath { get; init; } = [];
    public int? SpellLevel { get; init; }
    public string SpellList { get; init; } = "";
    public IReadOnlyList<string> Abilities { get; init; } = [];
    /// <summary>The feats an "ASI or feat" or feat choice offers, each with its own nested choices.</summary>
    public IReadOnlyList<BuildChoiceOption> FeatOptions { get; init; } = [];
}
public sealed record CreationChoiceReport(IReadOnlyList<BuilderChoiceDefinition> Definitions,IReadOnlyList<BuildIssue> Issues);
