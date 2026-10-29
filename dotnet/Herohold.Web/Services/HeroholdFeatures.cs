namespace Herohold.Web.Services;

/// <summary>Feature switches read from <c>Herohold:Features</c>. Combat mode is off unless configured on.</summary>
public sealed record HeroholdFeatures(bool Combat)
{
    public static readonly HeroholdFeatures Default=new(Combat:false);
}
