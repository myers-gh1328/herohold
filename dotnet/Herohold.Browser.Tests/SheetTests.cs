using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Browser.Tests;

/// <summary>The everyday sheet with the fictional sample: layout, journal, inventory, themes and widths.</summary>
[TestCategory("RulesData")]
[TestClass]
[DoNotParallelize]
public sealed class SheetTests : HostedTest
{
    [TestInitialize]
    public async Task ArrangeAsync()
    {
        await Expect(Page.GetByTestId("save-status")).ToHaveTextAsync("Saved");
    }

    [ClassCleanup]
    public static Task StopBrowser() => StopBrowserAsync();

    private Task<T> Js<T>(string expression) => Page.EvaluateAsync<T>(expression);

    [TestMethod]
    public async Task PlayShowsSkillsCountersSpellsAndStatusWithoutExpandingAnything()
    {
        await Expect(Page.Locator(".adventure-skill")).ToHaveCountAsync(18);
        Assert.IsTrue(await Js<bool>("(()=>{const layout=document.querySelector('.adventure-layout'),checks=layout.querySelector('.adventure-checks'),rows=[...checks.querySelectorAll('.adventure-skill')];return checks.getBoundingClientRect().left>layout.firstElementChild.getBoundingClientRect().left&&rows.every(r=>Math.abs(r.getBoundingClientRect().left-rows[0].getBoundingClientRect().left)<1)})()"), "Skills sit in one aligned right-hand column.");
        Assert.IsTrue(await Js<bool>("[...document.querySelectorAll('.adventure-skill')].every(r=>r.getBoundingClientRect().height<=30)"), "Skill rows stay compact.");
        Assert.IsTrue(await Js<bool>("(()=>{const c=[...document.querySelectorAll('.player-resource-bar .resource-counter')];return c.length>0&&c.every(e=>e.getClientRects().length>0)})()"), "Counters are visible without opening a panel.");
        Assert.IsGreaterThan(3, await Page.GetByTestId("adventure-magic").Locator("[data-testid^=cast-]").CountAsync(), "Magic lists every ready spell.");
        await Expect(Page.Locator(".player-resource-bar [data-testid^=resource-slot]")).ToHaveCountAsync(0);
        Assert.IsGreaterThan(0, await Page.Locator("[data-testid^=magic-slot-]").CountAsync(), "Spell slots appear once, in Magic.");
        Assert.IsTrue(await Js<bool>("(()=>{const c=document.querySelector('[data-testid=conditions-panel]').getBoundingClientRect(),d=document.querySelector('[data-testid=defenses-panel]').getBoundingClientRect();return c.height<160&&d.height<160})()"), "Status cards stay compact.");
        await Expect(Page.Locator(".ability-icon")).ToHaveCountAsync(6);
        var perception = Page.GetByTestId("skill-perception");
        await Expect(perception.GetByTestId("active-perception")).ToBeVisibleAsync();
        await Expect(perception.GetByTestId("passive-perception")).ToBeVisibleAsync();
        await Page.GetByTestId("roll-skill-perception").ClickAsync();
        await Expect(Page.Locator("#roll-dialog")).ToBeVisibleAsync();
        await Expect(Page.Locator("#roll-title")).ToContainTextAsync("Perception");
    }

    [TestMethod]
    public async Task JournalNotesAreSavedAsPlainTextAndFilterByCategory()
    {
        var token = "Browser note " + Guid.NewGuid().ToString("N")[..8];
        var category = "Travel " + Guid.NewGuid().ToString("N")[..8];
        await Page.GetByTestId("nav-journal").ClickAsync();
        await Page.Locator(".journal-category-creator summary").ClickAsync();
        await Page.GetByTestId("journal-new-category").FillAsync(category);
        await Page.GetByTestId("journal-create-category").ClickAsync();
        await Expect(Page.Locator($"[data-journal-category=\"{category}\"]")).ToBeVisibleAsync();
        await Page.EvaluateAsync("heroholdUi.closeDialog('journal-sheet-dialog')");

        await Page.GetByTestId("add-note").ClickAsync();
        await Expect(Page.Locator("#quick-note-dialog")).ToBeVisibleAsync();
        await Page.Locator("#note-title").FillAsync(token);
        await Page.Locator("#note-category").SelectOptionAsync(category);
        await Page.Locator("#note-text").FillAsync("Remember the ferry. <img src=x onerror=window.noteUnsafe=true>");
        await Page.GetByTestId("save-note").ClickAsync();
        await Expect(Page.Locator("main")).ToContainTextAsync(token);
        Assert.IsFalse(await Js<bool>("!!window.noteUnsafe"), "Note text must never run as markup.");

        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("workspace-ready")).ToBeVisibleAsync();
        await Expect(Page.Locator("main")).ToContainTextAsync(token);
        await Page.GetByTestId("nav-journal").ClickAsync();
        await Page.GetByTestId("journal-search").FillAsync(token);
        await Expect(Page.Locator(".journal-entry")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".journal-group h2").First).ToContainTextAsync(category);
        await Page.Locator("[data-journal-category=Quests]").ClickAsync();
        await Expect(Page.Locator(".journal-entry")).ToHaveCountAsync(0);
        await Page.Locator($"[data-journal-category=\"{category}\"]").ClickAsync();
        await Expect(Page.Locator(".journal-entry")).ToHaveCountAsync(1);
    }

    [TestMethod]
    public async Task EquippingAnItemChangesItsBonusAndUsingOneSurvivesReload()
    {
        var passive = Page.GetByTestId("passive-perception").Locator(".calculation-trigger");
        await Page.GetByTestId("nav-equipment").ClickAsync();
        var charm = Page.GetByTestId("equip-wayfinder-charm");
        if (await charm.IsCheckedAsync()) await charm.UncheckAsync();
        await Expect(charm).Not.ToBeCheckedAsync();
        await Page.GetByTestId("nav-adventure").ClickAsync();
        await Expect(passive).ToHaveTextAsync("19");
        await Page.GetByTestId("nav-equipment").ClickAsync();
        await charm.CheckAsync();
        await Expect(charm).ToBeCheckedAsync();
        await Page.GetByTestId("nav-adventure").ClickAsync();
        await Expect(passive).ToHaveTextAsync("20");

        await Page.GetByTestId("nav-equipment").ClickAsync();
        await Expect(Page.GetByTestId("quantity-potion")).ToHaveTextAsync("3");
        await Page.GetByTestId("consume-potion").ClickAsync();
        await Expect(Page.Locator("#item-use-dialog")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("quantity-potion")).ToHaveTextAsync("3");
        var facts = Page.GetByTestId("item-use-table-facts");
        if (await facts.CountAsync() > 0) await facts.CheckAsync();
        await Page.GetByTestId("confirm-item-use").ClickAsync();
        await Expect(Page.GetByTestId("quantity-potion")).ToHaveTextAsync("2");
        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("workspace-ready")).ToBeVisibleAsync();
        await Page.GetByTestId("nav-equipment").ClickAsync();
        await Expect(Page.GetByTestId("quantity-potion")).ToHaveTextAsync("2");
    }

    [TestMethod]
    public async Task ThemeFollowsTheChoiceOrTheSystemWithoutSavingTheCharacter()
    {
        var revision = await Page.GetByTestId("revision").TextContentAsync();
        await Page.GetByTestId("open-app-drawer").ClickAsync();
        await Page.GetByTestId("theme").SelectOptionAsync("dark");
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-theme", "dark");
        await Page.GetByTestId("theme").SelectOptionAsync("system");
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light });
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-theme", "light");
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-theme", "dark");
        Assert.AreEqual(revision, await Page.GetByTestId("revision").TextContentAsync(), "Appearance is not a character change.");
    }

    [TestMethod]
    public async Task EveryTabFitsFromPhoneToDesktopWithTheHeaderInView()
    {
        foreach (var width in new[] { 320, 390, 768, 1440 })
        {
            await Page.SetViewportSizeAsync(width, 1000);
            foreach (var tab in new[] { "adventure", "equipment", "companions" })
            {
                await Page.GetByTestId("nav-" + tab).ClickAsync();
                await Expect(Page.GetByTestId("nav-" + tab)).ToHaveAttributeAsync("aria-current", "page");
                Assert.IsTrue(await Js<bool>("document.documentElement.scrollWidth<=innerWidth"), $"{tab} overflows at {width}px");
                Assert.IsTrue(await Js<bool>("document.querySelector('#character-context').getBoundingClientRect().top>=0"), $"Header hidden on {tab} at {width}px");
            }
        }
    }
}
