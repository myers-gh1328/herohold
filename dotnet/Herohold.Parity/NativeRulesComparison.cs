using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Rules;

internal static class NativeRulesComparison
{
    public static async Task<int> RunAsync(string fixturePath, string catalogPath)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        try
        {
            var fixtures = JsonNode.Parse(await File.ReadAllTextAsync(fixturePath))!.AsObject();
            if (fixtures["schemaVersion"]!.GetValue<int>() != 1) throw new InvalidDataException("Unsupported rules fixture schema.");
            var catalog = ClassProgressionCatalog.Parse(await File.ReadAllTextAsync(catalogPath));
            var results = new JsonObject { ["hitPoints"] = new JsonArray(), ["classProgression"] = new JsonArray() };
            foreach (var fixture in fixtures["hitPoints"]!.AsArray())
            {
                var input = fixture!["input"]!.Deserialize<HitPointInput>(options)!;
                var before = JsonSerializer.Serialize(input, options);
                if (fixture["reject"]?.GetValue<bool>() == true)
                {
                    try { _ = HitPointRules.Calculate(input); }
                    catch (ArgumentException)
                    {
                        if (before != JsonSerializer.Serialize(input, options)) throw new InvalidDataException("Rejected hit point calculation mutated input.");
                        results["hitPoints"]!.AsArray().Add(new JsonObject { ["id"] = fixture["id"]!.DeepClone(), ["result"] = new JsonObject { ["rejected"] = true } });
                        continue;
                    }
                    throw new InvalidDataException("Expected hit point input rejection: " + fixture["id"]);
                }
                var summary = HitPointRules.Calculate(input);
                if (before != JsonSerializer.Serialize(input, options)) throw new InvalidDataException("Hit point calculation mutated input.");
                results["hitPoints"]!.AsArray().Add(new JsonObject { ["id"] = fixture["id"]!.DeepClone(), ["result"] = JsonSerializer.SerializeToNode(summary, options) });
            }
            foreach (var fixture in fixtures["classProgression"]!.AsArray())
            {
                var input = fixture!["input"]!;
                var snapshot = ClassProgressionRules.AtLevel(catalog, input["className"]!.GetValue<string>(), input["level"]!.GetValue<int>(), input["subclassName"]?.GetValue<string>() ?? "");
                results["classProgression"]!.AsArray().Add(new JsonObject { ["id"] = fixture["id"]!.DeepClone(), ["result"] = Progression(snapshot, options) });
            }
            Console.WriteLine(results.ToJsonString(options));
            return 0;
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidDataException or ArgumentException or NotImplementedException)
        {
            Console.Error.WriteLine($"Native rules comparison failed: {error.Message}");
            return 1;
        }
    }

    /// <summary>A progression snapshot in the reference's shape: catalog tables as the catalog writes them.</summary>
    private static JsonNode Progression(ClassProgressionSnapshot snapshot, JsonSerializerOptions options)
    {
        var result = JsonSerializer.SerializeToNode(snapshot, options)!.AsObject();
        result["choicesAtLevel"] = new JsonArray([.. snapshot.ChoicesAtLevel.Select(f =>
        {
            var feature = new JsonObject { ["id"] = f.Id, ["type"] = f.Type, ["name"] = f.Name, ["source"] = f.Source, ["level"] = f.Level };
            if (f.Type != "automatic_feature") feature["required"] = f.Required;
            if (f.Type == "subclass_choice") feature["options"] = new JsonArray([.. f.Options.Select(o => (JsonNode?)o)]);
            return (JsonNode?)feature;
        })]);
        result["resources"] = Table(snapshot.Resources);
        var grants = result["subclassGrants"]!.AsObject();
        grants["resources"] = Table(snapshot.SubclassGrants.Resources);
        grants["spellGrants"] = new JsonArray([.. snapshot.SubclassGrants.SpellGrants.Select(g => (JsonNode?)new JsonObject
        {
            ["name"] = g.Name, ["level_gained"] = g.LevelGained, ["source"] = new JsonObject { ["type"] = g.SourceType, ["subclass_id"] = g.SubclassId, ["subclass_name"] = g.SubclassName, ["source"] = g.SourceBook },
            ["spellcasting_ability"] = g.SpellcastingAbility, ["grant_type"] = g.GrantType, ["always_prepared"] = g.AlwaysPrepared, ["counts_against_limit"] = g.CountsAgainstLimit
        })]);
        return result;
    }
    private static JsonObject Table(ClassResources resources)
    {
        var table = new JsonObject();
        foreach (var pair in resources.Counts) table[pair.Key] = pair.Value;
        foreach (var pair in resources.Dice) table[pair.Key] = pair.Value;
        if (resources.SpellSlots.Count > 0) table["spell_slots"] = new JsonObject(resources.SpellSlots.Select(p => KeyValuePair.Create(p.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), (JsonNode?)p.Value)));
        return table;
    }
}
