using Herohold.TestSupport;
using Herohold.Core;
using System.Text.Json.Nodes;
using Herohold.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Integration.Tests;

[TestClass]
public sealed class NativeLevelUpAllClassTests
{
    private static JsonObject Patch(IReadOnlyList<string> path, JsonNode? value)
    {
        var result = new JsonObject();var cursor = result;
        for (var i = 0; i < path.Count - 1; i++) { var child = new JsonObject();cursor[path[i]] = child;cursor = child; }
        cursor[path[^1]] = value?.DeepClone();return result;
    }
    private static JsonNode? Value(BuilderChoiceDefinition choice, JsonObject character)
    {
        var options = choice.Options.Where(o => !o.Disabled).ToArray();
        if (choice.Mode == BuildChoiceMode.AsiOrFeat)
        {
            var ability = character["ability_scores"]!.AsObject().First(p => p.Value!.GetValue<int>() <= 18).Key;
            return new JsonObject { ["mode"] = "asi", ["ability_increases"] = new JsonObject { [ability] = 2 } };
        }
        if (choice.Mode == BuildChoiceMode.Multi)
        {
            Assert.IsTrue(options.Length >= choice.Choose, choice.Id + ": insufficient eligible options");
            return new JsonArray(options.Take(choice.Choose).Select(o => CanonicalFixtures.Node(o.Value)).ToArray());
        }
        Assert.IsTrue(options.Length > 0, choice.Id + ": required option list is empty");return CanonicalFixtures.Node(options[0].Value);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow("Barbarian", 5)] [DataRow("Bard", 5)] [DataRow("Cleric", 5)] [DataRow("Druid", 5)]
    [DataRow("Fighter", 7)] [DataRow("Monk", 5)] [DataRow("Paladin", 5)] [DataRow("Ranger", 5)]
    [DataRow("Rogue", 6)] [DataRow("Sorcerer", 5)] [DataRow("Warlock", 5)] [DataRow("Wizard", 5)]
    public void AllTwelveClassesAdvanceOneLevelAtATimeAndStoreEveryLevel(string className, int expectedImprovements)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);while (root is not null && !File.Exists(Path.Combine(root.FullName, "data/imported/5etools/classes.json"))) root = root.Parent;
        var catalog = NativeCatalogIntegrationTests.Snapshot();var classes = ClassProgressionCatalog.Parse(File.ReadAllText(Path.Combine(root!.FullName, "data/imported/5etools/classes.json")));
        var builder = NativeCharacterBuilderTests.Builder();var edits = new CharacterEditRules(catalog);var rules = new NativeLevelUpRules(catalog, classes, new(catalog), edits, builder);
        var character = NativeCharacterBuilderTests.Fighter();character["class"] = className;character["class_options"] = new JsonObject();character["weapon_masteries"] = new JsonArray();
        for (var step = 0; step < 80; step++)
        {
            character = builder.Recalculate(character);var report = builder.Describe(character);var unresolved = report.Definitions.FirstOrDefault(d => d.Status != BuildChoiceStatus.Resolved);
            if (unresolved is null) { Assert.AreEqual(0, report.Issues.Count, string.Join("; ", report.Issues.Select(i => i.Message)));break; }
            Assert.IsTrue(step < 79, className + ": initial creation did not converge");
            character = edits.Apply(character, Patch(unresolved.Path, Value(unresolved, character)));
        }
        for (var target = 2; target <= 20; target++)
        {
            var before = character.ToJsonString();var review = rules.Start(character, target);
            for (var step = 0; !review.CanFinalize && step < 80; step++)
            {
                var pending = review.PendingChoices.FirstOrDefault(c => c.Status != BuildChoiceStatus.Resolved);
                Assert.IsNotNull(pending, className + " " + target + ": " + string.Join("; ", review.Issues.Select(i => i.Message)));
                review = rules.Choose(character, review.Draft, pending.Id, Value(pending, pending.Mode == BuildChoiceMode.AsiOrFeat ? builder.Recalculate(review.Character) : review.Character));
            }
            Assert.IsTrue(review.CanFinalize, className + " " + target + ": " + string.Join("; ", review.Issues.Select(i => i.Message)));
            var result = rules.Commit(character, review.Draft);Assert.AreEqual(before, character.ToJsonString(), "The source was mutated before persistence.");character = result.Build.ReplaceIn(character, "all-classes");
            Assert.AreEqual(target, character["level"]!.GetValue<int>());
            Assert.IsTrue(builder.ResultFor(character)["validation"]!["complete"]!.GetValue<bool>(), className + " " + target + ": final native validation failed");
            // Every level's build must be storable exactly as committed.
            var record = new CharacterRecord { Id = "all-classes", Build = BuildFacts.FromCanonical(character) };
            Assert.AreEqual(System.Text.Json.JsonSerializer.Serialize(record), System.Text.Json.JsonSerializer.Serialize(Herohold.Storage.CharacterRows.From(record, DateTimeOffset.UtcNow).ToRecord()), className + " " + target + ": the build did not survive the tables");
        }
        Assert.AreEqual(19, character["completed_level_up_transactions"]!.AsArray().Count);Assert.AreEqual(19, character["level_history"]!.AsArray().Count);Assert.AreEqual(20, character["hp_history"]!.AsArray().Count);
        Assert.AreEqual(expectedImprovements, character["ability_score_adjustments"]!.AsArray().Count);
    }
}
