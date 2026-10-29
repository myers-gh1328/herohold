using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Rules;

internal static class CatalogComparison
{
    private static readonly string[] Keys = ["classes", "species", "backgrounds", "feats", "spells", "equipment", "weapons"];
    public static async Task<int> RunAsync(string repositoryRoot, string fixturePath)
    {
        try
        {
            var fixture = JsonNode.Parse(await File.ReadAllTextAsync(fixturePath))!.AsObject();
            if (fixture["schemaVersion"]!.GetValue<int>() != 1) throw new InvalidDataException("Unsupported catalog fixture schema.");
            var fixedInputs = new JsonObject();
            foreach (var key in Keys) fixedInputs[key] = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(repositoryRoot, "data", "imported", "5etools", key + ".json")));
            fixedInputs["rawSpells"] = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(repositoryRoot, "data", "raw", "5etools", "spells", "spells-xphb.json")));
            var output = new JsonObject { ["fixedSnapshot"] = ParseAndCheck(fixedInputs).ToEnvelope(), ["syntheticCases"] = new JsonArray() };
            foreach (var test in fixture["syntheticCases"]!.AsArray())
            {
                JsonObject result;
                try
                {
                    result = ParseAndCheck(test!["inputs"]!.AsObject()).ToEnvelope();
                }
                catch (Exception error) when ((error is ArgumentException or JsonException or InvalidOperationException) && test!["reject"]?.GetValue<bool>() == true)
                {
                    output["syntheticCases"]!.AsArray().Add(new JsonObject { ["id"] = test["id"]!.DeepClone(), ["result"] = new JsonObject { ["rejected"] = true } });
                    continue;
                }
                if (test!["reject"]?.GetValue<bool>() == true) throw new InvalidDataException("Expected catalog rejection: " + test["id"]);
                output["syntheticCases"]!.AsArray().Add(new JsonObject { ["id"] = test["id"]!.DeepClone(), ["result"] = result });
            }
            Console.WriteLine(output.ToJsonString());
            return 0;
        }
        catch (Exception error) when (error is IOException or JsonException or ArgumentException or InvalidDataException or InvalidOperationException or NotImplementedException)
        {
            Console.Error.WriteLine("Catalog comparison failed: " + error.Message);
            return 1;
        }
    }

    private static RulesCatalogSnapshot ParseAndCheck(JsonObject inputs)
    {
        var before = inputs.ToJsonString();
        string Source(string key) => inputs[key]!.ToJsonString();
        var snapshot = RulesCatalogSnapshot.Parse(Source("classes"), Source("species"), Source("backgrounds"), Source("feats"), Source("spells"), Source("equipment"), Source("weapons"), Source("rawSpells"));
        var baseline = snapshot.ToEnvelope();
        var returned = snapshot.ToEnvelope();
        returned["catalogs"]!["equipment"]!.AsArray().Add("Mutation probe");
        if (snapshot.Equipment is IList<string> equipment && equipment.Count > 0)
        {
            try { equipment[0] = "Mutation probe"; } catch (NotSupportedException) { }
        }
        if (!JsonNode.DeepEquals(baseline, snapshot.ToEnvelope())) throw new InvalidDataException("A catalog view mutated the private snapshot.");
        if (inputs.ToJsonString() != before) throw new InvalidDataException("Catalog parsing mutated source inputs.");
        return snapshot;
    }
}
