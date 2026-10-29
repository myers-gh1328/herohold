using Herohold.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Rules;

internal static class ClassChoiceComparison
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public static async Task<int> RunAsync(string repositoryRoot, string fixturePath)
    {
        try
        {
            string Read(string key) => File.ReadAllText(Path.Combine(repositoryRoot, "data", "imported", "5etools", key + ".json"));
            var rawSpells = File.ReadAllText(Path.Combine(repositoryRoot, "data", "raw", "5etools", "spells", "spells-xphb.json"));
            var native = new ClassChoiceRules(ClassProgressionCatalog.Parse(Read("classes")),
                RulesCatalogSnapshot.Parse(Read("classes"), Read("species"), Read("backgrounds"), Read("feats"), Read("spells"), Read("equipment"), Read("weapons"), rawSpells),
                BeastChoiceCatalog.Parse(Read("beasts")), SpellCapabilityCatalog.Parse(rawSpells));
            var document = JsonNode.Parse(await File.ReadAllTextAsync(fixturePath))!.AsObject();
            if (document["schemaVersion"]!.GetValue<int>() != 1) throw new InvalidDataException("Unsupported class-choice fixture schema.");
            var output = new JsonArray();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var test in document["fixtures"]!.AsArray().OfType<JsonObject>())
            {
                var id = test["id"]!.GetValue<string>();
                if (!ids.Add(id)) throw new InvalidDataException("Duplicate class-choice fixture ID.");
                var context = test["context"]!.Deserialize<ClassChoiceContext>(Json)!;
                var before = JsonSerializer.Serialize(context, Json);
                var level = test["targetLevel"]!.GetValue<int>();
                ClassChoiceReport Evaluate() => test["operation"]!.GetValue<string>() switch
                {
                    "class.describe" => native.Describe(context, level),
                    "class.levelUp" => native.ForLevelUp(context, test["currentLevel"]!.GetValue<int>(), level),
                    _ => throw new InvalidDataException("Unknown fixed class-choice operation.")
                };
                var result = JsonSerializer.SerializeToNode(Evaluate(), Json)!;
                if (!JsonNode.DeepEquals(result, JsonSerializer.SerializeToNode(Evaluate(), Json))) throw new InvalidDataException(id + ": repeated evaluation changed output.");
                if (before != JsonSerializer.Serialize(context, Json)) throw new InvalidDataException(id + ": calculation mutated input.");
                // Returned mutable selections must be detached from caller-owned JSON.
                foreach (var definition in Evaluate().Definitions)
                    definition.PreservedSelection?.Items?.Add(ChoiceValue.Of("Mutation probe"));
                if (before != JsonSerializer.Serialize(context, Json)) throw new InvalidDataException(id + ": returned selections alias input.");
                output.Add(new JsonObject { ["id"] = id, ["result"] = result });
            }
            Console.WriteLine(output.ToJsonString());
            return 0;
        }
        catch (Exception error) when (error is IOException or JsonException or ArgumentException or InvalidDataException or InvalidOperationException or NotImplementedException)
        {
            Console.Error.WriteLine("Class-choice comparison failed: " + error.Message);
            return 1;
        }
    }
}
