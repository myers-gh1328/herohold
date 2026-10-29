using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace Herohold.Rules;

/// <summary>
/// The 2024 (XPHB) species records of the 5etools source data, read once: the darkvision, damage defenses and
/// spells each species and lineage grants. The normalized species catalog does not carry these.
/// </summary>
public sealed class SpeciesSourceCatalog
{
    private readonly Dictionary<string, SpeciesSource> species;
    private SpeciesSourceCatalog(Dictionary<string, SpeciesSource> species) => this.species = species;
    public static readonly SpeciesSourceCatalog Empty = new(new(StringComparer.OrdinalIgnoreCase));

    /// <summary>A species by name, ignoring case.</summary>
    public SpeciesSource? Find(string name) => species.GetValueOrDefault(name);

    public static SpeciesSourceCatalog Parse(string rawRacesJson)
    {
        if (string.IsNullOrWhiteSpace(rawRacesJson) || Encoding.UTF8.GetByteCount(rawRacesJson) > 32 * 1024 * 1024) throw new ArgumentException("The species spell catalog is empty or too large.");
        JsonObject raw;
        try { raw = JsonNode.Parse(rawRacesJson, documentOptions: new() { MaxDepth = 64 }) as JsonObject ?? throw new ArgumentException("The raw species catalog must be an object."); }
        catch (JsonException error) { throw new ArgumentException("The species spell catalog is malformed.", error); }
        if (raw["race"] is not JsonArray records || records.Count > 50000) throw new ArgumentException("Species spell sources must be a bounded array.");
        var species = new Dictionary<string, SpeciesSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records.OfType<JsonObject>().Where(record => Text(record["source"]) == "XPHB"))
        {
            var entry = SpeciesSource.From(record);
            if (entry.Name.Length is 0 or > 200 || !species.TryAdd(entry.Name, entry)) throw new ArgumentException("Species spell source identities are invalid or duplicated.");
        }
        return new(species);
    }
    internal static string Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
}

/// <summary>A species or one of its lineages as the source data describes it. A lineage leaves unset what it takes from its species.</summary>
public sealed record SpeciesSource
{
    public string Name { get; init; } = "";
    public int? Darkvision { get; init; }
    public IReadOnlyList<string>? Resist { get; init; }
    public IReadOnlyList<string>? Immune { get; init; }
    public IReadOnlyList<string>? ConditionImmune { get; init; }
    public IReadOnlyList<SpeciesSpellAddition>? AdditionalSpells { get; init; }
    public IReadOnlyList<SpeciesSource> Versions { get; init; } = [];

    /// <summary>The lineage whose name contains the chosen lineage, if one was chosen.</summary>
    public SpeciesSource? Version(string lineage) => lineage.Length == 0 ? null : Versions.FirstOrDefault(v => v.Name.Contains(lineage, StringComparison.Ordinal));

    internal static SpeciesSource From(JsonObject r) => new()
    {
        Name = SpeciesSourceCatalog.Text(r["name"]), Darkvision = r["darkvision"] is JsonValue d && d.TryGetValue<int>(out var range) ? range : null,
        Resist = Names(r["resist"]), Immune = Names(r["immune"]), ConditionImmune = Names(r["conditionImmune"]),
        AdditionalSpells = r["additionalSpells"] is JsonArray additions ? [.. additions.OfType<JsonObject>().Select(SpeciesSpellAddition.From)] : null,
        Versions = [.. (r["_versions"] as JsonArray ?? []).OfType<JsonObject>().Select(From)]
    };
    /// <summary>The plain names of a list (a choice inside it names nothing), or null when the record does not set it.</summary>
    private static IReadOnlyList<string>? Names(JsonNode? node) => node switch
    {
        null => null,
        JsonArray list => [.. list.Select(SpeciesSourceCatalog.Text).Where(s => s.Length > 0)],
        _ => SpeciesSourceCatalog.Text(node) is { Length: > 0 } single ? [single] : []
    };
}

/// <summary>Spells a species or lineage grants: known at will or innate with daily uses, from a character level on.</summary>
public sealed record SpeciesSpellAddition
{
    /// <summary>The lineage this addition belongs to, or empty for every lineage.</summary>
    public string Name { get; init; } = "";
    /// <summary>The fixed spellcasting ability ("int", "wis"…), when the addition sets one.</summary>
    public string FixedAbility { get; init; } = "";
    /// <summary>The abilities a player may choose from, when the addition offers a choice instead.</summary>
    public IReadOnlyList<string>? AbilityChoices { get; init; }
    public IReadOnlyList<GatedSpells> Known { get; init; } = [];
    public IReadOnlyList<GatedSpells> Innate { get; init; } = [];

    internal static SpeciesSpellAddition From(JsonObject a) => new()
    {
        Name = SpeciesSourceCatalog.Text(a["name"]), FixedAbility = SpeciesSourceCatalog.Text(a["ability"]),
        AbilityChoices = a["ability"] is JsonObject ability ? [.. (ability["choose"] as JsonArray ?? []).Select(SpeciesSourceCatalog.Text).Where(s => s.Length > 0)] : null,
        Known = Gates(a["known"]), Innate = Gates(a["innate"])
    };
    private static IReadOnlyList<GatedSpells> Gates(JsonNode? node) => [.. (node as JsonObject ?? []).Select(pair => new GatedSpells(pair.Key, References(pair.Value),
        [.. ((pair.Value as JsonObject)?["daily"] as JsonObject ?? []).Select(daily => KeyValuePair.Create(daily.Key, References(daily.Value)))]))];
    /// <summary>Every spell a source entry names, in order; a choice is one reference carrying its filter.</summary>
    private static IReadOnlyList<SpeciesSpellReference> References(JsonNode? node)
    {
        var found = new List<SpeciesSpellReference>();
        void Walk(JsonNode? n)
        {
            if (n is JsonValue value && value.TryGetValue<string>(out var reference)) found.Add(new(reference, null));
            else if (n is JsonArray array) foreach (var item in array) Walk(item);
            else if (n is JsonObject obj)
            {
                if (obj.ContainsKey("choose")) found.Add(new("", SpeciesSourceCatalog.Text(obj["choose"])));
                else foreach (var pair in obj) Walk(pair.Value);
            }
        }
        Walk(node);
        return found;
    }
}
/// <summary>The spells of one level gate ("_" for always, or a character level) and, for innate spells, those with daily uses by count ("1", "1e", "pb").</summary>
public sealed record GatedSpells(string Gate, IReadOnlyList<SpeciesSpellReference> Spells, IReadOnlyList<KeyValuePair<string, IReadOnlyList<SpeciesSpellReference>>> Daily);
/// <summary>A spell reference ("misty step|xphb") or, when <see cref="Choose"/> is set, a choice by filter ("level=0|class=Wizard").</summary>
public sealed record SpeciesSpellReference(string Spell, string? Choose);
