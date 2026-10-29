using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Parity.Tests;

/// <summary>
/// Runs every Herohold.Parity mode and compares its output with the golden snapshot in the local
/// rules data (<c>data/parity</c>). The snapshots were captured on 27 October 2026 after the same
/// output passed the explicit expected values and the former Python reference implementation.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class ParityGoldenTests
{
    private static readonly Assembly Parity = Assembly.Load("Herohold.Parity");

    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow("calculations.json", "calculations.json", "")]
    [DataRow("rules.json", "native_rules.json", "--rules")]
    [DataRow("derivations.json", "derivations.json", "--derivations")]
    [DataRow("defenses.json", "defense_rules.json", "--defenses")]
    [DataRow("catalog.json", "catalog.json", "--catalog")]
    [DataRow("class-choices.json", "class_choices.json", "--class-choices")]
    [DataRow("feat-choices.json", "feat_choices.json", "--feat-choices")]
    [DataRow("creation-choices.json", "creation_choices.json", "--creation-choices")]
    public async Task NativeRulesMatchTheVerifiedGoldenSnapshot(string golden, string fixture, string mode)
    {
        var root = Root();
        var fixturePath = Path.Combine(root, "dotnet", "fixtures", fixture);
        string[] args = mode switch
        {
            "" => [fixturePath],
            "--rules" => [mode, fixturePath, Path.Combine(root, "data", "imported", "5etools", "classes.json")],
            "--derivations" or "--defenses" => [mode, fixturePath],
            _ => [mode, root, fixturePath],
        };
        var actual = await RunAsync(root, args);
        var expectedPath = Path.Combine(root, "data", "parity", golden);
        Assert.IsTrue(File.Exists(expectedPath), $"Golden snapshot missing: {expectedPath}. It is part of the local rules data.");
        var expected = JsonNode.Parse(await File.ReadAllTextAsync(expectedPath));
        Assert.IsTrue(JsonNode.DeepEquals(expected, actual), FirstDifference(expected, actual, "$") ?? "Outputs differ.");
    }

    // Herohold.Parity reads some rules data relative to the repository root, as CI ran it.
    private static async Task<JsonNode?> RunAsync(string root, string[] args)
    {
        var (originalOut, originalError, originalDirectory) = (Console.Out, Console.Error, Environment.CurrentDirectory);
        using var output = new StringWriter();
        using var error = new StringWriter();
        Console.SetOut(output);
        Console.SetError(error);
        Environment.CurrentDirectory = root;
        try
        {
            var result = Parity.EntryPoint!.Invoke(null, [args]);
            var code = result is Task<int> task ? await task : (int)result!;
            Assert.AreEqual(0, code, "Herohold.Parity reported a failure: " + error);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Environment.CurrentDirectory = originalDirectory;
        }
        return JsonNode.Parse(output.ToString());
    }

    private static string? FirstDifference(JsonNode? expected, JsonNode? actual, string path)
    {
        switch (expected, actual)
        {
            case (JsonObject e, JsonObject a):
                foreach (var key in e.Select(p => p.Key).Union(a.Select(p => p.Key)))
                {
                    if (!e.ContainsKey(key) || !a.ContainsKey(key)) return $"{path}.{key}: present on one side only";
                    if (FirstDifference(e[key], a[key], $"{path}.{key}") is { } inner) return inner;
                }
                return null;
            case (JsonArray e, JsonArray a):
                if (e.Count != a.Count) return $"{path}: {e.Count} items expected, {a.Count} found";
                for (var i = 0; i < e.Count; i++)
                    if (FirstDifference(e[i], a[i], $"{path}[{i}]") is { } inner) return inner;
                return null;
            default:
                return JsonNode.DeepEquals(expected, actual) ? null : $"{path}: expected {expected?.ToJsonString()}, found {actual?.ToJsonString()}";
        }
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "dotnet", "Herohold.slnx"))) return directory.FullName;
        throw new InvalidOperationException("Repository root not found.");
    }
}
