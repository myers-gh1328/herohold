using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Rules;
using Herohold.TestSupport;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Integration.Tests;

/// <summary>The typed rules API: drafts in, settled drafts and characters out.</summary>
[TestCategory("RulesData")]
[TestClass]
public sealed class CharacterRulesTests
{
    private static string Text(object value) => JsonSerializer.Serialize(value);

    [TestMethod]
    public void TypedChoicesMatchWhatTheRulesSentAsJson()
    {
        var builder = NativeCharacterBuilderTests.Builder();
        var characters = new[]
        {
            NativeCharacterBuilderTests.Fighter(),
            MulticlassCompositionTests.Combine(MulticlassCompositionTests.Complete("Fighter", 4), MulticlassCompositionTests.Complete("Ranger", 4)),
            CanonicalCharacterRulesCreate()
        };
        var compared = 0;
        foreach (var character in characters)
            foreach (var definition in builder.Describe(character).Definitions)
            {
                Assert.AreEqual(Text(JsonWorkflowViews.Choice(NativeChoiceProjection.ToJson(definition))), Text(BuilderChoices.From(definition)), definition.Id);
                compared++;
            }
        Assert.IsGreaterThan(20, compared, "The comparison covers the creation, class and feat choices of several characters.");
    }

    private static JsonObject CanonicalCharacterRulesCreate()
    {
        var created = Herohold.Rules.CanonicalCharacterRules.Create(); created["class"] = "Wizard"; created["species"] = "Elf"; created["background"] = "Sage";
        return created;
    }

    [TestMethod]
    public async Task AnExportedCharacterKeepsItsPlayFactsThroughAJsonImport()
    {
        var rules = TestStores.Workflow;
        var draft = rules.ImportJson(NativeCharacterBuilderTests.Fighter());
        Assert.IsTrue(draft.Validation.Complete, string.Join("; ", draft.Validation.Missing));
        var played = rules.Adopt(draft.Draft);
        var spent = played.Resources.First(r => r.Maximum > 0);
        played = played with
        {
            CurrentHp = played.MaximumHp - 1, TemporaryHp = 2,
            Notes = [new() { Id = "note-1", Title = "Lead", Text = "The miller lied." }],
            Resources = [.. played.Resources.Select(r => r.Id == spent.Id ? r with { Remaining = r.Maximum - 1 } : r)]
        };
        var exported = JsonNode.Parse(await rules.ExportAsync(played, "json"))!.AsObject();

        var imported = rules.ImportJson(exported);
        Assert.IsTrue(imported.Validation.Complete, string.Join("; ", imported.Validation.Missing));
        var adopted = rules.Adopt(imported.Draft);
        Assert.AreEqual(played.MaximumHp - 1, adopted.CurrentHp);
        Assert.AreEqual(2, adopted.TemporaryHp);
        Assert.AreEqual("The miller lied.", adopted.Notes.Single().Text);
        Assert.AreEqual(spent.Maximum - 1, adopted.Resources.Single(r => r.Id == spent.Id).Remaining);
        Assert.AreEqual(Text(played.Equipment), Text(adopted.Equipment));
    }

    [TestMethod]
    public void EditingASavedCharacterChangesOnlyTheEditedValue()
    {
        var rules = TestStores.Workflow;
        var saved = rules.Adopt(rules.ImportJson(NativeCharacterBuilderTests.Fighter()).Draft);
        var edited = rules.Edit(CharacterDraft.Of(saved), ["name"], ChoiceValue.Of("Renamed Fighter"), saved);
        Assert.IsTrue(edited.Validation.Complete);
        Assert.AreEqual("Renamed Fighter", edited.Summary.Name);
        var adopted = rules.Adopt(edited.Draft, saved);
        Assert.AreEqual("Renamed Fighter", adopted.Name);
        Assert.AreEqual(Text(saved with { Name = "", Build = null }), Text(adopted with { Name = "", Build = null }));
        Assert.Throws<ArgumentException>(() => rules.Edit(CharacterDraft.Of(saved), [], ChoiceValue.Of("x")));
        Assert.Throws<ArgumentException>(() => rules.Edit(CharacterDraft.Of(saved), ["include_default_equipment"], ChoiceValue.Of("yes")));
    }

    [TestMethod]
    public async Task AMulticlassCharacterKeepsEveryClassThroughSaveAndLoad()
    {
        var folder = Path.Combine(Path.GetTempPath(), "herohold-multiclass-load-" + Guid.NewGuid().ToString("N"));
        try
        {
            var rules = TestStores.Workflow; var store = TestStores.Open(folder);
            foreach (var (first, second) in new[] { (("Fighter", 5), ("Rogue", 3)), (("Wizard", 17), ("Warlock", 3)) })
            {
                var raw = MulticlassCompositionTests.Combine(MulticlassCompositionTests.Complete(first.Item1, first.Item2), MulticlassCompositionTests.Complete(second.Item1, second.Item2));
                var draft = rules.ImportJson(raw); Assert.IsTrue(draft.Validation.Complete, string.Join("; ", draft.Validation.Missing));
                var saved = WorkspaceProjection.WithNewIdentity(rules.Adopt(draft.Draft));
                await store.SaveAsync(new() { Character = saved }, 0);
                var loaded = (await store.LoadAsync(saved.Id))!.Character;
                CollectionAssert.AreEquivalent(saved.Expertise, loaded.Expertise, "Loading keeps every class's Expertise.");
                CollectionAssert.AreEquivalent(saved.Resources.Select(r => r.Id + ":" + r.Maximum).ToArray(), loaded.Resources.Select(r => r.Id + ":" + r.Maximum).ToArray(), "Loading keeps every class's resources and multiclass slots.");
                Assert.AreEqual(saved.ArmorClass, loaded.ArmorClass); Assert.AreEqual(saved.Speed, loaded.Speed);
                Assert.IsTrue(loaded.Derived.SpellcastingSources.All(s => s.SpellSaveDc > 8), "Every casting source has a save DC from the character's own modifiers.");
                if (second.Item1 == "Rogue") CollectionAssert.IsSubsetOf(ChoiceReading.Names(saved.Build!.ClassTracks!.Single(t => t.ClassName == "Rogue").Choices.Get("class_options")?.Fields.Get("expertise")).ToArray(), loaded.Expertise.ToArray());
                else Assert.AreEqual(1, loaded.Resources.Single(r => r.Id == "slot9").Maximum, "A Wizard 17 / Warlock 3 has a 9th-level slot.");
            }
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
