using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Core;

namespace Herohold.Web.Services;

public sealed record LegacyImportResult(string Id, string Name, string Outcome, IReadOnlyList<string> Differences);

/// <summary>
/// One-time move of the old JSON saves into the SQLite store. A character is imported only when the rules
/// rebuild it from its stored facts exactly as it was saved; otherwise its differences are reported and it is left out.
/// </summary>
public sealed class LegacyJsonImport(ICharacterRecordStore records, CharacterHydrator hydrator)
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public async Task<IReadOnlyList<LegacyImportResult>> ImportAsync(string directory, bool acceptRecalculated = false, CancellationToken ct = default)
    {
        var results = new List<LegacyImportResult>();
        foreach (var (snapshot, canonical) in await LegacyJsonSaves.ReadAllAsync(directory, ct))
        {
            // The old saves predate the typed derived sheet; read what they showed from their canonical copy.
            var c = snapshot.Character with { Derived = WorkspaceProjection.DerivedFrom(canonical) };
            if (await records.LoadAsync(c.Id, ct) is not null) { results.Add(new(c.Id, c.Name, "already imported", [])); continue; }
            WorkspaceLimits.Validate(snapshot);
            var record = CharacterRecords.FromState(c, 0);
            var rebuilt = await hydrator.HydrateAsync(record, ct);
            var differences = Differences(c, rebuilt);
            if (differences.Count > 0 && !acceptRecalculated) { results.Add(new(c.Id, c.Name, "not imported: the recalculated sheet differs", differences)); continue; }
            await records.SaveAsync(record, 0, ct);
            results.Add(new(c.Id, c.Name, differences.Count == 0 ? "imported unchanged" : "imported with recalculated values", differences));
        }
        return results.AsReadOnly();
    }

    /// <summary>Paths whose value differs between the saved and the rebuilt character. The build itself is compared only where the sheet shows it.</summary>
    public static IReadOnlyList<string> Differences(CharacterState saved, CharacterState rebuilt)
    {
        var differences = new List<string>();
        void Walk(string path, JsonNode? a, JsonNode? b)
        {
            if (a is JsonObject x && b is JsonObject y)
            {
                foreach (var key in x.Select(p => p.Key).Union(y.Select(p => p.Key)).Order(StringComparer.Ordinal)) Walk(path + "." + key, x[key], y[key]);
            }
            else if (a is JsonArray l && b is JsonArray r && l.Count == r.Count)
            {
                for (var i = 0; i < l.Count; i++) Walk(path + "[" + i + "]", l[i], r[i]);
            }
            else if (!JsonNode.DeepEquals(a, b)) differences.Add(path + ": " + Short(a) + " → " + Short(b));
        }
        Walk("character", JsonSerializer.SerializeToNode(saved with { Build = null }, Json), JsonSerializer.SerializeToNode(rebuilt with { Build = null }, Json));
        Walk("shown", Views(saved), Views(rebuilt));
        return differences;
    }

    /// <summary>What the sheet shows from the rules' copy of the build, beyond the typed character and its derived sheet.</summary>
    private static JsonObject Views(CharacterState c)
    {
        var views = new JsonObject
        {
            ["coins"] = SheetRules.HasUnresolvedCoins(c) ? "unresolved" : JsonSerializer.SerializeToNode(SheetRules.Purse(c), Json),
            ["experience_points"] = SheetRules.HasUnresolvedExperience(c) ? "unresolved" : SheetRules.ExperiencePoints(c),
            ["heroic_inspiration"] = SheetRules.HasHeroicInspiration(c),
            ["movement"] = JsonSerializer.SerializeToNode(AdventureRules.Movement(c), Json),
            ["journal"] = JsonSerializer.SerializeToNode(JournalRules.Entries(c), Json),
            ["profile"] = JsonSerializer.SerializeToNode(SheetRules.Profile(c), Json),
            ["class_levels"] = JsonSerializer.SerializeToNode(c.Build?.ClassLevels, Json),
            ["class_tracks"] = JsonSerializer.SerializeToNode(c.Build?.ClassTracks, Json),
            ["pdf_passthrough_fields"] = JsonSerializer.SerializeToNode(c.Build?.PdfPassthroughFields, Json)
        };
        return views;
    }

    private static string Short(JsonNode? node)
    {
        var text = node?.ToJsonString() ?? "(none)";
        return text.Length > 80 ? text[..77] + "…" : text;
    }
}

/// <summary><c>Herohold.Web import-json &lt;saves folder&gt; &lt;database&gt; [--accept-recalculated]</c>: prints one line per character.</summary>
public static class LegacyImportCommand
{
    public static async Task<int> RunAsync(string[] args, string repositoryRoot, string pdfWorkerAssembly, TextWriter output)
    {
        var options = args.Where(a => a.StartsWith("--", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        var paths = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
        if (paths.Length != 2 || options.Except(["--accept-recalculated"]).Any())
        {
            output.WriteLine("Usage: import-json <saves folder> <database file> [--accept-recalculated]");
            return 2;
        }
        using var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddHeroholdRules(repositoryRoot, pdfWorkerAssembly).AddHeroholdStorage(paths[1]).AddSingleton<LegacyJsonImport>().BuildServiceProvider();
        var results = await services.GetRequiredService<LegacyJsonImport>().ImportAsync(paths[0], options.Contains("--accept-recalculated"));
        foreach (var result in results)
        {
            output.WriteLine($"{result.Id} ({result.Name}): {result.Outcome}");
            foreach (var difference in result.Differences.Take(40)) output.WriteLine("    " + difference);
            if (result.Differences.Count > 40) output.WriteLine($"    … {result.Differences.Count - 40} more");
        }
        return results.Any(r => r.Outcome.StartsWith("not imported", StringComparison.Ordinal)) ? 1 : 0;
    }
}
