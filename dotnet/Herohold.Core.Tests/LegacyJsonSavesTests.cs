using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Core.Tests;

/// <summary>The old JSON save files are read once for the SQLite import, never written.</summary>
[TestClass]
public sealed class LegacyJsonSavesTests
{
    private string directory = null!;

    [TestInitialize]
    public void Setup() { directory = Path.Combine(Path.GetTempPath(), "herohold-legacy-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); }

    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    private string Write(CharacterState c, long revision = 1, Action<JsonObject>? edit = null)
    {
        var node = JsonSerializer.SerializeToNode(new WorkspaceSnapshot { Revision = revision, Character = c }, LegacyJsonSaves.Json)!.AsObject();
        // The old saves kept the build as a canonical character document and had no typed build or derived sheet.
        var character = node["character"]!.AsObject();
        character.Remove("build"); character.Remove("derived");
        character["canonicalBuild"] = c.Build?.ToCanonical(c.Id);
        edit?.Invoke(node);
        var path = Path.Combine(directory, c.Id + ".json");
        File.WriteAllText(path, node.ToJsonString());
        return path;
    }

    [TestMethod]
    public async Task SavesAreReadInIdOrderWithPlayAndBuildIntactAndNeverRewritten()
    {
        var first = Fixtures.Ranger() with { Build = new() { ClassName = "Ranger" } };
        var roll = PlayRules.Roll(new() { Revision = 1, Character = first }, new() { OperationId = "saved-roll", SourceRevision = 1, Kind = RollKind.Save, Ability = Ability.Dexterity, Mode = DiceMode.Physical, PhysicalD20 = [8] });
        var path = Write(roll, 2);
        Write(Fixtures.Ranger() with { Id = "a-second" });
        File.WriteAllText(Path.Combine(directory, "ignored.tmp"), "temporary");
        var before = File.ReadAllText(path);
        var saves = await LegacyJsonSaves.ReadAllAsync(directory);
        CollectionAssert.AreEqual(new[] { "a-second", roll.Id }, saves.Select(s => s.Snapshot.Character.Id).ToArray());
        var loaded = saves[1].Snapshot;
        Assert.AreEqual(2L, loaded.Revision);
        Assert.AreEqual(roll.Play.LastRoll!.Total, loaded.Character.Play.LastRoll!.Total);
        Assert.AreEqual("Ranger", loaded.Character.Build!.ClassName);
        Assert.AreEqual("Ranger", saves[1].Canonical!["class"]!.GetValue<string>(), "The saved canonical copy is kept for comparing what the sheet showed.");
        Assert.AreEqual(before, File.ReadAllText(path));
    }

    [TestMethod]
    public async Task OlderSchemasReadWithDefaultsForMissingParts()
    {
        Write(Fixtures.Ranger(), 1, node =>
        {
            node["schemaVersion"] = 1;
            foreach (var key in new[] { "play", "canonicalBuild", "features", "defenses", "knownTarget", "standingAdjustmentTerms" }) node["character"]!.AsObject().Remove(key);
        });
        var loaded = (await LegacyJsonSaves.ReadAllAsync(directory)).Single().Snapshot;
        Assert.AreEqual(3, loaded.SchemaVersion);
        Assert.AreEqual(0, loaded.Character.Play.UndoHistory.Count);
        Assert.AreEqual(32, loaded.Character.CurrentHp);
    }

    [TestMethod]
    public async Task DamagedFilesAndUnsafeNamesStopTheReadInsteadOfBeingSkipped()
    {
        Write(Fixtures.Ranger());
        File.WriteAllText(Path.Combine(directory, "broken.json"), "{malformed");
        await Assert.ThrowsAsync<InvalidDataException>(() => LegacyJsonSaves.ReadAllAsync(directory));
        File.Delete(Path.Combine(directory, "broken.json"));
        Write(Fixtures.Ranger() with { Id = "future" }, 1, node => node["schemaVersion"] = 4);
        await Assert.ThrowsAsync<InvalidDataException>(() => LegacyJsonSaves.ReadAllAsync(directory));
        File.Delete(Path.Combine(directory, "future.json"));
        File.WriteAllText(Path.Combine(directory, ".hidden.json"), "{}");
        await Assert.ThrowsAsync<InvalidDataException>(() => LegacyJsonSaves.ReadAllAsync(directory));
        File.Delete(Path.Combine(directory, ".hidden.json"));
        Write(Fixtures.Ranger() with { Id = "renamed" }, 1, node => node["character"]!["id"] = "other");
        await Assert.ThrowsAsync<InvalidDataException>(() => LegacyJsonSaves.ReadAllAsync(directory));
    }

    [TestMethod]
    public async Task AMissingFolderHasNoSaves() => Assert.AreEqual(0, (await LegacyJsonSaves.ReadAllAsync(Path.Combine(directory, "missing"))).Count);
}

[TestClass]
public sealed class WorkspaceLimitsTests
{
    [TestMethod]
    public void OversizedInvalidOrExhaustedWorkspacesAreRejectedBeforeAnyStoreSeesThem()
    {
        var large = Fixtures.Ranger() with
        {
            Build = new() { MigrationWarnings = [new string('x', 1_100_000)] },
            Play = new() { Spells = Enumerable.Range(0, 400).Select(i => new SpellDefinition { Id = "spell-" + i, Name = "Spell " + i, Level = 1, Description = new string('x', 3000) }).ToList() }
        };
        WorkspaceRules.Validate(large);
        Assert.Throws<InvalidDataException>(() => WorkspaceLimits.Validate(new() { Character = large }));
        var small = new WorkspaceSnapshot { Character = Fixtures.Ranger() };
        WorkspaceLimits.Validate(small);
        Assert.Throws<WorkspaceConflictException>(() => WorkspaceLimits.Validate(small with { Revision = long.MaxValue }));
        Assert.Throws<InvalidDataException>(() => WorkspaceLimits.Validate(small with { SchemaVersion = 4 }));
        Assert.Throws<ArgumentException>(() => WorkspaceLimits.Validate(small with { Character = small.Character with { CurrentHp = 100 } }));
        Assert.Throws<ArgumentException>(() => WorkspaceLimits.Validate(small with { Character = small.Character with { Equipment = [.. Enumerable.Range(0, 501).Select(i => new EquipmentItem { Id = "item-" + i, Name = "Item" })] } }));
        foreach (var id in new[] { "ranger-demo\n", "ranger/demo", "ranger\\demo", ".", "", new string('a', 65) })
            Assert.Throws<ArgumentException>(() => WorkspaceLimits.Validate(small with { Character = small.Character with { Id = id } }));
    }
}
