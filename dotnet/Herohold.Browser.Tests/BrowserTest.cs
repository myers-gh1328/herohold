using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Browser.Tests;

/// <summary>
/// One headless Chromium per test class and a fresh page per test. Page script errors fail the test.
/// Run <c>pwsh bin/Debug/net10.0/playwright.ps1 install chromium</c> once per machine.
/// </summary>
public abstract class BrowserTest
{
    private static IPlaywright? playwright;
    private static IBrowser? browser;
    private readonly List<string> pageErrors = [];
    private IBrowserContext context = default!;
    protected IPage Page { get; private set; } = default!;
    public TestContext TestContext { get; set; } = default!;

    protected static async Task StartBrowserAsync()
    {
        playwright ??= await Playwright.CreateAsync();
        browser ??= await playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    protected static async Task StopBrowserAsync()
    {
        if (browser is not null) await browser.CloseAsync();
        playwright?.Dispose();
        (browser, playwright) = (null, null);
    }

    [TestInitialize]
    public async Task OpenPageAsync()
    {
        await StartBrowserAsync();
        context = await browser!.NewContextAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
        context.SetDefaultTimeout(15_000);
        Page = await context.NewPageAsync();
        Page.PageError += (_, error) => pageErrors.Add(error);
    }

    [TestCleanup]
    public async Task ClosePageAsync()
    {
        if (TestContext.CurrentTestOutcome != UnitTestOutcome.Passed)
        {
            var folder = Path.Combine(HeroholdHost.Root, ".tmp", "browser-test-failures");
            Directory.CreateDirectory(folder);
            await Page.ScreenshotAsync(new() { Path = Path.Combine(folder, TestContext.TestName + ".png"), FullPage = true });
        }
        await context.CloseAsync();
        Assert.AreEqual(0, pageErrors.Count, "Page script errors: " + string.Join(" | ", pageErrors));
    }

    protected static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);
    protected static IPageAssertions Expect(IPage page) => Assertions.Expect(page);
}
