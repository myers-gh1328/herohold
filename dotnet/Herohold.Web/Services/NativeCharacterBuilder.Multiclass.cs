using System.Security.Cryptography;
using System.Text;
namespace Herohold.Web.Services;
public sealed partial class NativeCharacterBuilder
{
    private static string OwnedGrantAlias(string id)=>System.Text.RegularExpressions.Regex.Replace(id.ToLowerInvariant(),"[^a-z0-9]+","-").Trim('-');
    private static string OwnedId(string track,string id)
    {var value=track+"--"+id;return value.Length<=64?value:track[..Math.Min(track.Length,24)]+"--"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..32].ToLowerInvariant();}
}
