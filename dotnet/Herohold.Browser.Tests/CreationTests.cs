using System.Text.Json.Nodes;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Browser.Tests;

/// <summary>Character creation through visible choices, and drafts that stay repairable until complete.</summary>
[TestCategory("RulesData")]
[TestClass]
[DoNotParallelize]
public sealed class CreationTests : HostedTest
{
    [ClassCleanup]
    public static Task StopBrowser() => StopBrowserAsync();

    private ILocator Pool => Page.Locator(".ability-score-pool strong");

    private async Task StartCreationAsync(string name)
    {
        await Ui.OpenLibraryAsync();
        await Page.GetByTestId("create-character").ClickAsync();
        var input = Ui.Field("name").Locator("input");
        await input.FillAsync(name);
        await input.PressAsync("Tab");
        await Expect(Ui.Field("name")).ToHaveAttributeAsync("data-choice-value", System.Text.Json.JsonSerializer.Serialize(name));
    }

    private async Task SetManualScoreAsync(string ability, int value)
    {
        var input = Page.Locator(".luf-panel .ability-editor-grid label").Filter(new() { HasTextRegex = new($"^{ability}$") }).Locator("input");
        await input.FillAsync(value.ToString());
        await input.PressAsync("Tab");
        await HeroholdUi.UntilAsync(async () => (await Ui.ValueAsync("base_ability_scores"))?[ability]?.GetValue<int>() == value, $"{ability} = {value}");
    }

    private async Task SelectInPanelAsync(string label, bool last = false)
    {
        var selects = Page.Locator(".luf-panel select").Filter(new() { Has = Page.Locator("option", new() { HasTextRegex = new($"^\\s*{System.Text.RegularExpressions.Regex.Escape(label)}\\s*$") }) });
        await Expect(selects.First).ToBeVisibleAsync();
        await (last ? selects.Last : selects.First).SelectOptionAsync(new SelectOptionValue { Label = label });
    }

    private async Task CommitLevelAsync(int level, string name = "UI Born")
    {
        await Ui.CommitAsync();
        await Expect(Ui.Header).ToContainTextAsync("Fighter " + level);
        await Expect(Ui.Header).ToContainTextAsync(name);
    }

    private async Task StartLevelUpAsync()
    {
        await Ui.OpenLevelUpAsync();
        await Page.GetByTestId("workflow-next").ClickAsync();
        await Expect(Page.Locator(".luf-panel h2")).ToHaveTextAsync("Add hit points.");
        await Page.GetByTestId("workflow-next").ClickAsync();
        await Expect(Page.Locator(".luf-panel h2")).Not.ToHaveTextAsync("Add hit points.");
    }

    [TestMethod]
    public async Task AFighterIsCreatedFromVisibleChoicesAndProgressesThroughSubclassAndFeat()
    {
        await StartCreationAsync("UI Born");
        await Ui.ChooseAsync("class", "Fighter");
        await Ui.ChooseAsync("species", "Human");
        await Ui.ChooseAsync("background", "Soldier");

        // Leaving the draft keeps it resumable.
        await Page.GetByTestId("nav-equipment").ClickAsync();
        await Page.Locator(".draft-resume button").ClickAsync();
        await Expect(Ui.Field("name")).ToHaveAttributeAsync("data-choice-value", "\"UI Born\"");

        await Ui.StepAsync("Origin");
        await Ui.MultiAsync("origin_languages", "Elvish");
        await Ui.MultiAsync("origin_languages", "Dwarvish");

        await Ui.StepAsync("Abilities");
        await Page.GetByTestId("ability-method").SelectOptionAsync("Standard array");
        await Expect(Pool).ToHaveCountAsync(6);
        CollectionAssert.AreEqual(new[] { "15", "14", "13", "12", "10", "8" }, (await Pool.AllTextContentsAsync()).Select(t => t.Trim()).ToArray());
        await Page.Locator("[data-ability-assignment=Dexterity]").SelectOptionAsync("0");
        await Expect(Page.Locator("[data-ability-assignment=Strength]")).ToHaveValueAsync("1");
        await Page.GetByTestId("ability-method").SelectOptionAsync("Roll 4d6");
        await Expect(Pool).ToHaveCountAsync(0);
        await Page.GetByTestId("generate-ability-scores").ClickAsync();
        await Expect(Pool).ToHaveCountAsync(6);
        var rolled = (await Pool.AllTextContentsAsync()).Select(t => int.Parse(t.Trim())).ToArray();
        Assert.IsTrue(rolled.All(score => score is >= 3 and <= 18), string.Join(",", rolled));
        await Ui.StepAsync("Background");
        await Ui.StepAsync("Abilities");
        CollectionAssert.AreEqual(rolled, (await Pool.AllTextContentsAsync()).Select(t => int.Parse(t.Trim())).ToArray(), "Rolled scores survive moving between steps.");

        await Page.GetByTestId("ability-method").SelectOptionAsync("Manual entry");
        await Expect(Page.Locator(".luf-panel .ability-editor-grid input:not(:disabled)")).ToHaveCountAsync(6);
        foreach (var (ability, value) in new[] { ("Strength", 15), ("Dexterity", 12), ("Constitution", 14), ("Intelligence", 10), ("Wisdom", 10), ("Charisma", 8) })
            await SetManualScoreAsync(ability, value);

        await Ui.StepAsync("Background");
        await Ui.ChooseAsync("primary_ability", "Strength");
        await Ui.ChooseAsync("secondary_ability", "Constitution");
        await Ui.MultiAsync("background_options_tools", "Dice Set");
        await Ui.StepAsync("Species");
        await Ui.MultiAsync("species_skills", "Perception");
        await Ui.MultiAsync("species_origin_feats", "Tough");
        await Ui.StepAsync("Class");
        await Ui.MultiAsync("class_skills", "Acrobatics");
        await Ui.MultiAsync("class_skills", "Survival");
        await Ui.ChooseAsync("fighting_style", "Defense");
        await Ui.StepAsync("Equipment");
        await Ui.ChooseAsync("class_options_equipment", "A");
        await Ui.ChooseAsync("background_options_equipment", "A");
        foreach (var weapon in new[] { "Longsword", "Longbow", "Dagger" }) await Ui.MultiAsync("weapon_masteries", weapon);
        await CommitLevelAsync(1);
        var hp = (await Ui.HpAsync()).Split(" / ");
        Assert.AreEqual(hp[1], hp[0], "A new character starts at full health.");

        await StartLevelUpAsync();
        await CommitLevelAsync(2);

        await StartLevelUpAsync();
        await SelectInPanelAsync("Champion");
        await Expect(Page.Locator(".choice-editor[data-choice-value='\"Champion\"']")).ToHaveCountAsync(1);
        await CommitLevelAsync(3);

        await StartLevelUpAsync();
        await Ui.StepAsync("Abilities");
        await Page.Locator(".luf-panel").GetByRole(AriaRole.Button, new() { Name = "Choose a feat" }).ClickAsync();
        await SelectInPanelAsync("Skill Expert");
        await SelectInPanelAsync("Dexterity");
        await Expect(Page.Locator(".choice-editor[data-choice-value='\"Dexterity\"']").First).ToBeVisibleAsync();
        await SelectInPanelAsync("Insight");
        await HeroholdUi.UntilAsync(async () => await Page.Locator(".luf-panel select").Filter(new() { Has = Page.Locator("option", new() { HasText = "Insight" }) }).CountAsync() >= 2, "Expertise picker");
        await SelectInPanelAsync("Insight", last: true);
        await HeroholdUi.UntilAsync(async () => await Page.Locator(".choice-editor[data-choice-value='\"Insight\"']").CountAsync() >= 2, "Skill Expert proficiency and expertise");

        // The feat's additional Weapon Mastery takes any eligible weapon.
        await Ui.StepAsync("Equipment");
        var open = Page.Locator(".choice-editor:not([data-choice-status=resolved]) .choice-options input[type=checkbox]:not(:disabled)").First;
        await open.ClickAsync();
        await Expect(Page.Locator(".workflow-summary")).ToContainTextAsync("0 remaining");
        await CommitLevelAsync(4);
    }

    [TestMethod]
    public async Task ExpertiseOnASkillTheRogueNoLongerHasMustBeRepairedBeforeSaving()
    {
        await StartCreationAsync("Fictional Expertise Rogue");
        await Ui.ChooseAsync("class", "Rogue");
        await Ui.ChooseAsync("species", "Human");
        await Ui.ChooseAsync("background", "Soldier");
        await Ui.StepAsync("Origin");
        await Ui.MultiAsync("origin_languages", "Elvish");
        await Ui.MultiAsync("origin_languages", "Dwarvish");
        await Ui.StepAsync("Abilities");
        foreach (var (ability, value) in new[] { ("Strength", 10), ("Dexterity", 15), ("Constitution", 12), ("Intelligence", 10), ("Wisdom", 14), ("Charisma", 8) })
            await SetManualScoreAsync(ability, value);
        await Ui.StepAsync("Background");
        await Ui.ChooseAsync("primary_ability", "Dexterity");
        await Ui.ChooseAsync("secondary_ability", "Constitution");
        await Ui.MultiAsync("background_options_tools", "Dice Set");
        await Ui.StepAsync("Species");
        await Ui.MultiAsync("species_skills", "Perception");
        await Ui.MultiAsync("species_origin_feats", "Alert");
        await Ui.StepAsync("Class");
        foreach (var skill in new[] { "Acrobatics", "Insight", "Investigation", "Stealth" }) await Ui.MultiAsync("class_skills", skill);
        await Ui.ChooseAsync("rogue_thieves_cant_language", "Draconic");
        await Expect(Ui.Field("expertise")).ToContainTextAsync("Choose 2");
        Assert.IsTrue(await Ui.ValueAsync("expertise") is null or JsonArray { Count: 0 }, "A new Rogue chooses Expertise explicitly.");
        await Ui.MultiAsync("expertise", "Insight");
        await Expect(Ui.Field("expertise")).Not.ToHaveAttributeAsync("data-choice-status", "resolved");
        await Ui.MultiAsync("expertise", "Stealth");

        // Dropping a trained skill invalidates its Expertise.
        await Ui.MultiAsync("class_skills", "Insight", selected: false);
        await Ui.MultiAsync("class_skills", "Deception");
        await Expect(Ui.Field("expertise")).ToHaveAttributeAsync("data-choice-status", "unresolved");
        await Ui.StepAsync("Equipment");
        await Ui.ChooseAsync("class_options_equipment", "A");
        await Ui.ChooseAsync("background_options_equipment", "A");
        await Ui.MultiAsync("weapon_masteries", "Dagger");
        await Ui.MultiAsync("weapon_masteries", "Shortsword");
        await Ui.StepAsync("Review & confirm");
        await Expect(Page.GetByTestId("commit-workflow")).ToBeDisabledAsync();
        await Expect(Page.Locator(".luf-panel")).ToContainTextAsync("proficient");

        await Ui.StepAsync("Class");
        await Ui.MultiAsync("expertise", "Insight", selected: false);
        await Ui.MultiAsync("expertise", "Investigation");
        await Ui.CommitAsync();
        await Expect(Ui.Header).ToContainTextAsync("Fictional Expertise Rogue");

        await Ui.AllSkillsAsync();
        foreach (var (skill, total) in new[] { ("Stealth", "+7"), ("Investigation", "+4"), ("Acrobatics", "+5"), ("Insight", "+2") })
            Assert.AreEqual(total, await Ui.SkillAsync(skill), skill);
        Assert.IsTrue((await Ui.SkillTermsAsync("Stealth")).Any(t => t.Label.Contains("Expertise") && t.Value == "+4"));
        Assert.IsFalse((await Ui.SkillTermsAsync("Insight")).Any(t => t.Label.Contains("Expertise")));
        var exported = await Ui.ExportJsonAsync();
        CollectionAssert.AreEquivalent(new[] { "Stealth", "Investigation" }, exported["expertise"]!.AsArray().Select(v => v!.GetValue<string>()).ToArray());
        var trained = exported["skill_proficiencies"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
        CollectionAssert.DoesNotContain(trained, "Insight");
        CollectionAssert.Contains(trained, "Deception");
        await Page.Keyboard.PressAsync("Escape");
        await Ui.ReloadAsync();
        await Ui.AllSkillsAsync();
        Assert.AreEqual("+7", await Ui.SkillAsync("Stealth"));
        Assert.AreEqual("+2", await Ui.SkillAsync("Insight"));
    }

    [TestMethod]
    public async Task IncompleteEditsAndMissingSubclassImportsStayDraftsUntilRepaired()
    {
        await Ui.UploadAsync(await Fixtures.PathAsync("fighter.json"));
        await Expect(Ui.Header).ToContainTextAsync("Fictional Astra");
        Assert.AreEqual("14 / 14", await Ui.HpAsync());
        // The header also shows the save status, which arrives after the import; compare it once settled.
        await Expect(Page.GetByTestId("save-status")).ToHaveTextAsync("Saved");
        var original = await Ui.Header.TextContentAsync();

        await Ui.Header.Locator(".player-name").ClickAsync();
        await Page.Locator("#details-dialog").GetByRole(AriaRole.Button, new() { Name = "Edit character →" }).ClickAsync();
        await Ui.StepAsync("Background");
        Assert.AreEqual("Strength", (await Ui.ValueAsync("primary_ability"))!.GetValue<string>());
        Assert.AreEqual("Constitution", (await Ui.ValueAsync("secondary_ability"))!.GetValue<string>());
        await Ui.ChooseAsync("primary_ability", "Constitution");
        Assert.AreEqual("Constitution", (await Ui.ValueAsync("secondary_ability"))!.GetValue<string>(), "The conflict is kept for the player to repair.");
        Assert.AreEqual(original, await Ui.Header.TextContentAsync());
        await Ui.StepAsync("Review & confirm");
        await Expect(Page.GetByTestId("commit-workflow")).ToBeDisabledAsync();
        await Expect(Page.Locator(".luf-panel")).ToContainTextAsync("Choose a different secondary background ability.");
        await Ui.StepAsync("Background");
        await Ui.ChooseAsync("secondary_ability", "Dexterity");
        await Ui.StepAsync("Review & confirm");
        await Expect(Page.GetByTestId("commit-workflow")).ToBeEnabledAsync();
        Assert.AreEqual("14 → 15", await Ui.ReviewValueAsync("Maximum HP"));
        Assert.AreEqual("14", await Ui.ReviewValueAsync("Current HP"));
        Assert.AreEqual("14 / 14", await Ui.HpAsync(), "The preview still needs confirmation.");
        await Ui.CommitAsync();
        Assert.AreEqual("14 / 15", await Ui.HpAsync(), "More Constitution raises maximum HP without healing.");
        var repaired = await Ui.ExportJsonAsync();
        Assert.AreEqual("Constitution", repaired["background_options"]!["primary_ability"]!.GetValue<string>());
        Assert.AreEqual("Dexterity", repaired["background_options"]!["secondary_ability"]!.GetValue<string>());
        await Page.Keyboard.PressAsync("Escape");

        original = await Ui.Header.TextContentAsync();
        await Ui.ReviewUploadAsync(await Fixtures.PathAsync("fighter3-missing-subclass.json"));
        Assert.AreEqual(original, await Ui.Header.TextContentAsync());
        await Ui.StepAsync("Review & confirm");
        await Expect(Page.GetByTestId("commit-workflow")).ToBeDisabledAsync();
        await Expect(Page.Locator(".luf-panel")).ToContainTextAsync("ubclass");
        await Ui.StepAsync("Class");
        Assert.IsTrue(await Ui.ValueAsync("subclass") is null || (await Ui.ValueAsync("subclass"))!.GetValue<string>() == "");
        await Ui.ChooseAsync("subclass", "Champion");
        await Ui.StepAsync("Review & confirm");
        await Expect(Page.GetByTestId("commit-workflow")).ToBeEnabledAsync();
        Assert.AreEqual(original, await Ui.Header.TextContentAsync(), "Resolving import choices does not switch characters early.");
        Assert.AreEqual("Champion", await Ui.ReviewValueAsync("Subclass"));
        await Ui.CommitAsync();
        await Expect(Ui.Header).ToContainTextAsync("Fictional Missing Subclass");
        await Expect(Ui.Header).ToContainTextAsync("Fighter 3");
        await Expect(Ui.Header).ToContainTextAsync("Champion");
        var adopted = await Ui.ExportJsonAsync();
        Assert.AreEqual(3, adopted["level"]!.GetValue<int>());
        Assert.AreEqual("Champion", adopted["subclass"]!.GetValue<string>());
        await Page.Keyboard.PressAsync("Escape");
        await Ui.ReloadAsync();
        await Expect(Ui.Header).ToContainTextAsync("Fictional Missing Subclass");
    }
}
