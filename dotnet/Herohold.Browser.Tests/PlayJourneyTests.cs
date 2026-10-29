using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Browser.Tests;

/// <summary>
/// Table play with the fictional sample ranger (level 5, 44 HP) on a fresh workspace: damage, an
/// upcast heal paid from the chosen slot, staged rests, Hunter's Mark, Lucky and on-hit damage.
/// Physical dice keep every result deterministic.
/// </summary>
[TestCategory("RulesData")]
[TestClass]
[DoNotParallelize]
public sealed class PlayJourneyTests : HostedTest
{
    [TestInitialize]
    public async Task ArrangeAsync()
    {
        await Page.GetByTestId("preferred-dice-mode").SelectOptionAsync("Physical");
        // The sample starts partly spent; a long rest gives every journey full HP and pools.
        await RestAsync("long");
        await Page.GetByTestId("confirm-rest").ClickAsync();
        await Expect(Page.Locator("#rest-dialog")).ToBeHiddenAsync();
        await Expect(Health).ToContainTextAsync("44");
    }

    [ClassCleanup]
    public static Task StopBrowser() => StopBrowserAsync();

    private ILocator Health => Page.Locator("#health-emblem");
    private ILocator Slot(string id) => Page.GetByTestId("magic-slot-" + id).Locator("strong");
    private ILocator Resource(string id) => Page.GetByTestId("resource-" + id);

    private async Task RestAsync(string kind)
    {
        await Page.Locator(".player-header .rest-menu summary").ClickAsync();
        await Page.GetByTestId(kind + "-rest").ClickAsync();
        await Expect(Page.Locator("#rest-dialog")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task DamageIsPreviewedThenSavedAndAnUpcastHealPaysTheChosenSlotOnce()
    {
        await Page.GetByTestId("quick-damage").ClickAsync();
        await Page.Locator("#health-amount").FillAsync("12");
        await Expect(Page.GetByTestId("health-preview")).ToContainTextAsync("32 / 44");
        await Expect(Health).ToContainTextAsync("44");
        await Page.GetByTestId("confirm-health").ClickAsync();
        await Expect(Page.Locator("#health-dialog")).ToBeHiddenAsync();
        await Expect(Health).ToContainTextAsync("32");

        await Page.GetByTestId("adventure-magic").GetByTestId("cast-cure-wounds").ClickAsync();
        await Expect(Page.Locator("#spell-cast-dialog")).ToBeVisibleAsync();
        await Page.Locator("#cast-payment").SelectOptionAsync("slot2");
        await Expect(Page.Locator("#spell-cast-dialog .context-cast-preview")).ToContainTextAsync("4d8");
        await Page.GetByTestId("confirm-cast").ClickAsync();
        await Expect(Page.Locator("#spell-cast-dialog")).ToContainTextAsync("Cast confirmed");
        await Page.Locator("#cast-effect-physical").FillAsync("1,2,3,4");
        await Page.GetByTestId("roll-cast-effect").ClickAsync();
        await Expect(Page.GetByTestId("cast-effect-result").Locator("strong")).ToHaveTextAsync("14");
        await Page.EvaluateAsync("heroholdUi.closeDialog('spell-cast-dialog')");
        await Expect(Slot("slot2")).ToHaveTextAsync("1 / 2");
        await Expect(Slot("slot1")).ToHaveTextAsync("4 / 4");
        await Expect(Health).ToContainTextAsync("32");
    }

    [TestMethod]
    public async Task ShortRestStagesHitDiceAndLongRestRestoresWithoutChangingPreparations()
    {
        await Page.GetByTestId("quick-damage").ClickAsync();
        await Page.Locator("#health-amount").FillAsync("12");
        await Page.GetByTestId("confirm-health").ClickAsync();
        await Expect(Health).ToContainTextAsync("32");

        await RestAsync("short");
        await Page.Locator("#rest-physical-hit-die").FillAsync("4");
        await Page.GetByTestId("stage-hit-die").ClickAsync();
        await Expect(Page.Locator("#rest-dialog .rest-preview")).ToContainTextAsync("38");
        await Expect(Health).ToContainTextAsync("32");
        await Page.GetByTestId("confirm-rest").ClickAsync();
        await Expect(Page.Locator("#rest-dialog")).ToBeHiddenAsync();
        await Expect(Health).ToContainTextAsync("38");
        await Expect(Resource("hitdice")).ToHaveTextAsync("4");

        await RestAsync("long");
        await Expect(Page.Locator("#rest-spell-picker")).ToHaveCountAsync(0);
        await Page.Locator("#rest-change-spells").CheckAsync();
        await Expect(Page.Locator("#rest-spell-picker")).ToBeVisibleAsync();
        await Page.Locator("#rest-change-spells").UncheckAsync();
        await Expect(Page.Locator("#rest-spell-picker")).ToHaveCountAsync(0);
        await Page.GetByTestId("confirm-rest").ClickAsync();
        await Expect(Page.Locator("#rest-dialog")).ToBeHiddenAsync();
        await Expect(Health).ToContainTextAsync("44");
        await Expect(Resource("hitdice")).ToHaveTextAsync("5");
    }

    [TestMethod]
    public async Task HuntersMarkPaysOnceAndLuckyChangesTheAttackBeforeOnHitDamage()
    {
        var mark = Page.GetByTestId("adventure-magic").Locator(".magic-spell").Filter(new() { HasText = "Hunter" });
        await mark.Locator("button.button").ClickAsync();
        await Expect(Page.Locator("#spell-cast-dialog")).ToBeVisibleAsync();
        await Page.Locator("#cast-payment").SelectOptionAsync("hunters");
        await Page.Locator("#cast-target").FillAsync("Sentinel");
        await Page.Locator("#cast-concentration").CheckAsync();
        await Page.GetByTestId("confirm-cast").ClickAsync();
        await Expect(Page.Locator("#spell-cast-dialog")).ToContainTextAsync("Cast confirmed");
        await Expect(Page.Locator("#roll-cast-effect")).ToHaveCountAsync(0);
        await Page.EvaluateAsync("heroholdUi.closeDialog('spell-cast-dialog')");
        await Expect(Resource("hunters")).ToHaveTextAsync("2");

        await Page.GetByTestId("roll-longbow").ClickAsync();
        await Expect(Page.Locator("#roll-dialog")).ToBeVisibleAsync();
        await Page.Locator("#physical-d20").FillAsync("6");
        await Page.GetByTestId("roll-digital").ClickAsync();
        await Expect(Page.GetByTestId("roll-result").Locator("strong")).ToHaveTextAsync("15");
        await Page.Locator("#lucky-extra").FillAsync("18");
        await Page.GetByTestId("use-lucky").ClickAsync();
        await Expect(Page.GetByTestId("roll-result").Locator("strong")).ToHaveTextAsync("27");
        await Page.Locator("#effect-hm").CheckAsync();
        await Page.Locator("#effect-dread").CheckAsync();
        await Page.Locator("#attack-target").FillAsync("Sentinel");
        await Page.Locator("#physical-damage-dice").FillAsync("4,3,2,6");
        await Page.GetByTestId("attack-hit").ClickAsync();
        await Expect(Page.Locator("#roll-dialog")).ToContainTextAsync("Damage confirmed");
        await Expect(Page.Locator("#roll-dialog")).ToContainTextAsync("Psychic");
        await Expect(Page.Locator("#roll-dialog")).ToContainTextAsync("Force");
        await Page.EvaluateAsync("heroholdUi.closeDialog('roll-dialog')");
        await Expect(Resource("dread")).ToHaveTextAsync("3");
        await Expect(Resource("hunters")).ToHaveTextAsync("2");
        await Expect(Resource("luck")).ToHaveTextAsync("2");
    }
}
