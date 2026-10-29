using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Web.Services;
namespace Herohold.TestSupport;

/// <summary>Test conveniences: reads builder choices, validation and import reviews written as JSON (fixtures and fake rules answers).</summary>
public static class JsonWorkflowViews
{
    public static BuilderChoice Choice(JsonObject choice)
    {
        var details = choice["details"] as JsonObject ?? new();
        return new()
        {
            Id = Text(choice["id"]), Label = Text(choice["label"]), Section = Text(choice["section"]) is { Length: > 0 } section ? section : "choices",
            Type = Text(choice["type"]), Required = choice["required"] is JsonValue required && required.TryGetValue<bool>(out var must) && must, Choose = Number(choice["choose"]) ?? 1,
            Status = Text(choice["status"]), Path = [.. (choice["path"] as JsonArray ?? []).Select(Text)],
            Options = Options(choice["options"]), Value = Value(choice["value"]),
            Minimum = Number(details["minimum"]), Maximum = Number(details["maximum"]),
            FeatOptions = Options(details["feat_options"] ?? details["feat_candidates"] ?? choice["options"]),
            Children = Choices(details["children"])
        };
    }

    private static List<BuilderChoice> Choices(JsonNode? node) => [.. (node as JsonArray ?? []).OfType<JsonObject>().Select(Choice)];

    private static List<BuilderOption> Options(JsonNode? node) => [.. (node as JsonArray ?? []).OfType<JsonObject>().Select(option => new BuilderOption
    {
        Value = Value(option["value"] ?? option["id"] ?? option["name"]),
        Label = Text(option["label"]) is { Length: > 0 } label ? label : Text(option["name"]),
        Description = Text(option["description"]), Source = Text(option["source"]), Reason = Text(option["reason"]),
        Disabled = option["disabled"] is JsonValue disabled && disabled.TryGetValue<bool>(out var flag) && flag,
        Children = Choices(option["children"] ?? option["details"]?["children"])
    })];

    public static WorkflowValidation? Validation(JsonNode? node) => node is JsonObject validation ? new()
    {
        Complete = validation["complete"] is JsonValue complete && complete.TryGetValue<bool>(out var done) && done,
        Missing = [.. Lines(validation["missing"])],
        Warnings = [.. Lines(validation["warnings"]).Concat(Lines(validation["unsupported"]))]
    } : null;

    public static ImportReview ReadImportReview(JsonObject review) => new()
    {
        CanAdopt = review["canAdopt"] is JsonValue adopt && adopt.TryGetValue<bool>(out var ok) ? ok : null,
        SourceCanAdopt = review["sourceCanAdopt"] is JsonValue source && source.TryGetValue<bool>(out var allowed) && allowed,
        SourceKind = Text(review["sourceKind"]), Message = Text(review["message"]),
        DroppedFields = [.. (review["droppedFields"] as JsonArray ?? []).Select(Text).Where(n => n.Length > 0)],
        Issues = [.. (review["issues"] as JsonArray ?? []).OfType<JsonObject>().Select(i => new ImportIssue
        {
            Code = Text(i["code"]), Severity = Text(i["severity"]), Label = Text(i["label"]), Remedy = Text(i["remedy"]),
            Observed = HasValue(i["observed"]) ? Readable(i["observed"]) : null, Expected = HasValue(i["expected"]) ? Readable(i["expected"]) : null
        })],
        Comparisons = [.. (review["comparisons"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(c => new ImportComparisonView { Label = Text(c["label"]), Observed = Readable(c["observed"]), Calculated = Readable(c["calculated"]), Status = Text(c["status"]) })
            .Where(c => c.Status != "not_observed")],
        Fields = [.. (review["evidence"]?["fields"] as JsonObject ?? review["evidence"]?["observed_fields"] as JsonObject ?? []).Select(p => KeyValuePair.Create(p.Key, Readable(p.Value)))],
        Selections = review["selections"] is JsonObject selections ? Value(selections) : null
    };

    /// <summary>A choice value as the rules send it. Rules values are names, numbers, flags or named parts.</summary>
    public static ChoiceValue Value(JsonNode? node) => node?.Deserialize<ChoiceValue>() ?? new();

    /// <summary>A choice value as JSON. Rules values are names, numbers, flags or named parts.</summary>
    private static IEnumerable<string> Lines(JsonNode? node) => (node as JsonArray ?? []).Select(n => n is JsonValue v && v.TryGetValue<string>(out var text) ? text : Readable(n));
    private static bool HasValue(JsonNode? node) => WorkflowViews.HasValue(node);
    private static string Readable(JsonNode? node) => WorkflowViews.Readable(node);
    private static string Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : node?.ToJsonString() ?? "";
    private static int? Number(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;
}
