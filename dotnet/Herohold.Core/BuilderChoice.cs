namespace Herohold.Core;

/// <summary>One decision a character draft asks for, as the builder shows it.</summary>
public sealed record BuilderChoice
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    /// <summary>The builder step that shows it, such as "class", "abilities" or "origin".</summary>
    public string Section { get; init; } = "choices";
    /// <summary>The editor it needs: single, multi, text, boolean, number, ability_scores, asi_or_feat or feat_selection.</summary>
    public string Type { get; init; } = "single";
    public bool Required { get; init; } = true;
    public int Choose { get; init; } = 1;
    /// <summary>"resolved", "unresolved" or "unsupported".</summary>
    public string Status { get; init; } = "unresolved";
    /// <summary>Where the value goes in the build, as named parts.</summary>
    public List<string> Path { get; init; } = [];
    public List<BuilderOption> Options { get; init; } = [];
    public ChoiceValue Value { get; init; } = new();
    /// <summary>Score bounds for an ability score choice.</summary>
    public int? Minimum { get; init; }
    public int? Maximum { get; init; }
    /// <summary>The feats an "ASI or feat" or feat choice offers.</summary>
    public List<BuilderOption> FeatOptions { get; init; } = [];
    /// <summary>Nested decisions that belong to this one.</summary>
    public List<BuilderChoice> Children { get; init; } = [];
}

public sealed record BuilderOption
{
    public ChoiceValue Value { get; init; } = new();
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public string Source { get; init; } = "";
    public string Reason { get; init; } = "";
    public bool Disabled { get; init; }
    /// <summary>Decisions that come with picking this option, such as a feat's own choices.</summary>
    public List<BuilderChoice> Children { get; init; } = [];
}
