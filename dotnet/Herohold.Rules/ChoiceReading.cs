using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;
namespace Herohold.Rules;

/// <summary>Reads the player's catalog-defined choices from a typed build.</summary>
public static class ChoiceReading
{
    public static ChoiceValue? Get(this IReadOnlyDictionary<string, ChoiceValue>? choices, string key) => choices is not null && choices.TryGetValue(key, out var value) && !value.IsNull ? value : null;
    public static string TextOf(ChoiceValue? value) => value?.Text ?? "";
    public static int? NumberOf(ChoiceValue? value) => value?.Number;
    /// <summary>A list of names, or a single name. With <paramref name="strict"/>, any other shape is an error, as the build catalog requires.</summary>
    public static IEnumerable<string> Names(ChoiceValue? value, bool strict = false)
    {
        if (value is null || value.IsNull) return [];
        if (value.Text is { } single) return single.Length > 0 ? [single] : [];
        if (value.Items is { } items)
            return items.Select(item => item.Text ?? (strict ? throw new ArgumentException("A source identity must be text.") : "")).Where(name => name.Length > 0).ToArray();
        return strict ? throw new ArgumentException("A build source must be an array.") : [];
    }
    /// <summary>A value named by a rules catalog record, read when the catalog offers it as an option.</summary>
    public static ChoiceValue FromCatalog(JsonNode? node) => node?.Deserialize<ChoiceValue>() ?? new();
    /// <summary>The choices as a JSON object, for rules that still take JSON choice payloads.</summary>
    public static JsonObject Json(IReadOnlyDictionary<string, ChoiceValue>? choices) => choices is null ? new() : JsonSerializer.SerializeToNode(ChoiceValue.Record(choices))!.AsObject();
}
