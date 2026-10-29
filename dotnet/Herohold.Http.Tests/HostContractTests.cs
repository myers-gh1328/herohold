using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Herohold.Web.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.DependencyInjection;
using Herohold.Core;
using Herohold.Web.Services;

namespace Herohold.Http.Tests;

[TestClass, DoNotParallelize]
public sealed class HostContractTests
{
    [TestCategory("RulesData")]
    [TestMethod]
    public void RealHostResolvesNativeCharacterOperationsWithNoCompatibilityRuntimeDependency()
    {
        using var scope=factory.Services.CreateScope();var rules=scope.ServiceProvider.GetRequiredService<ICharacterRules>();Assert.IsInstanceOfType<CharacterRules>(rules);
        var draft=rules.Create();Assert.IsFalse(draft.Validation.Complete);Assert.IsTrue(draft.Choices.Count>=5);
        Assert.IsFalse(typeof(App).Assembly.GetReferencedAssemblies().Any(a=>a.Name=="Herohold.Compatibility"));
    }
    private static HeroholdFactory factory = default!;
    private static HttpClient client = default!;
    [ClassInitialize]
    public static void Start(TestContext _)
    {
        factory = new();
        client = factory.CreateClient(new() { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
    }
    [ClassCleanup]
    public static void Stop() { client?.Dispose();factory?.Dispose(); }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task HealthHasAnExplicitLivenessContractWithoutLeakingConfiguration()
    {
        var response = await client.GetAsync("/healthz");Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.AreEqual("alive", json!["status"]!.GetValue<string>());Assert.AreEqual("herohold-dotnet", json["application"]!.GetValue<string>());
        Assert.AreEqual(2, json.Count);Assert.AreEqual("application/json", response.Content.Headers.ContentType!.MediaType);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task ProductionRootAndStaticCssLoadUsingAnIsolatedEmptyLibrary()
    {
        var root = await client.GetAsync("/");Assert.AreEqual(HttpStatusCode.OK, root.StatusCode);
        var html = await root.Content.ReadAsStringAsync();StringAssert.Contains(html, "blazor.web.js");StringAssert.Contains(html, "<html lang=\"en\">");
        Assert.IsFalse(html.Contains(factory.DataDirectory, StringComparison.Ordinal));
        var css = await client.GetAsync("/app.css");Assert.AreEqual(HttpStatusCode.OK, css.StatusCode);
        Assert.AreEqual("text/css", css.Content.Headers.ContentType!.MediaType);StringAssert.Contains(await css.Content.ReadAsStringAsync(), "calculation-popover");
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task UnrecognizedHostIsRejectedBeforeHealthOrSignalR()
    {
        foreach (var path in new[] { "/healthz", "/_blazor/negotiate?negotiateVersion=1" })
        {
            using var request = new HttpRequestMessage(path.StartsWith("/_blazor") ? HttpMethod.Post : HttpMethod.Get, path);
            request.Headers.Host = "untrusted.example";
            var response = await client.SendAsync(request);Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, path);
        }
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task SameOriginSignalRNegotiationReturnsTheActualServerTransportContract()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/_blazor/negotiate?negotiateVersion=1");request.Headers.Add("Origin", "http://localhost");
        var response = await client.SendAsync(request);Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.IsFalse(string.IsNullOrWhiteSpace(json!["connectionToken"]!.GetValue<string>()));
        var transports = json["availableTransports"]!.AsArray().Select(n => n!["transport"]!.GetValue<string>()).ToArray();
        CollectionAssert.Contains(transports, "WebSockets");CollectionAssert.Contains(transports, "LongPolling");
    }

    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow("http://localhost.evil.example")]
    [DataRow("null")]
    [DataRow("not an origin")]
    [DataRow("https://herohold.test.evil.example")]
    public async Task CrossOriginRequestsCannotEnterHealthOrSignalR(string origin)
    {
        foreach (var path in new[] { "/healthz", "/_blazor/negotiate?negotiateVersion=1" })
        {
            using var request = new HttpRequestMessage(path.StartsWith("/_blazor") ? HttpMethod.Post : HttpMethod.Get, path);request.Headers.TryAddWithoutValidation("Origin", origin);
            var response = await client.SendAsync(request);Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode, path + " " + origin);
        }
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task ExactConfiguredProxyOriginIsAcceptedButCrossSiteMutationStillFails()
    {
        using var allowed = new HttpRequestMessage(HttpMethod.Get, "/healthz");allowed.Headers.Add("Origin", "https://herohold.test");
        Assert.AreEqual(HttpStatusCode.OK, (await client.SendAsync(allowed)).StatusCode);
        using var crossSite = new HttpRequestMessage(HttpMethod.Post, "/_blazor/negotiate?negotiateVersion=1");crossSite.Headers.Add("Origin", "https://herohold.test");crossSite.Headers.Add("Sec-Fetch-Site", "cross-site");
        Assert.AreEqual(HttpStatusCode.Forbidden, (await client.SendAsync(crossSite)).StatusCode);
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public async Task RazorPagePostWithoutAnAntiforgeryTokenIsRejected()
    {
        var response = await client.PostAsync("/", new FormUrlEncodedContent(new Dictionary<string, string> { ["name"] = "Untrusted mutation" }));
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class HeroholdFactory : WebApplicationFactory<App>
    {
        public string DataDirectory { get; } = Path.Combine(Path.GetTempPath(), "herohold-http-" + Guid.NewGuid().ToString("N"));
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null && !File.Exists(Path.Combine(current.FullName, "dotnet", "Herohold.Web", "Herohold.Web.csproj"))) current = current.Parent;
            var root = current?.FullName ?? throw new InvalidOperationException("Fixed test catalog root was not found.");
            builder.UseContentRoot(Path.Combine(root, "dotnet", "Herohold.Web"));builder.UseEnvironment("Production");
            builder.UseSetting("Herohold:RepositoryRoot", root);builder.UseSetting("Herohold:DataDirectory", DataDirectory);
            builder.UseSetting("Herohold:TrustedOrigins", "https://herohold.test");builder.UseSetting("AllowedHosts", "localhost;127.0.0.1;[::1]");
            builder.UseSetting("urls", "http://127.0.0.1:8510");
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(DataDirectory)) Directory.Delete(DataDirectory, recursive: true);
        }
    }
}
