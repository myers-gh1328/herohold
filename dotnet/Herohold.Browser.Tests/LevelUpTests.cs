using System.Text.RegularExpressions;
using Aspose.Pdf.Text;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Browser.Tests;

/// <summary>Wizard spell and HP choices through level 4, and Bard Jack of All Trades on skills.</summary>
[TestCategory("RulesData")]
[TestClass]
[DoNotParallelize]
public sealed class LevelUpTests : HostedTest
{
    [ClassCleanup]
    public static Task StopBrowser() => StopBrowserAsync();

    /// <summary>Walks the workflow steps until the choice is on screen.</summary>
    private async Task ShowFieldAsync(string choiceId)
    {
        if (await Ui.Field(choiceId).CountAsync() > 0) return;
        foreach (var label in await Page.Locator(".luf-steps button").AllTextContentsAsync())
        {
            var step = Regex.Replace(label.Trim(), @"^\d+\.\s*", "");
            if (step is "Review gains" or "Hit points" or "Review & confirm") continue;
            await Ui.StepAsync(step);
            if (await Ui.Field(choiceId).CountAsync() > 0) return;
        }
        Assert.Fail("Required choice not shown: " + choiceId);
    }

    private async Task ChooseAsync(string choiceId, string label) { await ShowFieldAsync(choiceId); await Ui.ChooseAsync(choiceId, label); }
    private async Task MultiAsync(string choiceId, string label) { await ShowFieldAsync(choiceId); await Ui.MultiAsync(choiceId, label); }

    private async Task LevelUpAsync(int previous)
    {
        await Expect(Ui.Header).ToContainTextAsync($"Wizard {previous}");
        await Ui.OpenLevelUpAsync();
        await Page.GetByTestId("workflow-next").ClickAsync();
        await Expect(Page.Locator("#level-add-current-hp")).ToBeVisibleAsync();
        await Expect(Page.Locator("#level-add-current-hp")).Not.ToBeCheckedAsync();
        await Page.GetByTestId("workflow-next").ClickAsync();
        await Expect(Page.Locator(".luf-panel h2")).Not.ToHaveTextAsync("Add hit points.");
    }

    private async Task CommitAsync(int level, string hp)
    {
        await Expect(Page.GetByTestId("commit-workflow")).ToBeEnabledAsync();
        await Page.GetByTestId("commit-workflow").ClickAsync();
        await Expect(Page.GetByTestId("character-workflow")).ToHaveCountAsync(0);
        await Expect(Ui.Header).ToContainTextAsync($"Wizard {level}");
        Assert.AreEqual(hp, await Ui.HpAsync());
    }

    [TestMethod]
    public async Task AWizardLearnsSpellsAndConstitutionRaisesHitPointsOnlyWhenConfirmed()
    {
        await Ui.ReviewUploadAsync(await Fixtures.PathAsync("wizard.json"));
        await Ui.StepAsync("Review & confirm");
        await CommitAsync(1, "6 / 6");

        await LevelUpAsync(1);
        await MultiAsync("wizard_2_spellbook_spells", "Detect Magic");
        await MultiAsync("wizard_2_spellbook_spells", "Disguise Self");
        await ChooseAsync("wizard_2_wizard_scholar_skill", "Arcana");
        await Ui.StepAsync("Review & confirm");
        Assert.AreEqual("6 → 10", await Ui.ReviewValueAsync("Maximum HP"));
        Assert.AreEqual("6", await Ui.ReviewValueAsync("Current HP"));
        Assert.AreEqual("6 / 6", await Ui.HpAsync(), "A draft does not change the saved character.");
        await CommitAsync(2, "6 / 10");

        await LevelUpAsync(2);
        await ChooseAsync("wizard_3_wizard_subclass", "Abjurer");
        foreach (var spell in new[] { "Expeditious Retreat", "False Life" }) await MultiAsync("wizard_3_spellbook_spells", spell);
        foreach (var spell in new[] { "Mage Armor", "Protection from Evil and Good" }) await MultiAsync("wizard_3_savant_spells", spell);
        await Ui.StepAsync("Review & confirm");
        Assert.AreEqual("10 → 14", await Ui.ReviewValueAsync("Maximum HP"));
        Assert.AreEqual("6", await Ui.ReviewValueAsync("Current HP"));
        await CommitAsync(3, "6 / 14");

        await LevelUpAsync(3);
        await ShowFieldAsync("wizard_4_ability_score_improvement");
        var asi = Ui.Field("wizard_4_ability_score_improvement");
        await asi.Locator(".field-grid select").First.SelectOptionAsync("Constitution");
        await asi.Locator("button.section-gap").ClickAsync();
        await HeroholdUi.UntilAsync(async () => ((await Ui.ValueAsync("wizard_4_ability_score_improvement")) as System.Text.Json.Nodes.JsonObject)?["ability_increases"]?["Constitution"]?.GetValue<int>() == 2, "Constitution +2");
        foreach (var spell in new[] { "Feather Fall", "Find Familiar" }) await MultiAsync("wizard_4_spellbook_spells", spell);
        await MultiAsync("wizard_4_cantrips", "Fire Bolt");
        await Ui.StepAsync("Review & confirm");
        // Constitution 10 to 12 adds 1 per level: 14 + fixed 4 + retroactive 4 = 22.
        Assert.AreEqual("14 → 22", await Ui.ReviewValueAsync("Maximum HP"));
        Assert.AreEqual("6", await Ui.ReviewValueAsync("Current HP"));
        foreach (var (add, expected) in new[] { (true, "14"), (false, "6"), (true, "14") })
        {
            await Ui.StepAsync("Hit points");
            await Page.Locator("#level-add-current-hp").SetCheckedAsync(add);
            await Ui.StepAsync("Review & confirm");
            Assert.AreEqual("14 → 22", await Ui.ReviewValueAsync("Maximum HP"));
            Assert.AreEqual(expected, await Ui.ReviewValueAsync("Current HP"));
            Assert.AreEqual("6 / 14", await Ui.HpAsync(), "Previewing the HP policy leaves the saved character alone.");
        }
        await CommitAsync(4, "14 / 22");

        var character = await Ui.ExportJsonAsync();
        Assert.AreEqual(4, character["level"]!.GetValue<int>());
        Assert.AreEqual("Abjurer", character["subclass"]!.GetValue<string>());
        Assert.AreEqual(12, character["ability_scores"]!["Constitution"]!.GetValue<int>());
        Assert.AreEqual(22, character["hit_points"]!["maximum"]!.GetValue<int>());
        Assert.AreEqual(14, character["hit_points"]!["current"]!.GetValue<int>());
        var book = new[] { "Detect Magic", "Disguise Self", "Expeditious Retreat", "False Life", "Mage Armor", "Protection from Evil and Good", "Feather Fall", "Find Familiar" };
        var owned = character["spellbook_spells"]!.AsArray().Select(v => v!.GetValue<string>()).ToHashSet();
        Assert.IsTrue(owned.IsSupersetOf(book), string.Join(", ", book.Except(owned)));
        var savant = character["class_options"]!["savant_spells"]!.AsArray().Select(v => v!.GetValue<string>()).ToHashSet();
        Assert.IsTrue(savant.SetEquals(["Mage Armor", "Protection from Evil and Good"]));
        Assert.IsFalse(savant.Overlaps(character["spells"]!["spellbook"]!.AsArray().Select(v => v!.GetValue<string>())), "Savant grants stay separate from learned spells.");
        CollectionAssert.Contains(character["spells"]!["cantrips"]!.AsArray().Select(v => v!.GetValue<string>()).ToList(), "Fire Bolt");
        Assert.AreEqual(0, character["spells"]!["prepared"]!.AsArray().Count, "Learning book spells does not replace the prepared list.");

        var pdfPath = await Ui.ExportAsync("export-pdf");
        using (var pdf = new Aspose.Pdf.Document(pdfPath))
        {
            Assert.IsGreaterThan(3, pdf.Pages.Count, "The whole spellbook prints on a continuation page.");
            var absorber = new TextAbsorber();
            for (var page = 4; page <= pdf.Pages.Count; page++) absorber.Visit(pdf.Pages[page]);
            var lines = absorber.Text.Split('\n').Select(l => l.Trim()).ToHashSet();
            Assert.IsTrue(lines.IsSupersetOf(book), "Printable book omitted: " + string.Join(", ", book.Except(lines)));
            Assert.AreEqual(334, pdf.Form!.Fields.Length, "Continuation pages keep the editable template fields.");
        }

        await Page.Keyboard.PressAsync("Escape");
        await Ui.ReloadAsync();
        Assert.AreEqual("14 / 22", await Ui.HpAsync());
        await Page.Locator(".player-header .rest-menu summary").ClickAsync();
        await Page.GetByTestId("long-rest").ClickAsync();
        await Expect(Page.Locator("#rest-dialog")).ToBeVisibleAsync();
        await Expect(Page.Locator("#rest-change-spells")).Not.ToBeCheckedAsync();
        await Page.Locator("#rest-change-spells").CheckAsync();
        await Expect(Page.Locator("#rest-spell-picker")).ToBeVisibleAsync();
        var eligible = (await Page.Locator("#rest-spell-picker .spell-choice span").AllTextContentsAsync()).Select(t => t.Trim()).ToHashSet();
        Assert.IsTrue(eligible.IsSupersetOf(book), "Missing from preparation choices: " + string.Join(", ", book.Except(eligible)));
        await Page.Locator("#rest-dialog [aria-label='Close rest review']").ClickAsync();
        await Expect(Page.Locator("#rest-dialog")).ToBeHiddenAsync();
        Assert.AreEqual("14 / 22", await Ui.HpAsync(), "Looking at preparations does not complete a rest.");
    }

    [TestMethod]
    public async Task JackOfAllTradesAddsToUntrainedSkillsOnlyAndSurvivesReload()
    {
        await Ui.UploadAsync(await Fixtures.PathAsync("bard.json"));
        await Expect(Ui.Header).ToContainTextAsync("Bard 2");
        await Ui.AllSkillsAsync();
        foreach (var (skill, total) in new[] { ("Insight", "+1"), ("Arcana", "+2"), ("Athletics", "+5") })
            Assert.AreEqual(total, await Ui.SkillAsync(skill), skill);
        Assert.IsTrue((await Ui.SkillTermsAsync("Insight")).Any(t => t.Label.Contains("Jack of All Trades") && t.Value == "+1"));
        var trained = await Ui.SkillTermsAsync("Arcana");
        Assert.IsTrue(trained.Any(t => t.Label.Contains("roficien", StringComparison.OrdinalIgnoreCase) && t.Value == "+2"));
        Assert.IsFalse(trained.Any(t => t.Label.Contains("Jack of All Trades")));
        var expertise = await Ui.SkillTermsAsync("Athletics");
        Assert.IsTrue(expertise.Any(t => t.Label.Contains("Expertise") && t.Value == "+4"));
        Assert.IsFalse(expertise.Any(t => t.Label.Contains("Jack of All Trades")));
        await Expect(Page.GetByLabel("Explain Wisdom saving throw: +0")).ToHaveTextAsync("+0");
        await Expect(Page.GetByLabel("Explain Initiative: +0")).ToHaveTextAsync("+0");
        await Ui.ReloadAsync();
        await Ui.AllSkillsAsync();
        Assert.AreEqual("+1", await Ui.SkillAsync("Insight"));
        Assert.AreEqual("+2", await Ui.SkillAsync("Arcana"));
        Assert.AreEqual("+5", await Ui.SkillAsync("Athletics"));
    }
}
