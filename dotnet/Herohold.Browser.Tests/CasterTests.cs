using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Browser.Tests;

/// <summary>Pact slots, cantrips, book rituals and upcasting, plus per-tab character selection.</summary>
[TestCategory("RulesData")]
[TestClass]
[DoNotParallelize]
public sealed class CasterTests : HostedTest
{
    [ClassCleanup]
    public static Task StopBrowser() => StopBrowserAsync();

    private ILocator Slot(string id) => Page.GetByTestId("magic-slot-" + id).Locator("strong");

    /// <summary>Reads the spell in the reference first (spending nothing), then casts from there.</summary>
    private async Task CastAsync(string name)
    {
        await Page.GetByTestId("nav-adventure").ClickAsync();
        var revision = await Page.GetByTestId("revision").TextContentAsync();
        await Page.GetByTestId("open-spell-library").ClickAsync();
        await Page.Locator(".spell-reference-row").Filter(new() { Has = Page.Locator("strong", new() { HasTextRegex = new($"^{System.Text.RegularExpressions.Regex.Escape(name)}$") }) }).Locator("button.roll-row-action").ClickAsync();
        await Expect(Page.Locator("#spell-reference-dialog")).ToBeVisibleAsync();
        await Expect(Page.Locator("#spell-reference-title")).ToHaveTextAsync(name);
        Assert.AreEqual(revision, await Page.GetByTestId("revision").TextContentAsync(), "Reading a spell spends nothing.");
        await Page.GetByTestId("cast-inspected-spell").ClickAsync();
        await Expect(Page.Locator("#spell-reference-dialog")).ToBeHiddenAsync();
        await Expect(Page.Locator("#spell-cast-dialog")).ToBeVisibleAsync();
    }

    private async Task ConfirmCastAsync()
    {
        await Page.GetByTestId("confirm-cast").ClickAsync();
        await Expect(Page.Locator("#spell-cast-dialog")).ToContainTextAsync("Cast confirmed");
    }

    /// <summary>Closes the cast and the spell reference list it was opened from.</summary>
    private async Task CloseCastAsync()
    {
        await Page.EvaluateAsync("heroholdUi.closeDialog('spell-cast-dialog')");
        await Page.Locator("#spell-library-dialog").GetByRole(AriaRole.Button, new() { Name = "Close spell reference list" }).ClickAsync();
        await Expect(Page.Locator("#spell-library-dialog")).ToBeHiddenAsync();
    }

    [TestMethod]
    public async Task PactSlotsPayForWarlockSpellsAndReturnOnAShortRest()
    {
        await Ui.UploadAsync(await Fixtures.PathAsync("warlock.json"));
        await CastAsync("Armor of Agathys");
        await Expect(Page.Locator("#cast-payment option[value=pact]")).ToHaveCountAsync(1);
        await Page.Locator("#cast-payment").SelectOptionAsync("pact");
        await ConfirmCastAsync();
        await CloseCastAsync();
        await Expect(Slot("pact")).ToHaveTextAsync("1 / 2");
        await Page.Locator(".player-header .rest-menu summary").ClickAsync();
        await Page.GetByTestId("short-rest").ClickAsync();
        await Page.GetByTestId("confirm-rest").ClickAsync();
        await Expect(Page.Locator("#rest-dialog")).ToBeHiddenAsync();
        await Expect(Slot("pact")).ToHaveTextAsync("2 / 2");
    }

    [TestMethod]
    public async Task CantripsAndBookRitualsUseNoSlotAndTheSelectedCharacterIsPerTab()
    {
        await Page.GetByTestId("preferred-dice-mode").SelectOptionAsync("Physical");
        await Ui.UploadAsync(await Fixtures.PathAsync("warlock.json"));
        await Ui.UploadAsync(await Fixtures.PathAsync("wizard.json"));
        await CastAsync("Acid Splash");
        await Expect(Page.Locator("#cast-payment")).ToHaveValueAsync("none");
        await ConfirmCastAsync();
        await Page.Locator("#cast-effect-physical").FillAsync("3");
        await Page.GetByTestId("roll-cast-effect").ClickAsync();
        await Expect(Page.GetByTestId("cast-effect-result").Locator("strong")).ToHaveTextAsync("3");
        await CloseCastAsync();
        await Expect(Slot("slot1")).ToHaveTextAsync("2 / 2");

        await CastAsync("Comprehend Languages");
        await Expect(Page.Locator("#cast-ritual")).ToBeCheckedAsync();
        await Expect(Page.Locator("#cast-payment")).ToHaveValueAsync("none");
        await ConfirmCastAsync();
        await CloseCastAsync();
        await Expect(Slot("slot1")).ToHaveTextAsync("2 / 2");

        await Ui.ReloadAsync();
        await Expect(Ui.Header).ToContainTextAsync("Fictional Wizard");

        // Another tab picking a different character does not change this tab after refresh.
        var second = await Page.Context.NewPageAsync();
        await second.GotoAsync(Host.BaseUrl);
        await Expect(second.GetByTestId("workspace-ready")).ToBeVisibleAsync();
        var other = new HeroholdUi(second);
        await other.OpenLibraryAsync();
        await second.Locator(".library-entry").Filter(new() { Has = second.Locator("h2", new() { HasTextString = "Fictional Warlock" }) }).Locator("button[data-character-id]").ClickAsync();
        await Expect(other.Header).ToContainTextAsync("Fictional Warlock");
        await second.CloseAsync();
        await Ui.ReloadAsync();
        await Expect(Ui.Header).ToContainTextAsync("Fictional Wizard");
    }

    [TestMethod]
    public async Task AFourthLevelSlotUpcastsFireballAndPaysOnlyThatSlot()
    {
        await Page.GetByTestId("preferred-dice-mode").SelectOptionAsync("Physical");
        await Ui.UploadAsync(await Fixtures.PathAsync("wizard7.json"));
        await CastAsync("Fireball");
        await Page.Locator("#cast-payment").SelectOptionAsync("slot4");
        var preview = Page.Locator("#spell-cast-dialog .context-cast-preview");
        await Expect(preview).ToContainTextAsync("9d6");
        await Expect(preview).ToContainTextAsync("Fire");
        await Expect(preview).ToContainTextAsync("20-ft radius");
        await Expect(preview).ToContainTextAsync("Dexterity save against DC");
        await ConfirmCastAsync();
        await Page.Locator("#cast-effect-physical").FillAsync("1,2,3,4,5,6,1,2,3");
        await Page.GetByTestId("roll-cast-effect").ClickAsync();
        await Expect(Page.GetByTestId("cast-effect-result").Locator("strong")).ToHaveTextAsync("27");
        await CloseCastAsync();
        await Expect(Slot("slot4")).ToHaveTextAsync("0 / 1");
        await Expect(Slot("slot3")).ToHaveTextAsync("3 / 3");
    }
}
