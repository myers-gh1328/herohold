using System.Text.Json.Nodes;
namespace Herohold.Rules;

/// <summary>A background as the rules catalog describes it, read once when the catalog loads.</summary>
public sealed record CatalogBackground
{
    public string Name { get; init; } = "";
    public string Source { get; init; } = "";
    public BackgroundAbilityOptions AbilityOptions { get; init; } = new();
    public IReadOnlyList<string> OriginFeats { get; init; } = [];
    public IReadOnlyList<string> SkillProficiencies { get; init; } = [];
    public IReadOnlyList<string> ToolProficiencies { get; init; } = [];
    public CatalogPick ToolChoices { get; init; } = new(0, []);
    public IReadOnlyList<string> Languages { get; init; } = [];
    public string Feature { get; init; } = "";
    public IReadOnlyDictionary<string, string> FeatureDescriptions { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> Equipment { get; init; } = [];
    public IReadOnlyList<EquipmentPackage> EquipmentPackages { get; init; } = [];

    public static CatalogBackground From(JsonObject b) => new()
    {
        Name = CatalogText.Text(b["name"]), Source = CatalogText.Text(b["source"]),
        AbilityOptions = b["ability_options"] is JsonObject a ? new() { Mode = CatalogText.Text(a["mode"]), Abilities = CatalogText.Strings(a["abilities"]), PrimaryBonus = CatalogText.Integer(a["primary_bonus"]), SecondaryBonus = CatalogText.Integer(a["secondary_bonus"]) } : new(),
        OriginFeats = CatalogText.Strings(b["origin_feats"]), SkillProficiencies = CatalogText.Strings(b["skill_proficiencies"]), ToolProficiencies = CatalogText.Strings(b["tool_proficiencies"]),
        ToolChoices = CatalogText.Pick(b["tool_choices"]), Languages = CatalogText.Strings(b["languages"]), Feature = CatalogText.Text(b["feature"]),
        FeatureDescriptions = CatalogText.Map(b["feature_descriptions"]), Equipment = CatalogText.Strings(b["equipment"]),
        EquipmentPackages = [.. (b["equipment_packages"] as JsonArray ?? []).OfType<JsonObject>().Select(p => new EquipmentPackage(CatalogText.Text(p["name"]), CatalogText.Strings(p["items"])))]
    };
}

/// <summary>How a background raises ability scores; "choose_weighted" gives one ability the primary bonus and another the secondary.</summary>
public sealed record BackgroundAbilityOptions
{
    public string Mode { get; init; } = "";
    public IReadOnlyList<string> Abilities { get; init; } = [];
    public int? PrimaryBonus { get; init; }
    public int? SecondaryBonus { get; init; }
}

/// <summary>A species as the rules catalog describes it, read once when the catalog loads.</summary>
public sealed record CatalogSpecies
{
    public string Name { get; init; } = "";
    public string Source { get; init; } = "";
    public string Size { get; init; } = "";
    public int Speed { get; init; } = 30;
    public IReadOnlyList<string> Languages { get; init; } = [];
    public CatalogPick SkillChoices { get; init; } = new(0, []);
    /// <summary>A named choice such as Draconic Ancestry, stored under its key in the species options.</summary>
    public SpeciesChoice? ChoiceOptions { get; init; }
    public IReadOnlyList<SpeciesLineage> LineageOptions { get; init; } = [];
    public IReadOnlyList<string> Traits { get; init; } = [];
    public IReadOnlyDictionary<string, string> TraitDescriptions { get; init; } = new Dictionary<string, string>();

    public static CatalogSpecies From(JsonObject s) => new()
    {
        Name = CatalogText.Text(s["name"]), Source = CatalogText.Text(s["source"]), Size = CatalogText.Text(s["size"]), Speed = CatalogText.Integer(s["speed"]) ?? 30,
        Languages = CatalogText.Strings(s["languages"]), SkillChoices = CatalogText.Pick(s["skill_choices"]),
        ChoiceOptions = s["choice_options"] is JsonObject c ? new(CatalogText.Text(c["name"]), CatalogText.Text(c["key"]), CatalogText.Strings(c["options"])) : null,
        LineageOptions = [.. (s["lineage_options"] as JsonArray ?? []).OfType<JsonObject>().Select(l => new SpeciesLineage(CatalogText.Text(l["name"]), CatalogText.Strings(l["level_1_spells"]), CatalogText.Strings(l["spellcasting_abilities"])))],
        Traits = CatalogText.Strings(s["traits"]), TraitDescriptions = CatalogText.Map(s["trait_descriptions"])
    };
}

public sealed record CatalogPick(int Choose, IReadOnlyList<string> From);
public sealed record EquipmentPackage(string Name, IReadOnlyList<string> Items);
public sealed record SpeciesChoice(string Name, string Key, IReadOnlyList<string> Options);
public sealed record SpeciesLineage(string Name, IReadOnlyList<string> Level1Spells, IReadOnlyList<string> SpellcastingAbilities);

/// <summary>Strict readers for catalog values: a value of the wrong kind is a catalog error, not a missing value.</summary>
internal static class CatalogText
{
    public static string Text(JsonNode? n) => n is null ? "" : n is JsonValue v && v.TryGetValue<string>(out var s) ? s : throw new ArgumentException("A catalog value must be text.");
    public static int? Integer(JsonNode? n) => n is null ? null : n is JsonValue v && v.TryGetValue<int>(out var i) ? i : throw new ArgumentException("A catalog value must be a whole number.");
    public static IReadOnlyList<string> Strings(JsonNode? n) => n is null ? [] : n is JsonValue ? [Text(n)] : n is JsonArray a ? [.. a.Select(Text).Where(s => s.Length > 0)] : throw new ArgumentException("A catalog list must be an array.");
    public static CatalogPick Pick(JsonNode? n) => n is JsonObject o ? new(Integer(o["choose"]) ?? 0, Strings(o["from"])) : new(0, []);
    public static IReadOnlyDictionary<string, string> Map(JsonNode? n) => n is JsonObject o ? o.Where(p => p.Value is JsonValue).ToDictionary(p => p.Key, p => Text(p.Value), StringComparer.Ordinal) : new Dictionary<string, string>();
    public static bool Flag(JsonNode? n) => n is not null && (n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : throw new ArgumentException("A catalog flag must be true or false."));
    /// <summary>The objects of a catalog list; anything else in it is a catalog error.</summary>
    public static IEnumerable<JsonObject> Records(JsonNode? n, string error) => n is null ? [] : n is JsonArray a ? a.Select(r => r as JsonObject ?? throw new ArgumentException(error)) : throw new ArgumentException(error);
    /// <summary>A table keyed by class level (1–20).</summary>
    public static IReadOnlyDictionary<int, T> Levels<T>(JsonNode? n, Func<JsonNode?, T> read)
    {
        if (n is null) return new Dictionary<int, T>();
        if (n is not JsonObject o) throw new ArgumentException("A class level table must be an object.");
        var table = new SortedDictionary<int, T>();
        foreach (var pair in o)
            if (!int.TryParse(pair.Key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var level) || level is < 1 or > 20 || !table.TryAdd(level, read(pair.Value)))
                throw new ArgumentException("A class threshold table has invalid or duplicate levels.");
        return table;
    }
    /// <summary>A spell slot table: slot level (1–9) to count.</summary>
    public static IReadOnlyDictionary<int, int> Slots(JsonNode? n)
    {
        if (n is null) return new Dictionary<int, int>();
        if (n is not JsonObject o) throw new ArgumentException("A spell slot table must be an object.");
        var slots = new SortedDictionary<int, int>();
        foreach (var pair in o)
            if (!int.TryParse(pair.Key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var slot) || slot is < 1 or > 9 || Integer(pair.Value) is not (>= 0 and <= 100000) || !slots.TryAdd(slot, Integer(pair.Value)!.Value))
                throw new ArgumentException("The class spell-slot table has invalid levels or counts.");
        return slots;
    }
}
