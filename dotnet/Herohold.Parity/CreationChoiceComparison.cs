using Herohold.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Rules;

internal static class CreationChoiceComparison
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public static async Task<int> RunAsync(string repositoryRoot, string fixturePath)
    {
        try
        {
            string Read(string key) => File.ReadAllText(Path.Combine(repositoryRoot, "data", "imported", "5etools", key + ".json"));
            var raw = File.ReadAllText(Path.Combine(repositoryRoot, "data", "raw", "5etools", "spells", "spells-xphb.json"));
            var catalog = RulesCatalogSnapshot.Parse(Read("classes"), Read("species"), Read("backgrounds"), Read("feats"), Read("spells"), Read("equipment"), Read("weapons"), raw);
            var classes = ClassProgressionCatalog.Parse(Read("classes"));
            var choices = new ClassChoiceRules(classes, catalog, BeastChoiceCatalog.Parse(Read("beasts")), SpellCapabilityCatalog.Parse(raw));
            var rawSpecies = SpeciesSourceCatalog.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "data", "raw", "5etools", "races.json")));
            var native = new CreationChoiceRules(catalog, classes, choices, RulesReference.Parse(Read("rules")), rawSpecies);
            var document = JsonNode.Parse(await File.ReadAllTextAsync(fixturePath))!.AsObject();
            if (document["schemaVersion"]!.GetValue<int>() != 1) throw new InvalidDataException("Unsupported creation-choice fixture schema.");
            var results = new JsonArray();var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var test in document["fixtures"]!.AsArray().OfType<JsonObject>())
            {
                var id = test["id"]!.GetValue<string>();
                if (!ids.Add(id)) throw new InvalidDataException("Duplicate creation fixture ID.");
                // The fixture carries the character as a rules document; the rules read it as a typed build.
                var fixture = test["context"]!.AsObject();var character = fixture["character"]!.AsObject();
                var context = new CreationChoiceContext
                {
                    Build = BuildFacts.FromCanonical(character), HasInventory = character["folio_inventory"] is not null,
                    ClassContext = fixture["classContext"]?.Deserialize<ClassChoiceContext>(Json) ?? new(),
                    EligibleWeaponMasteries = fixture["eligibleWeaponMasteries"]?.Deserialize<List<string>>(Json) ?? []
                };
                var before = JsonSerializer.Serialize(context, Json);
                var result = JsonSerializer.SerializeToNode(native.Describe(context), Json)!;
                if (!JsonNode.DeepEquals(result, JsonSerializer.SerializeToNode(native.Describe(context), Json))) throw new InvalidDataException(id + ": repeated creation inspection changed output.");
                foreach (var definition in native.Describe(context).Definitions)
                {
                    definition.PreservedSelection?.Items?.Add(ChoiceValue.Of("Mutation probe"));
                }
                if (before != JsonSerializer.Serialize(context, Json)) throw new InvalidDataException(id + ": creation output aliases or mutated input.");
                results.Add(new JsonObject { ["id"] = id, ["result"] = result });
            }
            Console.WriteLine(results.ToJsonString());return 0;
        }
        catch (Exception error) when (error is IOException or JsonException or ArgumentException or InvalidDataException or InvalidOperationException or NotImplementedException)
        {
            Console.Error.WriteLine("Creation-choice comparison failed: " + error.Message);return 1;
        }
    }
}
