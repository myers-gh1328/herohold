using System.Net;
using Herohold.Core;
using Herohold.Web.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Http.Tests;

[TestClass]
public sealed class LanHostingTests
{
    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow("http://192.168.1.20:8511")]
    [DataRow("http://10.42.0.7:8511")]
    [DataRow("http://172.20.0.7:8511")]
    public async Task ExplicitLanBindingServesOnlyItsConfiguredHostAndSameOriginCircuit(string address)
    {
        using var host=new LanFactory(address,true);
        using var client=host.CreateClient(new(){BaseAddress=new(address)});
        Assert.AreEqual(HttpStatusCode.OK,(await client.GetAsync("/healthz")).StatusCode);
        using var same=new HttpRequestMessage(HttpMethod.Post,"/_blazor/negotiate?negotiateVersion=1");same.Headers.Add("Origin",address);
        Assert.AreEqual(HttpStatusCode.OK,(await client.SendAsync(same)).StatusCode);
        using var cross=new HttpRequestMessage(HttpMethod.Post,"/_blazor/negotiate?negotiateVersion=1");cross.Headers.Add("Origin","http://192.168.1.21:8511");
        Assert.AreEqual(HttpStatusCode.Forbidden,(await client.SendAsync(cross)).StatusCode);
        using var tail=new HttpRequestMessage(HttpMethod.Get,"/healthz");tail.Headers.Host="desktop.tail0000.ts.net:8506";
        Assert.AreEqual(HttpStatusCode.BadRequest,(await client.SendAsync(tail)).StatusCode);
    }
    [TestMethod]
    [DataRow("http://192.168.1.20:8511",false)]
    [DataRow("http://0.0.0.0:8511",true)]
    [DataRow("http://8.8.8.8:8511",true)]
    [DataRow("http://100.64.0.10:8511",true)]
    public void LanHostingRequiresExplicitPrivateAddressOptIn(string address,bool enabled)
    {
        using var host=new LanFactory(address,enabled);
        Assert.Throws<InvalidOperationException>(()=>host.CreateClient());
    }
    private sealed class LanFactory(string address,bool enabled):WebApplicationFactory<App>
    {
        private readonly string directory=Path.Combine(Path.GetTempPath(),"herohold-lan-"+Guid.NewGuid().ToString("N"));
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var root=new DirectoryInfo(AppContext.BaseDirectory);
            while(root is not null&&!File.Exists(Path.Combine(root.FullName,"dotnet/Herohold.Web/Herohold.Web.csproj")))root=root.Parent;
            builder.UseContentRoot(Path.Combine(root!.FullName,"dotnet/Herohold.Web"));builder.UseEnvironment("Production");
            builder.UseSetting("urls",address);builder.UseSetting("Herohold:AllowLan",enabled.ToString());
            builder.UseSetting("AllowedHosts",new Uri(address).Host+";localhost;127.0.0.1");builder.UseSetting("Herohold:TrustedOrigins","");
            builder.UseSetting("Herohold:DataDirectory",directory);builder.UseSetting("Herohold:RepositoryRoot",root.FullName);
        }
        protected override void Dispose(bool disposing)
        {base.Dispose(disposing);if(disposing&&Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
