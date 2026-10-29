using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Microsoft.Playwright.Assertions;

namespace Herohold.Browser.Tests;

/// <summary>Drives visible controls the way a player does; reads only rendered values.</summary>
public sealed class HeroholdUi(IPage page)
{
    public ILocator Header => page.Locator("#character-context");
    public ILocator Field(string choiceId) => page.Locator($"[data-choice-id={choiceId}]");
    private ILocator Panel => page.Locator(".luf-panel");

    /// <summary>"current / maximum" from the header health emblem.</summary>
    public async Task<string> HpAsync()
    {
        var label = await page.Locator("#health-emblem").GetAttributeAsync("aria-label") ?? "";
        var match = Regex.Match(label, @"Health (\d+) of (\d+)");
        Assert.IsTrue(match.Success, label);
        return $"{match.Groups[1].Value} / {match.Groups[2].Value}";
    }

    public async Task OpenDrawerAsync()
    {
        if (!await page.Locator("#app-drawer").IsVisibleAsync()) await page.GetByTestId("open-app-drawer").ClickAsync();
        await Expect(page.Locator("#app-drawer")).ToBeVisibleAsync();
    }

    public async Task OpenLibraryAsync()
    {
        await OpenDrawerAsync();
        await page.Locator("#app-drawer").GetByRole(AriaRole.Button, new() { Name = "Character library", Exact = true }).ClickAsync();
        await Expect(page.Locator(".library-entry").First).ToBeVisibleAsync();
    }

    public async Task ReviewUploadAsync(string path)
    {
        await OpenDrawerAsync();
        await page.GetByTestId("menu-import").ClickAsync();
        // A person can only choose a file once the previous save has finished and the control is enabled.
        await Expect(page.Locator("#character-upload")).ToBeEnabledAsync();
        await page.Locator("#character-upload").SetInputFilesAsync(path);
        await Expect(page.GetByTestId("character-workflow")).ToBeVisibleAsync();
    }

    public async Task UploadAsync(string path)
    {
        await ReviewUploadAsync(path);
        await CommitAsync();
    }

    public async Task StepAsync(string label)
    {
        var heading = label switch { "Review & confirm" => "Review the whole change.", "Hit points" => "Add hit points.", _ => label };
        var button = page.Locator(".luf-steps button").Filter(new() { HasTextRegex = new Regex(Regex.Escape(label) + @"\s*$") });
        await Expect(button).ToBeEnabledAsync();
        await button.ClickAsync();
        await Expect(Panel.Locator("h2")).ToHaveTextAsync(heading);
    }

    public async Task ReviewAsync()
    {
        var last = page.Locator(".luf-steps li:last-child button");
        await Expect(last).ToBeEnabledAsync();
        await last.ClickAsync();
        await Expect(Panel.Locator("h2")).ToContainTextAsync("Review");
    }

    public async Task CommitAsync()
    {
        await ReviewAsync();
        await Expect(page.GetByTestId("commit-workflow")).ToBeEnabledAsync();
        await page.GetByTestId("commit-workflow").ClickAsync();
        await Expect(page.GetByTestId("character-workflow")).ToHaveCountAsync(0);
    }

    public async Task<JsonNode?> ValueAsync(string choiceId) =>
        JsonNode.Parse(await Field(choiceId).GetAttributeAsync("data-choice-value") ?? "null");

    public async Task ChooseAsync(string choiceId, string label)
    {
        var select = Field(choiceId).Locator("select");
        await Expect(select).ToBeEnabledAsync();
        await select.SelectOptionAsync(new SelectOptionValue { Label = label });
        await Expect(Field(choiceId)).ToHaveAttributeAsync("data-choice-value", JsonSerializer.Serialize(label));
    }

    public async Task MultiAsync(string choiceId, string label, bool selected = true)
    {
        var option = Field(choiceId).Locator(".choice-option").Filter(new() { Has = page.Locator("strong", new() { HasTextRegex = new Regex("^" + Regex.Escape(label) + "$") }) });
        var input = option.Locator("input");
        await Expect(input).ToBeEnabledAsync();
        Assert.AreNotEqual(selected, await input.IsCheckedAsync(), $"{choiceId}: {label} is already in the requested state");
        await input.ClickAsync();
        await UntilAsync(async () => (await ValueAsync(choiceId)) is JsonArray values ? values.Any(v => v?.GetValue<string>() == label) == selected : !selected, $"{choiceId}: {label} selected={selected}");
    }

    public async Task ConfirmAsync(string choiceId)
    {
        var box = Field(choiceId).Locator("input[type=checkbox]");
        await Expect(box).ToBeEnabledAsync();
        await box.ClickAsync();
        await Expect(Field(choiceId)).ToHaveAttributeAsync("data-choice-value", "true");
    }

    public async Task<string?> ReviewValueAsync(string label) =>
        await Panel.Locator(".change-row").Filter(new() { Has = page.Locator("span", new() { HasTextRegex = new Regex("^" + Regex.Escape(label) + "$") }) }).Locator("strong").First.TextContentAsync() is { } text ? text.Trim() : null;

    public async Task AllSkillsAsync()
    {
        await page.GetByTestId("nav-adventure").ClickAsync();
        await Expect(page.Locator(".skill-row")).ToHaveCountAsync(18);
    }

    private ILocator SkillRow(string name) => page.GetByTestId("skill-" + name.ToLowerInvariant().Replace(' ', '-'));

    public async Task<string> SkillAsync(string name) => (await SkillRow(name).Locator(".skill-calculation-total").TextContentAsync())!.Trim();

    public async Task<List<(string Label, string Value)>> SkillTermsAsync(string name)
    {
        await SkillRow(name).Locator(".skill-calculation-total").ClickAsync();
        var popover = page.Locator($".calculation-popover:popover-open[aria-label=\"{name} check bonus calculation\"]");
        await Expect(popover).ToBeVisibleAsync();
        var terms = new List<(string, string)>();
        foreach (var row in await popover.Locator(".calculation-row").AllAsync())
            terms.Add(((await row.Locator("span").First.TextContentAsync())!, (await row.Locator("strong").First.TextContentAsync())!));
        // The trigger reopens on hover and focus, so move away before closing.
        await page.Mouse.MoveAsync(0, 0);
        await popover.GetByRole(AriaRole.Button, new() { Name = "Close calculation" }).ClickAsync();
        await page.Mouse.MoveAsync(0, 0);
        await Expect(popover).ToHaveCountAsync(0);
        return terms;
    }

    public async Task<string> ExportAsync(string testId)
    {
        await OpenDrawerAsync();
        var download = await page.RunAndWaitForDownloadAsync(() => page.GetByTestId(testId).ClickAsync());
        var path = Path.Combine(HeroholdHost.Root, ".tmp", "herohold-ui-downloads", Guid.NewGuid().ToString("N") + Path.GetExtension(download.SuggestedFilename));
        await download.SaveAsAsync(path);
        return path;
    }

    public async Task<JsonObject> ExportJsonAsync() => JsonNode.Parse(await File.ReadAllTextAsync(await ExportAsync("export-json")))!.AsObject();

    public async Task OpenLevelUpAsync()
    {
        await Header.Locator(".player-name").ClickAsync();
        await Expect(page.Locator("#details-dialog")).ToBeVisibleAsync();
        await page.GetByTestId("open-level-up").ClickAsync();
        // Level-up first asks which class to advance; the current class is preselected.
        await Expect(page.GetByTestId("confirm-level-class")).ToBeEnabledAsync();
        await page.GetByTestId("confirm-level-class").ClickAsync();
        await Expect(page.GetByTestId("character-workflow")).ToBeVisibleAsync();
    }

    /// <summary>Retries a rendered-state condition for up to 15 seconds.</summary>
    public static async Task UntilAsync(Func<Task<bool>> condition, string description)
    {
        for (var deadline = DateTime.UtcNow.AddSeconds(15); DateTime.UtcNow < deadline; await Task.Delay(100))
            if (await condition()) return;
        Assert.Fail("Timed out waiting for " + description);
    }

    public async Task ReloadAsync()
    {
        await page.ReloadAsync();
        await Expect(page.GetByTestId("workspace-ready")).ToBeVisibleAsync();
    }
}
