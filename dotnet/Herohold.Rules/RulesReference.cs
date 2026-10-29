using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;
namespace Herohold.Rules;

/// <summary>The general rules tables (rules.json): the skills and their abilities, the Origin feats, and feature descriptions.</summary>
public sealed record RulesReference
{
    public static readonly RulesReference Empty = new();
    /// <summary>Each skill with the ability it uses, in the table's order.</summary>
    public IReadOnlyList<KeyValuePair<string, Ability>> Skills { get; init; } = [];
    public IReadOnlyList<string> OriginFeats { get; init; } = [];
    public IReadOnlyDictionary<string, string> FeatureDescriptions { get; init; } = new Dictionary<string, string>();

    public static RulesReference Parse(string rulesJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rulesJson);
        JsonObject rules;
        try { rules = JsonNode.Parse(rulesJson, documentOptions: new() { MaxDepth = 64 }) as JsonObject ?? throw new ArgumentException("The rules tables must be an object."); }
        catch (JsonException error) { throw new ArgumentException("The rules tables are malformed.", error); }
        var skills = rules["skills"] is null ? new JsonObject() : rules["skills"] as JsonObject ?? throw new ArgumentException("The skills table must be an object.");
        return new()
        {
            Skills = [.. skills.Select(p => KeyValuePair.Create(p.Key, Enum.TryParse<Ability>(CatalogText.Text(p.Value), out var ability) && Enum.IsDefined(ability) ? ability : throw new ArgumentException("A skill names an unknown ability.")))],
            OriginFeats = CatalogText.Strings(rules["origin_feats"]), FeatureDescriptions = CatalogText.Map(rules["feature_descriptions"])
        };
    }
}
