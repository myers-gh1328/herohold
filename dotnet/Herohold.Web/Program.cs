using System.Net;
using Herohold.Core;
using Herohold.Rules;
using Herohold.Documents;
using System.Text.Json.Nodes;
using Herohold.Web.Components;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;

if (args is ["import-json", ..])
{
    var root = Environment.GetEnvironmentVariable("Herohold__RepositoryRoot") ?? FindRepositoryRoot(Directory.GetCurrentDirectory());
    Environment.Exit(await LegacyImportCommand.RunAsync(args[1..], root, Path.Combine(AppContext.BaseDirectory, "pdf-worker", "Herohold.PdfWorker.dll"), Console.Out));
}

var builder = WebApplication.CreateBuilder(args);
var addresses = builder.Configuration["urls"] ?? "http://127.0.0.1:8510";
var allowLan = builder.Configuration.GetValue<bool>("Herohold:AllowLan");
foreach (var address in addresses.Split(';', StringSplitOptions.RemoveEmptyEntries))
{
    if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
        uri.UserInfo.Length > 0 || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        throw new InvalidOperationException("Configure an HTTP listener with an explicit loopback or private LAN address.");
    var numeric = IPAddress.TryParse(uri.Host, out var ip);
    var loopback = uri.Host == "localhost" || numeric && IPAddress.IsLoopback(ip!);
    var bytes = numeric ? ip!.GetAddressBytes() : [];
    var privateLan = bytes.Length == 4 && (bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 192 && bytes[1] == 168);
    if (!loopback && !(allowLan && privateLan))
        throw new InvalidOperationException("Listeners require loopback, or an explicit private IPv4 address with Herohold:AllowLan=true. Wildcard, public and Tailscale addresses are not allowed.");
}
builder.WebHost.UseUrls(addresses);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var dataDirectory = builder.Configuration["Herohold:DataDirectory"]
    ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".tmp", "herohold-dotnet-data"));
builder.Services.AddHeroholdStorage(builder.Configuration["Herohold:DatabasePath"] ?? Path.Combine(dataDirectory, "herohold.db"));
builder.Services.AddSingleton(new HeroholdFeatures(Combat: builder.Configuration.GetValue("Herohold:Features:Combat", false)));
string FindRepositoryRoot(string start)
{
    for(var current=new DirectoryInfo(start);current is not null;current=current.Parent)
        if(File.Exists(Path.Combine(current.FullName,"data","imported","5etools","classes.json")))return current.FullName;
    throw new InvalidOperationException("Configure Herohold:RepositoryRoot for the installed character rules data.");
}
var repositoryRoot=builder.Configuration["Herohold:RepositoryRoot"]??(File.Exists(Path.Combine(AppContext.BaseDirectory,"data","imported","5etools","classes.json"))?AppContext.BaseDirectory:FindRepositoryRoot(builder.Environment.ContentRootPath));
builder.Services.AddHeroholdRules(repositoryRoot,builder.Configuration["Herohold:PdfWorkerAssembly"]??Path.Combine(AppContext.BaseDirectory,"pdf-worker","Herohold.PdfWorker.dll"));
builder.Services.AddScoped<WorkspaceSession>();
var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// Reject browser requests initiated by another origin, including WebSocket upgrades.
// Additional exact origins are opt-in for a reviewed private reverse proxy.
var trustedOrigins = (builder.Configuration["Herohold:TrustedOrigins"] ?? "")
    .Split(';', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers.Origin.ToString();
    var crossSiteMutation = context.Request.Headers["Sec-Fetch-Site"] == "cross-site"
        && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method);
    var requestOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
    if (crossSiteMutation || origin.Length > 0 &&
        (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
         !string.Equals(uri.GetLeftPart(UriPartial.Authority), requestOrigin, StringComparison.OrdinalIgnoreCase)
         && !trustedOrigins.Contains(origin)))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next(context);
});
app.UseAntiforgery();
app.MapGet("/healthz", () => Results.Ok(new { status = "alive", application = "herohold-dotnet" }));
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
