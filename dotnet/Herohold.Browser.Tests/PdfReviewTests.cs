using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Browser.Tests;

/// <summary>An editable PDF without its embedded record is rebuilt from explicit, reviewed choices.</summary>
[TestCategory("RulesData")]
[TestClass]
[DoNotParallelize]
public sealed class PdfReviewTests : HostedTest
{
    [ClassCleanup]
    public static Task StopBrowser() => StopBrowserAsync();

    [TestMethod]
    public async Task AnEditablePdfIsReconstructedFromConfirmedChoicesWithItsEvidenceKeptSeparate()
    {
        await Ui.ReviewUploadAsync(await Fixtures.PathAsync("fighter-editable.pdf"));
        await Ui.StepAsync("Origin");
        await Ui.MultiAsync("import_origin_languages", "Elvish");
        await Ui.MultiAsync("import_origin_languages", "Dwarvish");
        await Ui.StepAsync("Abilities");
        await Expect(Page.Locator(".luf-panel")).ToContainTextAsync("final scores shown on the source");
        await Ui.ConfirmAsync("import_final_scores_confirmed");
        await Ui.StepAsync("Background");
        await Ui.ChooseAsync("import_primary_ability", "Strength");
        await Ui.ChooseAsync("import_secondary_ability", "Constitution");
        await Ui.MultiAsync("import_background_options_tools", "Dice Set");
        await Ui.StepAsync("Species");
        await Ui.MultiAsync("import_species_skills", "Perception");
        await Ui.MultiAsync("import_species_origin_feats", "Tough");
        await Ui.StepAsync("Class");
        await Ui.MultiAsync("import_class_skills", "Acrobatics");
        await Ui.MultiAsync("import_class_skills", "Survival");
        await Ui.ChooseAsync("import_fighting_style", "Defense");
        await Ui.StepAsync("Equipment");
        foreach (var weapon in new[] { "Longsword", "Longbow", "Dagger" }) await Ui.MultiAsync("import_weapon_masteries", weapon);
        await Ui.ConfirmAsync("import_equipment_confirmed");

        // Earlier confirmations survive the review being regenerated.
        await Ui.StepAsync("Abilities");
        await Expect(Ui.Field("import_final_scores_confirmed")).ToHaveAttributeAsync("data-choice-value", "true");
        await Ui.ReviewAsync();
        await Expect(Page.GetByTestId("commit-workflow")).ToBeEnabledAsync();
        await Expect(Page.GetByTestId("import-evidence")).Not.ToHaveAttributeAsync("open", "");
        await Expect(Page.GetByTestId("import-evidence").Locator("summary")).ToContainTextAsync("334");
        await Page.GetByTestId("commit-workflow").ClickAsync();
        await Expect(Page.GetByTestId("character-workflow")).ToHaveCountAsync(0);
        await Expect(Ui.Header).ToContainTextAsync("Fictional Astra");
    }
}
