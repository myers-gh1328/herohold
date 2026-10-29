using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;

namespace Herohold.Web.Services;

/// <summary>A character draft as the builder shows it: create, edit, level-up or import review.</summary>
public sealed record WorkflowView
{
    /// <summary>"create", "edit", "levelup" or "import".</summary>
    public string Kind { get; init; } = "";
    public long SourceRevision { get; init; }
    public DraftSummary Candidate { get; init; } = new();
    public List<BuilderChoice> Choices { get; init; } = [];
    public WorkflowValidation Validation { get; init; } = new();
    /// <summary>The level-up preview's own check, when a preview was made.</summary>
    public WorkflowValidation? PreviewValidation { get; init; }
    public LevelUpSummary? LevelUp { get; init; }
    public ImportReviewView? Review { get; init; }
    /// <summary>Identifies one draft, so the builder can reset its steps when another draft opens.</summary>
    public string Stamp => Kind + ":" + (LevelUp?.TransactionId is { Length: > 0 } transaction ? transaction : SourceRevision.ToString(CultureInfo.InvariantCulture));
}

public sealed record WorkflowValidation
{
    public bool Complete { get; init; }
    public List<string> Missing { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
}

/// <summary>The draft character's headline facts.</summary>
public sealed record DraftSummary
{
    public string Name { get; init; } = "";
    public string Species { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string Subclass { get; init; } = "";
    public int? Level { get; init; }
    public Dictionary<string, int>? ClassLevels { get; init; }
    public int? MaximumHp { get; init; }
    public int? CurrentHp { get; init; }
    public string AbilityMethod { get; init; } = "Manual entry";
    public List<int> RolledScores { get; init; } = [];
    /// <summary>Which rolled or standard-array score each ability takes, by position.</summary>
    public Dictionary<Ability, int> Assignments { get; init; } = [];
}

public sealed record LevelUpSummary
{
    public string TransactionId { get; init; } = "";
    public string ClassName { get; init; } = "";
    public int? TargetLevel { get; init; }
    public int? HitDie { get; init; }
    public int? HpBaseGain { get; init; }
    public int? HpIncrease { get; init; }
    public List<string> AutomaticFeatures { get; init; } = [];
}

/// <summary>How an uploaded character compares with what Herohold recalculated. Values are shown as text.</summary>
public sealed record ImportReviewView
{
    /// <summary>Whether the review allows adoption; unset when the review states nothing either way.</summary>
    public bool? CanAdopt { get; init; }
    public string SourceKind { get; init; } = "";
    public List<string> DroppedFields { get; init; } = [];
    public List<ImportIssueView> Issues { get; init; } = [];
    public List<ImportComparisonView> Comparisons { get; init; } = [];
    /// <summary>The original fields recovered from an uploaded PDF.</summary>
    public List<KeyValuePair<string, string>> Fields { get; init; } = [];
}

public sealed record ImportIssueView
{
    public string Severity { get; init; } = "";
    public string Label { get; init; } = "";
    public string? Observed { get; init; }
    public string? Expected { get; init; }
    public string Remedy { get; init; } = "";
    public bool Blocks => Severity is "blocker" or "error";
}

public sealed record ImportComparisonView
{
    public string Label { get; init; } = "";
    public string Observed { get; init; } = "";
    public string Calculated { get; init; } = "";
    /// <summary>"match", "conflict" or "not_observed".</summary>
    public string Status { get; init; } = "";
}

/// <summary>A level-up's hit point policy: the fixed gain or a physical Hit Die roll.</summary>
public sealed record LevelUpHp
{
    /// <summary>"fixed" or "rolled".</summary>
    public string Mode { get; init; } = "fixed";
    public int? Roll { get; init; }
    public bool AddGainedHp { get; init; }
}

/// <summary>Item and weapon names the inventory editor offers.</summary>
public sealed record ItemCatalog(List<string> Equipment, List<string> Weapons);

/// <summary>The builder's view of a draft, and how a review shows values that arrive as JSON from a PDF.</summary>
public static class WorkflowViews
{
    public static WorkflowView From(string kind, long sourceRevision, DraftResult draft) => new()
    {
        Kind = kind, SourceRevision = sourceRevision, Candidate = draft.Summary, Choices = draft.Choices, Validation = draft.Validation,
        Review = draft.Review is { } review ? View(review) : null
    };

    public static WorkflowView From(long sourceRevision, LevelUpResult levelUp) => new()
    {
        Kind = "levelup", SourceRevision = sourceRevision, Candidate = levelUp.Candidate, Choices = levelUp.PendingChoices,
        Validation = levelUp.Validation, PreviewValidation = levelUp.PreviewValidation,
        LevelUp = new()
        {
            TransactionId = levelUp.Review.Draft.TransactionId, ClassName = levelUp.Review.Draft.ClassName, TargetLevel = levelUp.Review.Draft.TargetLevel,
            HitDie = levelUp.Review.HitDie, HpBaseGain = levelUp.Review.HpBaseGain, HpIncrease = levelUp.Review.HpIncrease, AutomaticFeatures = [.. levelUp.Review.AutomaticFeatures]
        }
    };

    private static ImportReviewView View(ImportReview review) => new()
    {
        CanAdopt = review.CanAdopt, SourceKind = review.SourceKind, DroppedFields = review.DroppedFields, Comparisons = review.Comparisons, Fields = review.Fields,
        Issues = [.. review.Issues.Select(i => new ImportIssueView { Severity = i.Severity, Label = i.Label, Observed = i.Observed, Expected = i.Expected, Remedy = i.Remedy }).DistinctBy(i => (i.Severity, i.Label))]
    };

    internal static bool HasValue(JsonNode? node) => node is JsonArray array ? array.Count > 0 : node is JsonObject record ? record.Count > 0 : Text(node).Length > 0;
    internal static string Readable(JsonNode? node) => node switch
    {
        null => "—",
        JsonArray array => string.Join(", ", array.Select(Readable)),
        JsonObject record => string.Join(" · ", record.Select(p => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(p.Key.Replace('_', ' ')) + ": " + Readable(p.Value))),
        _ => Text(node) is { Length: > 0 } text ? text : "—"
    };
    private static string Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : node?.ToJsonString() ?? "";
}
