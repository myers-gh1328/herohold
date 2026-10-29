using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Browser.Tests;

/// <summary>Imports, detached edits, level-up, exports, the library and worn armor.</summary>
[TestCategory("RulesData")]
[TestClass]
[DoNotParallelize]
public sealed class CharacterLibraryTests : HostedTest
{
    [ClassCleanup]
    public static Task StopBrowser() => StopBrowserAsync();

    [TestMethod]
    public async Task ImportEditLevelUpExportAndReimportKeepEveryCharacterInTheLibrary()
    {
        await Ui.ReviewUploadAsync(await Fixtures.PathAsync("fighter.json"));
        await Expect(Ui.Header).ToContainTextAsync("Veyra Thorne");
        await Ui.CommitAsync();
        await Expect(Ui.Header).ToContainTextAsync("Fictional Astra");
        await Expect(Ui.Header).ToContainTextAsync("Fighter 1");

        // A detached edit changes only the draft until it is confirmed.
        await Ui.Header.Locator(".player-name").ClickAsync();
        await Expect(Page.Locator("#details-dialog")).ToBeVisibleAsync();
        await Page.Locator("#details-dialog").GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = "Edit character →" }).ClickAsync();
        await Ui.Field("name").Locator("input").FillAsync("UI Astra");
        await Ui.Field("name").Locator("input").PressAsync("Tab");
        await Expect(Ui.Field("name")).ToHaveAttributeAsync("data-choice-value", "\"UI Astra\"");
        await Expect(Ui.Header).ToContainTextAsync("Fictional Astra");
        await Ui.CommitAsync();
        await Expect(Ui.Header).ToContainTextAsync("UI Astra");

        // Level 1 to 2 with a rolled hit-point result previewed before saving.
        await Ui.OpenLevelUpAsync();
        await Page.GetByTestId("workflow-next").ClickAsync();
        await Expect(Page.Locator(".luf-panel h2")).ToHaveTextAsync("Add hit points.");
        await Page.Locator("input[name=hp-method][value=rolled]").CheckAsync();
        await Page.Locator("#level-hp-roll").FillAsync("3");
        await Page.GetByTestId("workflow-next").ClickAsync();
        await Expect(Page.Locator(".luf-panel h2")).ToContainTextAsync("Review");
        await Expect(Ui.Header).ToContainTextAsync("Fighter 1");
        await Page.GetByTestId("commit-workflow").ClickAsync();
        await Expect(Ui.Header).ToContainTextAsync("Fighter 2");

        var exported = await Ui.ExportJsonAsync();
        Assert.AreEqual("UI Astra", exported["name"]!.GetValue<string>());
        Assert.AreEqual(2, exported["level"]!.GetValue<int>());
        var pdf = await Ui.ExportAsync("export-pdf");
        CollectionAssert.AreEqual("%PDF-"u8.ToArray(), File.ReadAllBytes(pdf)[..5]);

        await Page.Keyboard.PressAsync("Escape");
        await Ui.UploadAsync(pdf);
        await Expect(Ui.Header).ToContainTextAsync("UI Astra");
        await Ui.OpenLibraryAsync();
        Assert.IsGreaterThanOrEqualTo(3, await Page.Locator(".library-entry").CountAsync(), "Source, re-import and sample all stay in the library.");
        await Page.Locator("[data-character-id=veyra-thorne]").ClickAsync();
        await Expect(Ui.Header).ToContainTextAsync("Veyra Thorne");
    }

    [TestMethod]
    public async Task CarriedArmorStaysInactiveAndWornArmorSetsArmorClass()
    {
        var armorClass = Page.Locator(".ac-emblem strong");
        var hp = await Ui.HpAsync();
        var speed = await Page.Locator(".player-travel span").Filter(new() { HasText = "Travel" }).TextContentAsync();
        await Page.GetByTestId("nav-equipment").ClickAsync();
        var studded = Page.GetByTestId("equip-studded-leather");
        if (!await studded.IsCheckedAsync()) await studded.CheckAsync();
        await Expect(armorClass).ToContainTextAsync("16");

        await Page.Locator(".inventory-view").GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = "Add item +" }).ClickAsync();
        await Expect(Page.Locator("#equipment-dialog")).ToBeVisibleAsync();
        await Page.Locator("#item-name").FillAsync("Chain Mail");
        await Page.Locator("#equipment-dialog .button.primary").ClickAsync();
        await Expect(Page.Locator("#equipment-dialog")).ToBeHiddenAsync();
        await Expect(armorClass).ToContainTextAsync("16");

        await studded.UncheckAsync();
        await Expect(armorClass).ToContainTextAsync("14");
        await studded.CheckAsync();
        await Expect(armorClass).ToContainTextAsync("16");
        Assert.AreEqual(hp, await Ui.HpAsync());
        await Page.GetByTestId("nav-adventure").ClickAsync();
        Assert.AreEqual(speed, await Page.Locator(".player-travel span").Filter(new() { HasText = "Travel" }).TextContentAsync());
    }

    [TestMethod]
    public async Task CastingRestingLevelUpAndImportAreReachable()
    {
        await Expect(Page.GetByTestId("adventure-magic").GetByTestId("cast-cure-wounds")).ToBeVisibleAsync();
        await Page.Locator(".player-header .rest-menu summary").ClickAsync();
        await Expect(Page.GetByTestId("short-rest")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("long-rest")).ToBeVisibleAsync();
        await Ui.Header.Locator(".player-name").ClickAsync();
        await Expect(Page.GetByTestId("open-level-up")).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Ui.OpenDrawerAsync();
        await Expect(Page.GetByTestId("menu-import")).ToBeVisibleAsync();
    }
}
