using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Core;
using Herohold.Rules;

internal static class DerivationComparison
{
    public static async Task<int> RunAsync(string fixturePath)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        try
        {
            var fixtures = JsonNode.Parse(await File.ReadAllTextAsync(fixturePath))!.AsObject();
            if (fixtures["schemaVersion"]!.GetValue<int>() != 1) throw new InvalidDataException("Unsupported derivation fixture schema.");
            var output = new JsonObject();
            output["abilityScores"] = Run<AbilityScoreInput>("abilityScores", input =>
            {
                var summary = AbilityScoreRules.Calculate(input);
                foreach (var calculation in summary.Calculations.Values) Check(calculation);
                return new { summary.Scores, summary.BackgroundBonuses, summary.SourceBonuses, summary.Maximums };
            });
            output["standing"] = Run<StandingInput>("standing", input =>
            {
                var summary = StandingRules.Calculate(input);
                return new { Modifiers = summary.Modifiers.ToDictionary(p => p.Key, p => Check(p.Value)), Saves = summary.Saves.ToDictionary(p => p.Key, p => Check(p.Value)),
                    Skills = summary.Skills.ToDictionary(p => p.Key, p => Check(p.Value)), Initiative = Check(summary.Initiative),
                    summary.SaveProficiencies, summary.SkillProficiencies, summary.Expertise, summary.UnmodeledStandingFeats };
            });
            if (fixtures["dependentResources"] is JsonArray)
            {
                var catalog = ClassProgressionCatalog.Parse(await File.ReadAllTextAsync("data/imported/5etools/classes.json"));
                output["dependentResources"] = Run<DependentInput>("dependentResources", input =>
                {
                    var summary = DependentResourceRules.Calculate(catalog, input.ClassName, input.Subclass, input.Level, input.Modifiers, input.ManeuverSaveAbility, input.Feats);
                    return new { summary.Values, Pools = summary.Pools.ToDictionary(pool => pool.Id, pool => new { pool.Maximum, pool.Recovery }) };
                });
            }
            Console.WriteLine(output.ToJsonString(options));
            return 0;

            JsonArray Run<T>(string category, Func<T, object> calculate)
            {
                var results = new JsonArray();
                foreach (var fixture in fixtures[category]!.AsArray())
                {
                    JsonNode? result = null;
                    var rejected = false;
                    T? input = default;
                    string? before = null;
                    try
                    {
                        input = fixture!["input"]!.Deserialize<T>(options)!;
                        before = JsonSerializer.Serialize(input, options);
                        result = JsonSerializer.SerializeToNode(calculate(input), options);
                    }
                    catch (Exception error) when (error is ArgumentException or JsonException)
                    {
                        if (fixture!["reject"]?.GetValue<bool>() != true) throw;
                        rejected = true;
                        result = new JsonObject { ["rejected"] = true };
                    }
                    if (before is not null && before != JsonSerializer.Serialize(input, options)) throw new InvalidDataException("Derivation mutated input: " + fixture!["id"]);
                    if (!rejected && fixture!["reject"]?.GetValue<bool>() == true) throw new InvalidDataException("Expected derivation rejection: " + fixture["id"]);
                    if (!rejected && result!.ToJsonString() != JsonSerializer.SerializeToNode(calculate(input!), options)!.ToJsonString()) throw new InvalidDataException("Repeated derivation changed results.");
                    results.Add(new JsonObject { ["id"] = fixture!["id"]!.DeepClone(), ["result"] = result });
                }
                return results;
            }
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidDataException or ArgumentException or NotImplementedException)
        {
            Console.Error.WriteLine("Native derivation comparison failed: " + error.Message);
            return 1;
        }
    }

    private static int Check(Calculation calculation)
    {
        if (calculation.Terms.Sum(term => term.Value) != calculation.Total || calculation.Terms.Any(term => string.IsNullOrWhiteSpace(term.Label))) throw new InvalidDataException("A calculation breakdown does not explain its total.");
        return calculation.Total;
    }

    private sealed record DependentInput
    {
        public string ClassName { get; init; } = "";
        public string Subclass { get; init; } = "";
        public int Level { get; init; } = 1;
        public Dictionary<Ability, int> Modifiers { get; init; } = [];
        public Ability? ManeuverSaveAbility { get; init; }
        public List<string> Feats { get; init; } = [];
    }
}
