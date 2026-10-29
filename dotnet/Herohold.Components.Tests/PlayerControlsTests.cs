using Herohold.TestSupport;
using Herohold.Web.Services;
using System.Text.Json.Nodes;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Components.Tests;

[TestClass]
public sealed class PlayerControlsTests
{
    [TestMethod]
    public void ModifierBreakdownIsAvailableByFocusHoverAndClickWithoutChangingItsSources()
    {
        using var context = new BunitContext();context.JSInterop.Mode = JSRuntimeMode.Loose;
        var calculation = new Calculation(8, [new("Wisdom", 4), new("Expertise", 4)]);
        var cut = context.Render<CalculationValue>(p => p.Add(c => c.Label, "Insight").Add(c => c.Value, calculation));
        var trigger = cut.Find("button.calculation-trigger");
        Assert.AreEqual("+8", trigger.TextContent.Trim());
        Assert.AreEqual("Explain Insight: +8", trigger.GetAttribute("aria-label"));
        trigger.Focus();trigger.MouseEnter();trigger.Click();
        Assert.AreEqual(3, context.JSInterop.Invocations.Count(i => i.Identifier == "heroholdUi.showCalculation"));
        CollectionAssert.AreEqual(new[] { "focus", "pointer", "pointer" }, context.JSInterop.Invocations.Where(i => i.Identifier == "heroholdUi.showCalculation").Select(i => i.Arguments[2]).ToArray(), "Focus is marked so a focus returned by closing does not reopen it.");
        CollectionAssert.AreEqual(new[] { "Wisdom", "Expertise" }, cut.FindAll(".calculation-row span").Select(e => e.TextContent).ToArray());
        CollectionAssert.AreEqual(new[] { "+4", "+4" }, cut.FindAll(".calculation-row strong").Select(e => e.TextContent).ToArray());
        cut.Find("button[aria-label='Close calculation']").Click();
        Assert.AreEqual(1, context.JSInterop.Invocations.Count(i => i.Identifier == "heroholdUi.hideCalculation"));
        Assert.AreEqual(8, calculation.Total);Assert.AreEqual(2, calculation.Terms.Count);
    }

    [TestMethod]
    public void PhysicalDiceCountersSendResourceAndCharacterIdentityAndRespectBounds()
    {
        using var context = new BunitContext();
        var character = new CharacterState { Id = "fictional-ranger", Resources = [new() { Id = "luck", Name = "Luck", Maximum = 3, Remaining = 2 }] };
        (string Id, int Delta, string SourceId)? request = null;
        var cut = context.Render<ResourceCounters>(p => p.Add(c => c.Character, character).Add(c => c.OnAdjust, value => request = value));
        cut.Find("button[aria-label='Use one Luck']").Click();
        Assert.AreEqual(("luck", -1, "fictional-ranger"), request!.Value);
        cut.Find("button[aria-label='Restore one Luck']").Click();
        Assert.AreEqual(("luck", 1, "fictional-ranger"), request!.Value);
        Assert.AreEqual(2, character.Resources[0].Remaining, "The component emits a command instead of mutating persisted state.");
        cut.Render(p => p.Add(c => c.Character, character with { Resources = [character.Resources[0] with { Remaining = 0 }] }));
        Assert.IsTrue(cut.Find("button[aria-label='Use one Luck']").HasAttribute("disabled"));
        cut.Render(p => p.Add(c => c.Character, character with { Resources = [character.Resources[0] with { Remaining = 3 }] }));
        Assert.IsTrue(cut.Find("button[aria-label='Restore one Luck']").HasAttribute("disabled"));
        cut.Render(p => p.Add(c => c.Busy, true));
        Assert.IsTrue(cut.FindAll("button").All(b => b.HasAttribute("disabled")));
    }

    [TestMethod]
    public void UnsupportedSavedChoiceStaysVisibleForRepairAndDoesNotModifyCallerJson()
    {
        using var context = new BunitContext();
        var choice = JsonNode.Parse("""{"id":"expertise","label":"Expertise","type":"single","required":true,"status":"unresolved","value":"Insight","options":[{"value":"Stealth","label":"Stealth"}]}""")!.AsObject();
        var typed = JsonWorkflowViews.Choice(choice);var before = System.Text.Json.JsonSerializer.Serialize(typed);ChoiceValue? selected = null;
        var cut = context.Render<ChoiceEditor>(p => p.Add(c => c.Choice, typed).Add(c => c.ValueChanged, value => selected = value));
        Assert.IsTrue(cut.FindAll("option").Any(o => o.TextContent == "Insight"));
        Assert.AreEqual("unresolved", cut.Find("[data-choice-status]").GetAttribute("data-choice-status"));
        cut.Find("select").Change("0");
        Assert.AreEqual("Stealth", selected!.Text);
        Assert.AreEqual(before, System.Text.Json.JsonSerializer.Serialize(typed), "Editing never changes the caller's choice.");
    }

    [TestMethod]
    public void MultiselectSearchKeepsSelectionAndEmitsDetachedRepair()
    {
        using var context = new BunitContext();
        var choice = new JsonObject { ["id"] = "spells", ["label"] = "Spells", ["type"] = "multi", ["choose"] = 2, ["value"] = new JsonArray("Spell 01"), ["options"] = new JsonArray(Enumerable.Range(1, 70).Select(i => (JsonNode)new JsonObject { ["value"] = $"Spell {i:00}", ["label"] = $"Spell {i:00}" }).ToArray()) };
        var typed = JsonWorkflowViews.Choice(choice);var before = System.Text.Json.JsonSerializer.Serialize(typed);ChoiceValue? selected = null;
        var cut = context.Render<ChoiceEditor>(p => p.Add(c => c.Choice, typed).Add(c => c.ValueChanged, value => selected = value));
        Assert.AreEqual(60, cut.FindAll("input[type=checkbox]").Count);
        cut.Find("input[type=search]").Input("Spell 70");
        Assert.AreEqual(1, cut.FindAll("input[type=checkbox]").Count);
        cut.Find("input[type=checkbox]").Change(true);
        CollectionAssert.AreEqual(new[] { "Spell 01", "Spell 70" }, selected!.Items!.Select(v => v.Text).ToArray());
        Assert.AreEqual(before, System.Text.Json.JsonSerializer.Serialize(typed), "Editing never changes the caller's choice.");
    }

    [TestMethod]
    public void RecursiveFeatEditorUpdatesOneChildAndRetainsItsSiblingChoices()
    {
        using var context = new BunitContext();
        var choice = JsonNode.Parse("""
            {"id":"level-feat","label":"Feat","type":"feat_selection","value":{"mode":"feat","feat_id":"skill_expert","feat_ability_choice":"Wisdom","feat_choices":{"skill_proficiency":"Insight","expertise":"Stealth"}},"details":{"feat_options":[{"id":"skill_expert","name":"Skill Expert"}],"children":[{"id":"expertise","label":"Expertise","type":"single","path":["feat_choices","expertise"],"options":[{"label":"Insight","value":"Insight"},{"label":"Stealth","value":"Stealth"}]}]}}
            """)!.AsObject();
        var typed = JsonWorkflowViews.Choice(choice);var before = System.Text.Json.JsonSerializer.Serialize(typed);ChoiceValue? selected = null;
        var cut = context.Render<ChoiceEditor>(p => p.Add(c => c.Choice, typed).Add(c => c.ValueChanged, value => selected = value));
        cut.Find("[data-choice-id='expertise'] select").Change("0");
        Assert.AreEqual("Insight", selected!.At(["feat_choices", "expertise"])!.Text);
        Assert.AreEqual("Insight", selected.At(["feat_choices", "skill_proficiency"])!.Text);
        Assert.AreEqual("Wisdom", selected.At(["feat_ability_choice"])!.Text);
        Assert.AreEqual("skill_expert", selected.At(["feat_id"])!.Text);
        Assert.AreEqual(before, System.Text.Json.JsonSerializer.Serialize(typed), "Editing never changes the caller's choice.");
        cut.Render(p => p.Add(c => c.Busy, true));
        Assert.IsTrue(cut.FindAll("select").All(select => select.HasAttribute("disabled")));
    }

    [TestMethod]
    public void BusyAsiModeCannotChangeItsLocalAllocationOrSwitchToAFeat()
    {
        using var context = new BunitContext();
        var choice = JsonNode.Parse("""{"id":"asi","label":"Improvement","type":"asi_or_feat","value":{"mode":"asi","ability_increases":{"Strength":1,"Dexterity":1}}}""")!.AsObject();
        var cut = context.Render<ChoiceEditor>(p => p.Add(c => c.Choice, JsonWorkflowViews.Choice(choice)).Add(c => c.Busy, true));
        Assert.AreEqual(3, cut.FindAll("select").Count);
        Assert.IsTrue(cut.FindAll("select, button").All(control => control.HasAttribute("disabled")));
    }

    [TestMethod]
    public void NotesAndDefenseSourcesRenderAsTextIncludingUntrustedMarkup()
    {
        using var context = new BunitContext();
        var notes = context.Render<NoteList>(p => p.Add(c => c.Notes, new[] { new JournalNote { Title = "Camp", Text = "<script>window.bad=true</script>" } }));
        Assert.AreEqual(0, notes.FindAll("script").Count);StringAssert.Contains(notes.Find("p").TextContent, "<script>");
        var defense = context.Render<DefenseSummary>();StringAssert.Contains(defense.Markup, "None listed");
    }
}
