using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace Herohold.Rules;
public sealed class BeastChoiceCatalog
{
    private sealed record Beast(string Name,string Type,decimal Cr,int Fly);
    private readonly IReadOnlyList<Beast> records;
    private BeastChoiceCatalog(IReadOnlyList<Beast> records)=>this.records=records;
    public static BeastChoiceCatalog Parse(string beastsJson)
    {
        if(string.IsNullOrWhiteSpace(beastsJson)||Encoding.UTF8.GetByteCount(beastsJson)>16*1024*1024)throw new ArgumentException("The beast catalog is empty or too large.");
        JsonArray array;
        try{array=JsonNode.Parse(beastsJson,documentOptions:new(){MaxDepth=64}) as JsonArray??throw new ArgumentException("The beast catalog must be an array.");}
        catch(JsonException error){throw new ArgumentException("The beast catalog is malformed.",error);}
        if(array.Count>50000)throw new ArgumentException("The beast catalog has too many records.");
        var records=new List<Beast>();
        foreach(var node in array)
        {
            if(node is not JsonObject record||record["name"] is not JsonValue nv||!nv.TryGetValue<string>(out var name)||string.IsNullOrWhiteSpace(name)||name.Length>200
                ||record["creature_type"] is not JsonValue tv||!tv.TryGetValue<string>(out var type)
                ||record["challenge_rating"] is not JsonValue cv||!cv.TryGetValue<decimal>(out var cr)||cr is <0 or >30)
                throw new ArgumentException("Beast identities and challenge ratings are invalid.");
            var fly=0;
            if(record["speeds"] is not null&&record["speeds"] is not JsonObject)throw new ArgumentException("Beast speeds must be an object.");
            if(record["speeds"]?["fly"] is {} speed&&(speed is not JsonValue sv||!sv.TryGetValue<int>(out fly)||fly is <0 or >100000))throw new ArgumentException("A beast flying speed is invalid.");
            records.Add(new(name,type,cr,fly));
        }
        return new(records.AsReadOnly());
    }
    public IReadOnlyList<string> WildShapeOptions(int level,bool moon)
    {
        if(level is <1 or >20)throw new ArgumentException("Wild Shape requires a level1–20 context.");
        if(level<2)return [];
        var cr=level>=8?1m:level>=4?0.5m:0.25m;if(moon&&level>=3)cr=Math.Max(cr,level/3);
        return records.Where(record=>record.Type=="Beast"&&record.Cr<=cr&&(level>=8||record.Fly==0)).OrderBy(record=>record.Cr).ThenBy(record=>record.Name,StringComparer.Ordinal).Select(record=>record.Name).Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
    }
}
