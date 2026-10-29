using System.Text.Json.Nodes;
namespace Herohold.Rules;

/// <summary>A feat as the rules catalog describes it, read once when the catalog loads.</summary>
public sealed record CatalogFeat
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>"general", "origin", "fighting_style" or "epic_boon".</summary>
    public string Category { get; init; } = "";
    public string Source { get; init; } = "";
    public string Description { get; init; } = "";
    public bool Repeatable { get; init; }
    public FeatPrerequisites Prerequisites { get; init; } = new();
    public FeatAbilityIncrease? AbilityIncrease { get; init; }
    public IReadOnlyList<string> AutomaticFeatures { get; init; } = [];
    public IReadOnlyList<string> RequiredChoices { get; init; } = [];
    /// <summary>The spell list a Magic Initiate variant draws from.</summary>
    public string MagicInitiateClass { get; init; } = "";
    /// <summary>The artisan tools a Crafter may choose from.</summary>
    public IReadOnlyList<string> ToolChoices { get; init; } = [];

    /// <summary>Reads one catalog feat record.</summary>
    public static CatalogFeat From(JsonObject f) => new()
    {
        Id = Text(f["id"]), Name = Text(f["name"]), Category = Text(f["category"]), Source = Text(f["source"]), Description = Text(f["description"]),
        Repeatable = f["repeatable"] is JsonValue r && r.TryGetValue<bool>(out var repeatable) && repeatable,
        Prerequisites = new() { MinimumLevel = Integer(f["prerequisites"]?["minimum_level"]), Any = f["prerequisites"]?["any"] is JsonArray groups ? [.. groups.OfType<JsonObject>().Select(Group)] : null },
        AbilityIncrease = f["ability_increase"] is JsonObject increase ? Increase(increase) : null,
        AutomaticFeatures = Strings(f["automatic_features"]), RequiredChoices = f["required_choices"] is JsonArray required ? [.. required.Select(n => n?.ToJsonString() ?? "")] : [],
        MagicInitiateClass = Text(f["magic_initiate_class"]), ToolChoices = Strings(f["tool_choices"])
    };

    private static FeatPrerequisiteGroup Group(JsonObject g) => new()
    {
        MinimumLevel = Integer(g["minimum_level"]) ?? 0, Spellcasting = g["spellcasting"] is JsonValue s && s.TryGetValue<bool>(out var spellcasting) && spellcasting,
        AbilityScores = [.. (g["ability_scores"] as JsonArray ?? []).OfType<JsonObject>().Select(o => (IReadOnlyDictionary<string, int>)o.ToDictionary(p => p.Key, p => Integer(p.Value) ?? 0, StringComparer.Ordinal))],
        Proficiencies = [.. (g["proficiencies"] as JsonArray ?? []).OfType<JsonObject>().Select(p => new FeatProficiencyPrerequisite(Text(p["type"]), Text(p["value"])))],
        Features = Strings(g["features"]), Other = g["other"] is JsonArray other ? [.. other.Select(n => n?.ToJsonString() ?? "")] : []
    };

    private static FeatAbilityIncrease Increase(JsonObject i) => new()
    {
        Mode = Text(i["mode"]), Choose = Integer(i["choose"]), From = Strings(i["from"]), Amount = Integer(i["amount"]), Maximum = Integer(i["maximum"]),
        Modes = [.. (i["modes"] as JsonArray ?? []).OfType<JsonObject>().Select(Increase)]
    };

    private static string Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
    private static int? Integer(JsonNode? n) => n is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;
    private static IReadOnlyList<string> Strings(JsonNode? n) => n is JsonArray a ? [.. a.Select(Text).Where(s => s.Length > 0)] : [];
}

/// <summary>When a feat may be taken: from <see cref="MinimumLevel"/>, and when any one group is met. No groups means never.</summary>
public sealed record FeatPrerequisites
{
    public int? MinimumLevel { get; init; }
    public IReadOnlyList<FeatPrerequisiteGroup>? Any { get; init; }
}

public sealed record FeatPrerequisiteGroup
{
    public int MinimumLevel { get; init; }
    public bool Spellcasting { get; init; }
    /// <summary>Alternatives, each naming the minimum score of one or more abilities.</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, int>> AbilityScores { get; init; } = [];
    public IReadOnlyList<FeatProficiencyPrerequisite> Proficiencies { get; init; } = [];
    public IReadOnlyList<string> Features { get; init; } = [];
    /// <summary>Prerequisites Herohold cannot check; a group with any is never met.</summary>
    public IReadOnlyList<string> Other { get; init; } = [];
}

public sealed record FeatProficiencyPrerequisite(string Type, string Value);

/// <summary>A feat's ability score increase: "choose", "fixed" or "multi_mode" with several <see cref="Modes"/>.</summary>
public sealed record FeatAbilityIncrease
{
    public string Mode { get; init; } = "";
    public int? Choose { get; init; }
    public IReadOnlyList<string> From { get; init; } = [];
    public int? Amount { get; init; }
    public int? Maximum { get; init; }
    public IReadOnlyList<FeatAbilityIncrease> Modes { get; init; } = [];
}
