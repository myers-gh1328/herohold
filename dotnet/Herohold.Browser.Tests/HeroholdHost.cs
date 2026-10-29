using System.Net;
using System.Net.Sockets;
using Herohold.Web.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Herohold.Browser.Tests;

/// <summary>A real Kestrel host over a disposable data directory, for one test class.</summary>
public sealed class HeroholdHost : WebApplicationFactory<App>
{
    private readonly int port = FreePort();
    public string DataDirectory { get; } = Path.Combine(Path.GetTempPath(), "herohold-browser-" + Guid.NewGuid().ToString("N"));
    public string BaseUrl => $"http://127.0.0.1:{port}/";
    public static string Root { get; } = FindRoot();

    public HeroholdHost() => UseKestrel(port);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Path.Combine(Root, "dotnet", "Herohold.Web"));
        builder.UseEnvironment("Development");
        builder.UseSetting("Herohold:RepositoryRoot", Root);
        builder.UseSetting("Herohold:DataDirectory", DataDirectory);
        builder.UseSetting("urls", BaseUrl.TrimEnd('/'));
        builder.UseSetting("AllowedHosts", "localhost;127.0.0.1");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(DataDirectory)) Directory.Delete(DataDirectory, recursive: true);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "dotnet", "Herohold.slnx"))) return directory.FullName;
        throw new InvalidOperationException("Repository root not found.");
    }
}
