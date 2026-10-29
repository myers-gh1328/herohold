using Herohold.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Rules;

internal static class FeatChoiceComparison
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public static async Task<int> RunAsync(string repositoryRoot, string fixturePath)
    {
        try
        {
            string Read(string key) => File.ReadAllText(Path.Combine(repositoryRoot, "data", "imported", "5etools", key + ".json"));
            var sources = new[] { "classes", "species", "backgrounds", "feats", "spells", "equipment", "weapons" }.ToDictionary(key => key, Read);
            var rawSpells = File.ReadAllText(Path.Combine(repositoryRoot, "data", "raw", "5etools", "spells", "spells-xphb.json"));
            RulesCatalogSnapshot Catalog(string feats) => RulesCatalogSnapshot.Parse(sources["classes"], sources["species"], sources["backgrounds"], feats, sources["spells"], sources["equipment"], sources["weapons"], rawSpells);
            var rules = new FeatSelectionRules(Catalog(sources["feats"]));
            var document = JsonNode.Parse(await File.ReadAllTextAsync(fixturePath))!.AsObject();
            if (document["schemaVersion"]!.GetValue<int>() != 1) throw new InvalidDataException("Unsupported feat-choice fixture schema.");
            var results = new JsonArray();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var test in document["fixtures"]!.AsArray().OfType<JsonObject>())
            {
                var id = test["id"]!.GetValue<string>();
                if (!ids.Add(id)) throw new InvalidDataException("Duplicate feat-choice fixture ID.");
                var native = test["feat"] is JsonObject feat ? new FeatSelectionRules(Catalog(new JsonArray(feat.DeepClone()).ToJsonString())) : rules;
                var featId = test["featId"]!.GetValue<string>();
                var context = test["context"]!.Deserialize<FeatRuleContext>(Json)!;
                var selection = test["selection"] as JsonObject;
                var before = JsonSerializer.Serialize(context, Json);
                var selectionBefore = selection?.ToJsonString();
                JsonObject Evaluate() => new()
                {
                    ["eligibility"] = JsonSerializer.SerializeToNode(native.Evaluate(featId, context), Json),
                    ["fields"] = JsonSerializer.SerializeToNode(native.DescribeChoices(featId, context), Json),
                    ["validation"] = selection is null ? null : JsonSerializer.SerializeToNode(native.ValidateSelection(featId, context,
                        selection["choices"] is JsonObject choices ? choices.Deserialize<Dictionary<string, ChoiceValue>>() : null,
                        selection["ability_increases"] is JsonObject increases ? increases.ToDictionary(p => p.Key, p => p.Value is JsonValue v && v.TryGetValue<int>(out var n) ? n : -1) : null), Json)
                };
                var result = Evaluate();
                if (!JsonNode.DeepEquals(result, Evaluate())) throw new InvalidDataException(id + ": repeated feat evaluation changed output.");
                if (before != JsonSerializer.Serialize(context, Json) || selectionBefore != selection?.ToJsonString()) throw new InvalidDataException(id + ": feat evaluation mutated caller input.");
                results.Add(new JsonObject { ["id"] = id, ["result"] = result });
            }
            Console.WriteLine(results.ToJsonString());
            return 0;
        }
        catch (Exception error) when (error is IOException or JsonException or ArgumentException or InvalidDataException or InvalidOperationException or NotImplementedException)
        {
            Console.Error.WriteLine("Feat-choice comparison failed: " + error.Message);
            return 1;
        }
    }
}
