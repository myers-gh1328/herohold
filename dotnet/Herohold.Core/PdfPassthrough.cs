using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text;
namespace Herohold.Core;

public static class PdfPassthrough
{
    private static readonly HashSet<string> Allowed=new(StringComparer.Ordinal){"XP","Age","Alignment","Allies","Backstory","Bonds","CP","EP","Eyes","FactionName","Flaws","GP","Hair","Height","Ideals","Inspiration","PersonalityTraits ","PlayerName","PP","Skin","SP","Treasure","Weight"};
    public static JsonObject Normalize(JsonNode? values)
    {
        var result=new JsonObject();if(values is not JsonObject fields)return result;
        if(fields.Count>1000)throw new ArgumentException("PDF passthrough exceeds 1000 fields.");
        var total=0;
        foreach(var (name,value) in fields)
        {
            if(!Allowed.Contains(name)&&!name.StartsWith("Check Box ",StringComparison.Ordinal))continue;
            if(name.EnumerateRunes().Count()>256)throw new ArgumentException("A PDF passthrough field name exceeds 256 characters.");
            if(value is not JsonValue scalar)continue;
            if(scalar.GetValueKind()==JsonValueKind.True||scalar.GetValueKind()==JsonValueKind.False){result[name]=scalar.GetValue<bool>();continue;}
            if(scalar.GetValueKind() is not (JsonValueKind.String or JsonValueKind.Number))continue;
            var text=scalar.GetValueKind()==JsonValueKind.String?scalar.GetValue<string>():scalar.ToJsonString();
            var bytes=Encoding.UTF8.GetByteCount(text);total+=bytes;
            if(bytes>100_000||total>2*1024*1024)throw new ArgumentException("PDF passthrough text exceeds the supported size limit.");
            result[name]=text;
        }
        return result;
    }
}
