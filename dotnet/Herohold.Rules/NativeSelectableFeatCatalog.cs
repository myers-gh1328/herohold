using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace Herohold.Rules;

/// <summary>Additional 2024 Origin and Fighting Style feats; the public normalized catalog stays unchanged.</summary>
public sealed class NativeSelectableFeatCatalog
{
    private NativeSelectableFeatCatalog(IReadOnlyList<CatalogFeat> definitions) => AdditionalDefinitions = definitions;
    public IReadOnlyList<CatalogFeat> AdditionalDefinitions { get; }
    private static string Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
    public static NativeSelectableFeatCatalog Parse(string rawFeatsJson)
    {
        if (string.IsNullOrWhiteSpace(rawFeatsJson) || Encoding.UTF8.GetByteCount(rawFeatsJson) > 16 * 1024 * 1024) throw new ArgumentException("The feat source exceeds its supported bounds.");
        JsonObject root;
        try { root = JsonNode.Parse(rawFeatsJson, documentOptions: new() { MaxDepth = 64 }) as JsonObject ?? throw new ArgumentException("The feat source must be an object."); }
        catch (JsonException e) { throw new ArgumentException("The feat source contains invalid JSON.", e); }
        if (root["feat"] is not JsonArray rows || rows.Count > 10000) throw new ArgumentException("The feat source must contain a bounded feat list.");
        var result = new List<JsonObject>(); var ids = new HashSet<string>();
        foreach (var row in rows.OfType<JsonObject>().Where(r => Text(r["source"]) == "XPHB" && Text(r["category"]) is "O" or "FS"))
        {
            var name = Text(row["name"]); if (string.IsNullOrWhiteSpace(name) || name.Length > 200) throw new ArgumentException("Selectable feats need bounded names.");
            var style = Text(row["category"]) == "FS";
            if (row["prerequisite"] is JsonNode prereq && (!style || prereq is not JsonArray groups || groups.Count != 1 || groups[0] is not JsonObject group || group.Count != 1 || group["feature"] is not JsonArray features || features.Count != 1 || Text(features[0]) != "Fighting Style"))
                throw new ArgumentException("A selectable feat contains unsupported prerequisites.");
            if (style && row["prerequisite"] is null) throw new ArgumentException("A fighting style must retain its feature prerequisite.");
            foreach (var spellClass in name == "Magic Initiate" ? new[] { "Cleric", "Druid", "Wizard" } : new[] { "" })
            {
                var display = spellClass.Length > 0 ? $"Magic Initiate ({spellClass})" : name;
                var id = Regex.Replace(display.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_') + "_xphb";
                if (!ids.Add(id)) throw new ArgumentException("Selectable feat identities must be unique.");
                var definition = new JsonObject
                {
                    ["id"] = id, ["name"] = display, ["source"] = "XPHB", ["category"] = style ? "fighting_style" : "origin",
                    ["repeatable"] = name == "Skilled", ["raw_ref"] = name + "|XPHB",
                    ["prerequisites"] = new JsonObject { ["minimum_level"] = 1, ["any"] = new JsonArray(new JsonObject { ["minimum_level"] = 1, ["features"] = style ? new JsonArray("Fighting Style") : new JsonArray() }) },
                    ["automatic_features"] = new JsonArray(display), ["required_choices"] = new JsonArray(), ["unsupported_choices"] = new JsonArray(),
                    ["description"] = string.Join("\n", EntryText(row["entries"]))
                };
                if (spellClass.Length > 0) definition["magic_initiate_class"] = spellClass;
                if (name == "Crafter") definition["tool_choices"] = row["toolProficiencies"]?[0]?["choose"]?["from"]?.DeepClone();
                result.Add(definition);
            }
        }
        // Reuse the catalog's single-pass tag cleanup; source and source ordering remain stable.
        var clean = RulesCatalogSnapshot.Parse("[]", "[]", "[]", new JsonArray(result.Select(d => (JsonNode)d).ToArray()).ToJsonString(), "[]", "[]", "[]", "{}").FeatRecords;
        return new(clean);
    }
    private static IEnumerable<string> EntryText(JsonNode? node)
    {
        if (node is JsonValue v && v.TryGetValue<string>(out var text)) yield return text;
        else if (node is JsonArray array) foreach (var item in array) foreach (var value in EntryText(item)) yield return value;
        else if (node is JsonObject record)
        {
            if (record["name"] is JsonValue title) yield return Text(title);
            foreach (var key in new[] { "entries", "items", "rows" }) foreach (var value in EntryText(record[key])) yield return value;
        }
    }
}
