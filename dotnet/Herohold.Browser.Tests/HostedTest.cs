using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Browser.Tests;

/// <summary>Each test gets its own Herohold host over an empty workspace, opened on the sample character.</summary>
public abstract class HostedTest : BrowserTest
{
    protected HeroholdHost Host { get; private set; } = default!;
    protected HeroholdUi Ui { get; private set; } = default!;

    [TestInitialize]
    public async Task StartHostAsync()
    {
        Host = new HeroholdHost();
        Host.StartServer();
        Ui = new HeroholdUi(Page);
        await Page.GotoAsync(Host.BaseUrl);
        await Expect(Page.GetByTestId("workspace-ready")).ToBeVisibleAsync();
    }

    [TestCleanup]
    public void StopHost() => Host.Dispose();
}
