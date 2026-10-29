using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Browser.Tests;

[TestCategory("RulesData")]
[TestClass]
public sealed class SmokeTests : BrowserTest
{
    private static HeroholdHost host = default!;

    [ClassInitialize]
    public static void Start(TestContext _) { host = new HeroholdHost(); host.StartServer(); }

    [ClassCleanup]
    public static async Task Stop() { await StopBrowserAsync(); host?.Dispose(); }

    [TestMethod]
    public async Task SheetLoadsWithAllSkills()
    {
        await Page.GotoAsync(host.BaseUrl);
        await Expect(Page.GetByTestId("workspace-ready")).ToBeVisibleAsync();
        await Expect(Page.Locator(".adventure-skill")).ToHaveCountAsync(18);
    }
}
