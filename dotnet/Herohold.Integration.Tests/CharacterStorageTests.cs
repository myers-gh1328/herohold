using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Core;
using Herohold.Storage;
using Herohold.TestSupport;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;

/// <summary>Characters are stored as player facts in SQLite tables and recalculated by the rules on load.</summary>
[TestClass]
public sealed class CharacterStorageTests
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };
    private string folder = "";
    [TestInitialize] public void Setup() => folder = Path.Combine(Path.GetTempPath(), "herohold-storage-tests", Guid.NewGuid().ToString("N"));
    [TestCleanup] public void Cleanup() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

    private static string Text<T>(T value) => JsonSerializer.Serialize(value, Json);

    /// <summary>A sample character after play that touches every kind of stored fact.</summary>
    private async Task<WorkspaceSession> PlayedAsync()
    {
        var session = new WorkspaceSession(TestStores.Open(folder), TestStores.Workflow);
        await session.InitializeAsync();
        var id = session.Character!.Id;
        void Ok(string step) => Assert.IsNull(session.Error, step + ": " + session.Error);
        await session.AddNoteAsync("At the bridge", "Ask the ferryman."); Ok("note");
        await session.AdjustResourceAsync("luck", -1); Ok("resource");
        await session.AddEffectAsync("Blessed", "Cleric", "+1d4", 10, false); Ok("effect");
        await session.RollAsync(new RollRequest { SourceCharacterId = id, SourceRevision = session.Snapshot!.Revision, Name = "Stealth", Kind = RollKind.Skill, Skill = "Stealth", Mode = DiceMode.Physical, PhysicalD20 = [14] }); Ok("roll");
        await session.TransitionCombatAsync(new CombatTransitionRequest { SourceCharacterId = id, SourceRevision = session.Snapshot!.Revision, Kind = CombatTransitionKind.Enter, NewEncounter = true, EncounterId = "test", OwnTurnNow = true, TableBoundaryConfirmed = true, BoundaryId = "turn1" }); Ok("combat");
        return session;
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task EveryStoredFactSurvivesTheTablesExactly()
    {
        var session = await PlayedAsync();
        var record = CharacterRecords.FromState(session.Character!, session.Snapshot!.Revision);
        Assert.IsNotEmpty(record.Facts.UndoHistory, "The played sample has undo history to store.");
        Assert.IsNotNull(record.Facts.LastRoll);
        var back = CharacterRows.From(record, DateTimeOffset.UtcNow).ToRecord();
        Assert.AreEqual(Text(record), Text(back));
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task AReloadedCharacterIsExactlyWhatTheSessionShowedAfterSaving()
    {
        var session = await PlayedAsync();
        var reloaded = new WorkspaceSession(TestStores.Open(folder), TestStores.Workflow);
        await reloaded.InitializeAsync();
        Assert.AreEqual(session.Snapshot!.Revision, reloaded.Snapshot!.Revision);
        Assert.AreEqual(Text(session.Character!), Text(reloaded.Character!));
        Assert.IsTrue(reloaded.Character!.Notes.Any(n => n.Title == "At the bridge"));
        var luck = reloaded.Character.Resources.Single(r => r.Id == "luck");
        Assert.AreEqual(luck.Maximum - 1, luck.Remaining, "Spent uses survive a reload.");
        await reloaded.UndoAsync(new() { SourceCharacterId = reloaded.Character.Id, SourceRevision = reloaded.Snapshot.Revision });
        Assert.IsNull(reloaded.Error, "Undo history survives a reload: " + reloaded.Error);
    }

    private static string[] AttackNames(CharacterState c)
        => [.. c.Derived.Attacks.Select(a => a.Name)];

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task ARecalculatedCharacterAttacksWithWhatItHasEquipped()
    {
        var session = await PlayedAsync();
        await session.SetEquipmentAsync("shortsword", false, false);
        Assert.IsNull(session.Error, session.Error);
        await session.AddEquipmentAsync(new() { Id = "briar", Name = "Briarbloom Bow", BaseWeapon = "Longbow", AttackBonus = 1, DamageBonus = 2, Equipped = true });
        Assert.IsNull(session.Error, session.Error);
        string[] expected = ["Longbow", "Briarbloom Bow", "Unarmed Strike"];
        CollectionAssert.AreEqual(expected, AttackNames(session.Character!), "The saved character keeps its own inventory, not starting gear.");
        var reloaded = new WorkspaceSession(TestStores.Open(folder), TestStores.Workflow);
        await reloaded.InitializeAsync();
        CollectionAssert.AreEqual(expected, AttackNames(reloaded.Character!));
        Assert.AreEqual(Text(session.Character!.Equipment.Single(i => i.Id == "briar")), Text(reloaded.Character!.Equipment.Single(i => i.Id == "briar")));
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task RulesOutputIsRecalculatedNotStored()
    {
        var session = await PlayedAsync();
        var canonical = WorkspaceProjection.ToCanonical(session.Character!);
        Assert.IsNotNull(canonical["ability_scores"]);
        var stored = CharacterRecords.FromState(session.Character!, session.Snapshot!.Revision).Build.ToCanonical(session.Character!.Id);
        foreach (var derived in new[] { "ability_scores", "skill_bonuses", "armor_class", "features", "feature_descriptions", "spell_metadata", "folio_play_metadata", "hit_points", "custom_equipment" })
            Assert.IsNull(stored[derived], derived + " is recalculated, not stored.");
        Assert.IsTrue(stored.Select(p => p.Key).All(key => BuildFacts.StoredKeys.Contains(key) || key is "schema_version" or "character_id"));
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task StaleAndCompetingSavesAreRejected()
    {
        var session = await PlayedAsync();
        var record = CharacterRecords.FromState(session.Character!, session.Snapshot!.Revision);
        var first = TestStores.Records(folder);
        var second = TestStores.Records(folder);
        var saved = await first.SaveAsync(record, record.Revision);
        Assert.AreEqual(record.Revision + 1, saved.Revision);
        await Assert.ThrowsAsync<WorkspaceConflictException>(() => second.SaveAsync(record, record.Revision));
        await Assert.ThrowsAsync<WorkspaceConflictException>(() => first.SaveAsync(saved, saved.Revision - 1));
        Assert.AreEqual(saved.Revision, (await second.LoadAsync(record.Id))!.Revision);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task SimultaneousSavesOfOneRevisionLetExactlyOneWin()
    {
        var session = await PlayedAsync();
        var record = CharacterRecords.FromState(session.Character!, session.Snapshot!.Revision);
        var stores = Enumerable.Range(0, 4).Select(_ => TestStores.Records(folder)).ToArray();
        async Task<bool> Attempt(SqliteCharacterRecordStore store, int hp)
        {
            try { await store.SaveAsync(record with { Facts = record.Facts with { CurrentHp = hp } }, record.Revision); return true; }
            catch (WorkspaceConflictException) { return false; }
        }
        var outcomes = await Task.WhenAll(stores.Select((store, i) => Attempt(store, i + 1)));
        Assert.AreEqual(1, outcomes.Count(x => x));
        var saved = (await stores[0].LoadAsync(record.Id))!;
        Assert.AreEqual(record.Revision + 1, saved.Revision);
        Assert.AreEqual(Array.IndexOf(outcomes, true) + 1, saved.Facts.CurrentHp);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task CancelledFailedAndInvalidSavesKeepThePriorRevision()
    {
        var session = await PlayedAsync();
        var store = TestStores.Open(folder);
        var current = session.Snapshot!;
        await Assert.ThrowsAsync<OperationCanceledException>(() => store.SaveAsync(current with { Character = WorkspaceRules.AdjustHp(current.Character, -1) }, current.Revision, new CancellationToken(canceled: true)));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(current with { Character = current.Character with { Equipment = [.. Enumerable.Range(0, 501).Select(i => new EquipmentItem { Id = "item-" + i, Name = "Item" })] } }, current.Revision));
        await Assert.ThrowsAsync<ArgumentException>(() => store.LoadAsync("../outside"));
        var db = Path.Combine(folder, "herohold.db");
        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(db);
            File.SetUnixFileMode(db, UnixFileMode.UserRead);
            try { await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => TestStores.Records(folder).SaveAsync(CharacterRecords.FromState(current.Character, current.Revision), current.Revision)); }
            finally { File.SetUnixFileMode(db, mode); }
        }
        var loaded = (await store.LoadAsync(current.Character.Id))!;
        Assert.AreEqual(current.Revision, loaded.Revision);
        Assert.AreEqual(current.Character.CurrentHp, loaded.Character.CurrentHp);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task TheOldJsonSavesImportOnlyWhenTheRulesRebuildThemUnchanged()
    {
        var session = await PlayedAsync();
        var saves = Path.Combine(folder, "saves");
        Directory.CreateDirectory(saves);
        async Task Write(WorkspaceSnapshot snapshot)
        {
            // The old saves kept the rules' canonical copy of the character and had no typed build or derived sheet.
            var json = JsonSerializer.SerializeToNode(snapshot, LegacyJsonSaves.Json)!.AsObject();
            var character = json["character"]!.AsObject();
            Assert.IsNotNull(character["derived"]);
            character.Remove("derived"); character.Remove("build");
            character["canonicalBuild"] = await TestStores.OldSaveCanonicalAsync(snapshot.Character);
            File.WriteAllText(Path.Combine(saves, snapshot.Character.Id + ".json"), json.ToJsonString());
        }
        await Write(session.Snapshot!);
        await Write(new WorkspaceSnapshot { Revision = 1, Character = SampleWorkspace.Create().Character with { Id = "hand-made" } });
        var target = Path.Combine(folder, "imported");
        var import = new LegacyJsonImport(TestStores.Records(target), TestStores.Hydrator);
        var results = await import.ImportAsync(saves);
        var played = results.Single(r => r.Id == session.Character!.Id);
        Assert.AreEqual("imported unchanged", played.Outcome, string.Join("; ", played.Differences));
        var refused = results.Single(r => r.Id == "hand-made");
        StringAssert.StartsWith(refused.Outcome, "not imported");
        Assert.IsTrue(refused.Differences.Any(d => d.StartsWith("character.SkillProficiencies", StringComparison.Ordinal)));
        var reloaded = (await TestStores.Open(target).LoadAsync(session.Character!.Id))!;
        Assert.AreEqual(Text(session.Character!), Text(reloaded.Character));
        Assert.IsNull(await TestStores.Open(target).LoadAsync("hand-made"));
        var again = await import.ImportAsync(saves);
        Assert.AreEqual("already imported", again.Single(r => r.Id == session.Character.Id).Outcome);
        Assert.AreEqual("imported with recalculated values", (await import.ImportAsync(saves, acceptRecalculated: true)).Single(r => r.Id == "hand-made").Outcome);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task FieldsHeroholdDoesNotUseAreListedInTheUploadReviewAndLeftOut()
    {
        var session = await PlayedAsync();
        var canonical = WorkspaceProjection.ToCanonical(session.Character!);
        canonical["homebrew_blessing"] = "Moon";
        canonical["campaign_notes_v9"] = new JsonObject { ["arc"] = 2 };
        await session.ReviewUploadAsync("upload.json", System.Text.Encoding.UTF8.GetBytes(canonical.ToJsonString()));
        Assert.IsNull(session.Error, session.Error);
        CollectionAssert.AreEqual(new[] { "campaign_notes_v9", "homebrew_blessing" }, session.Workflow!.Review!.DroppedFields.ToArray());
        var export = await session.ExportAsync("workspace");
        StringAssert.EndsWith(export!.FileName, ".herohold.json");
        await session.ReviewUploadAsync("current.herohold.json", export.Bytes);
        Assert.IsNull(session.Error, session.Error);
        Assert.AreEqual(0, session.Workflow!.Review!.DroppedFields.Count, "Herohold's own workspace download carries nothing it cannot keep.");
        // Downloads from before the rename were named .folio.json and keep their folio_ keys.
        await session.ReviewUploadAsync("current.folio.json", export.Bytes);
        Assert.IsNull(session.Error, session.Error);
        Assert.IsTrue(session.Workflow!.Review!.CanAdopt, "A download named before the rename still imports.");
        // A workspace downloaded before the typed build kept the build as a canonical character document.
        var workspace = JsonNode.Parse(export.Bytes)!.AsObject();
        var character = workspace["character"]!.AsObject();
        character.Remove("build"); character.Remove("derived");
        character["canonicalBuild"] = WorkspaceProjection.ToCanonical(session.Character!);
        character["canonicalBuild"]!["homebrew_blessing"] = "Moon";
        character["canonicalBuild"]!["campaign_notes_v9"] = new JsonObject { ["arc"] = 2 };
        await session.ReviewUploadAsync("upload.herohold.json", System.Text.Encoding.UTF8.GetBytes(workspace.ToJsonString()));
        Assert.IsNull(session.Error, session.Error);
        var dropped = session.Workflow!.Review!.DroppedFields.ToArray();
        CollectionAssert.AreEqual(new[] { "campaign_notes_v9", "homebrew_blessing" }, dropped, string.Join(", ", dropped));
        await session.AdoptImportAsync();
        Assert.IsNull(session.Error, session.Error);
        var stored = (await TestStores.Records(folder).LoadAsync(session.Character!.Id))!.Build.ToCanonical(session.Character.Id);
        Assert.IsNull(stored["homebrew_blessing"]);
        Assert.AreEqual(session.Character.Name, stored["name"]!.GetValue<string>());
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task AMulticlassCharacterSavesAndReloadsWithItsTracksAndAbilityIncreases()
    {
        var raw = MulticlassCompositionTests.Combine(MulticlassCompositionTests.Complete("Fighter", 4), MulticlassCompositionTests.Complete("Ranger", 4));
        raw["ability_score_adjustments"] = new JsonArray(
            new JsonObject { ["source_id"] = "fighter-asi-4", ["source_type"] = "class_level", ["source_class"] = "Fighter", ["source_level"] = 4, ["adjustments"] = new JsonObject { ["Dexterity"] = 2 } },
            new JsonObject { ["source_id"] = "ranger-asi-4", ["source_type"] = "class_level", ["source_class"] = "Ranger", ["source_level"] = 4, ["adjustments"] = new JsonObject { ["Dexterity"] = 1 } });
        var c = TestStores.Calculated(raw);
        var store = TestStores.Open(folder);
        var saved = await store.SaveAsync(new(){Character=c}, 0);
        var reloaded = (await store.LoadAsync(c.Id))!.Character;
        Assert.AreEqual(Text(saved.Character), Text(reloaded));
        CollectionAssert.AreEquivalent(new[] { "Fighter", "Ranger" }, reloaded.Build!.ClassLevels!.Keys.ToArray());
        Assert.IsTrue(reloaded.Build.ClassTracks!.All(t => t.RulesSource == "XPHB"), "Each track keeps its rules source.");
        CollectionAssert.AreEqual(new[] { "fighter-asi-4", "ranger-asi-4" }, reloaded.Build.AbilityScoreAdjustments!.Select(a => a.SourceId).ToArray());
        Assert.IsNull(reloaded.Build.AbilityScoreAdjustments![0].Maximum, "An increase without its own cap keeps the rules' default cap.");
        Assert.AreEqual(c.AbilityScores[Ability.Dexterity], reloaded.AbilityScores[Ability.Dexterity]);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task AnImportedCharactersOwnProfileTextSurvivesAReload()
    {
        var canonical = WorkspaceProjection.ToCanonical(SampleWorkspace.Create().Character);
        canonical.Remove("folio_character_sheet");
        canonical["backstory"] = "Raised among the briars.";
        canonical["alignment"] = "Chaotic Good";
        var imported = TestStores.Restored(canonical);
        var store = TestStores.Open(folder);
        await store.SaveAsync(new(){Character=imported}, 0);
        var reloaded = (await store.LoadAsync(imported.Id))!.Character;
        Assert.AreEqual("Raised among the briars.", SheetRules.Profile(reloaded).Backstory, "Free-text profile facts have no build home, so the import keeps them in the profile.");
        Assert.AreEqual("Chaotic Good", SheetRules.Profile(reloaded).Alignment);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task AFeatKeepsItsLevelImportedIncreaseAndRetiredChoicesThroughTheTables()
    {
        var session = await PlayedAsync();
        var record = CharacterRecords.FromState(session.Character!, session.Snapshot!.Revision);
        var feat = new FeatFact
        {
            InstanceId = "imported-alert", FeatId = "alert_xphb", Name = "Alert", Source = new() { Type = "import" }, LevelTaken = 4, AbilityIncreaseInBase = true,
            LegacyChoiceHistory = [new(StringComparer.Ordinal) { ["initiative_swap"] = ChoiceValue.Of(true), ["note"] = ChoiceValue.Of("old sheet") }]
        };
        record = record with { Build = record.Build with { Feats = [.. record.Build.Feats ?? [], feat] } };
        var back = CharacterRows.From(record, DateTimeOffset.UtcNow).ToRecord();
        Assert.AreEqual(Text(record), Text(back));
        var stored = back.Build.Feats!.Single(f => f.InstanceId == "imported-alert");
        Assert.AreEqual(4, stored.LevelTaken);
        Assert.IsTrue(stored.AbilityIncreaseInBase);
        Assert.AreEqual("old sheet", stored.LegacyChoiceHistory!.Single()["note"].Text);
    }

    [TestMethod]
    public void AFeatureConfigurationStoresOnlyThePlayersChosenValue()
    {
        var canonical = SampleWorkspace.Create().Character.Build!.ToCanonical("configured");
        canonical["feature_configurations"] = JsonNode.Parse("""[{"feature_id":"wild_companion_form","feature_name":"Wild Companion","source":{"type":"class_option","class_name":"Druid","level_gained":2},"initial_value":"Owl","current_value":"Cat","eligible_values":["Owl","Cat"]}]""");
        var build = BuildFacts.FromCanonical(canonical);
        var stored = build.FeatureConfigurations!.Single();
        Assert.AreEqual(("wild_companion_form", "Cat"), (stored.FeatureId, stored.CurrentValue), "The rules recalculate the rest of the record from the class.");
        Assert.AreEqual("""[{"feature_id":"wild_companion_form","current_value":"Cat"}]""", build.ToCanonical("configured")["feature_configurations"]!.ToJsonString());
    }

    [TestMethod]
    public void ABuildValueInAnUnexpectedShapeIsRejectedNotDropped()
    {
        var c = SampleWorkspace.Create().Character;
        var canonical = c.Build!.ToCanonical(c.Id);
        canonical["general_feats"] = new JsonArray(new JsonObject { ["instance_id"] = "x", ["feat_id"] = "alert", ["name"] = "Alert", ["source"] = new JsonObject { ["type"] = "import" }, ["choices"] = new JsonObject(), ["mystery"] = 1 });
        Assert.Throws<ArgumentException>(() => BuildFacts.FromCanonical(canonical));
    }

    [TestMethod]
    public void ChoicesKeepNamesListsNumbersAndNestedPartsInOrder()
    {
        var choices = JsonNode.Parse("""{"skills":["Stealth","Arcana"],"style":"Archery","asi":{"mode":"asi","ability_increases":{"Strength":2,"Dex/terity~":1}},"empty":[],"none":null,"flag":true}""")!.AsObject();
        var build = new BuildFacts { Name = "Fixture", ClassName = "Fighter", ClassOptions = choices.Deserialize<Dictionary<string, ChoiceValue>>() };
        var record = new CharacterRecord { Id = "fixture", Build = build };
        var back = CharacterRows.From(record, DateTimeOffset.UtcNow).ToRecord();
        Assert.IsTrue(JsonNode.DeepEquals(choices, back.Build.ToCanonical("fixture")["class_options"]));
    }
}
