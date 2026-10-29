using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace Herohold.Rules;
public sealed class ToolCatalog
{
    public IReadOnlyList<string> ArtisanTools {get;}=[];
    public IReadOnlyList<string> GamingSets {get;}=[];
    public IReadOnlyList<string> MusicalInstruments {get;}=[];
    public IReadOnlyList<string> OtherTools {get;}=[];
    public IReadOnlyList<string> All {get;}=[];
    private ToolCatalog(IReadOnlyDictionary<string,HashSet<string>> groups)
    {
        static IReadOnlyList<string> Names(HashSet<string> values)=>Array.AsReadOnly(values.Order(StringComparer.Ordinal).ToArray());
        ArtisanTools=Names(groups["AT"]);GamingSets=Names(groups["GS"]);MusicalInstruments=Names(groups["INS"]);OtherTools=Names(groups["T"]);
        All=Array.AsReadOnly(groups.Values.SelectMany(values=>values).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }
    public static ToolCatalog Parse(string rawItemsBaseJson,string? rawItemsJson=null)
    {
        var groups=new Dictionary<string,HashSet<string>>{["AT"]=new(StringComparer.Ordinal),["GS"]=new(StringComparer.Ordinal),["INS"]=new(StringComparer.Ordinal),["T"]=new(StringComparer.Ordinal)};
        void Read(string json,string key)
        {
            if(string.IsNullOrWhiteSpace(json)||Encoding.UTF8.GetByteCount(json)>32*1024*1024)throw new ArgumentException("A tool catalog is empty or exceeds its supported size.");
            JsonObject root;try{root=JsonNode.Parse(json,documentOptions:new(){MaxDepth=64}) as JsonObject??throw new ArgumentException("A tool catalog must be an object.");}catch(JsonException error){throw new ArgumentException("The tool catalog is malformed.",error);}
            if(root[key] is not JsonArray records||records.Count>50000)throw new ArgumentException("Tool records must be a bounded array.");
            foreach(var record in records.OfType<JsonObject>().Where(record=>record["source"]?.GetValue<string>()=="XPHB"))
            {
                var type=record["type"]?.GetValue<string>()?.Split('|')[0]??"";if(!groups.TryGetValue(type,out var group))continue;
                var name=record["name"]?.GetValue<string>()??"";if(string.IsNullOrWhiteSpace(name)||name.Length>200)throw new ArgumentException("Tool names must be bounded, nonempty text.");group.Add(name);
            }
        }
        Read(rawItemsBaseJson,"baseitem");if(rawItemsJson is not null)Read(rawItemsJson,"item");
        if(groups.Values.Sum(group=>group.Count)>1000)throw new ArgumentException("The tool catalog has too many tool identities.");return new(groups);
    }
    public IReadOnlyList<string> Expand(IEnumerable<string> options)
    {
        ArgumentNullException.ThrowIfNull(options);var bounded=options.Take(1001).ToArray();if(bounded.Length>1000||bounded.Any(name=>name is null||name.Length>200))throw new ArgumentException("Tool option names exceed their supported bounds.");
        IEnumerable<string> Group(string name)=>name switch
        {
            "Any artisan's tools" or "Any artisan's tool" or "Any artisans tool"=>ArtisanTools,
            "Any gaming set"=>GamingSets,"Any musical instrument"=>MusicalInstruments,"Any other tool"=>OtherTools,
            _=>All.Contains(name,StringComparer.Ordinal)?[name]:[]
        };
        return Array.AsReadOnly(bounded.SelectMany(Group).Distinct(StringComparer.Ordinal).ToArray());
    }
}
