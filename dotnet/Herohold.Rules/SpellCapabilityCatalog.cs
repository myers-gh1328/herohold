using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace Herohold.Rules;
public sealed class SpellCapabilityCatalog
{
    private readonly Dictionary<string,(int Level,bool Damage)> records;
    private SpellCapabilityCatalog(Dictionary<string,(int,bool)> records)=>this.records=records;
    public static SpellCapabilityCatalog Parse(string rawSpellsJson)
    {
        if(string.IsNullOrWhiteSpace(rawSpellsJson)||Encoding.UTF8.GetByteCount(rawSpellsJson)>16*1024*1024)throw new ArgumentException("The spell capability catalog is empty or too large.");
        JsonObject raw;
        try{raw=JsonNode.Parse(rawSpellsJson,documentOptions:new(){MaxDepth=64}) as JsonObject??throw new ArgumentException("The spell capability catalog must be an object.");}
        catch(JsonException error){throw new ArgumentException("The spell capability catalog is malformed.",error);}
        if(raw["spell"] is not JsonArray array||array.Count>50000)throw new ArgumentException("Spell capability records must be a bounded array.");
        var records=new Dictionary<string,(int,bool)>(StringComparer.OrdinalIgnoreCase);
        foreach(var node in array)
        {
            if(node is not JsonObject record||record["name"] is not JsonValue nameValue||!nameValue.TryGetValue<string>(out var name)||string.IsNullOrWhiteSpace(name)||name.Length>200
                ||record["level"] is not JsonValue levelValue||!levelValue.TryGetValue<int>(out var level)||level is <0 or >9)
                throw new ArgumentException("Spell capability identities and levels are invalid.");
            if(record["damageInflict"] is not null&&record["damageInflict"] is not JsonArray)throw new ArgumentException("Structured spell damage must be an array.");
            var damage=record["damageInflict"] as JsonArray;
            if(damage?.Any(item=>item is not JsonValue value||!value.TryGetValue<string>(out _))==true)throw new ArgumentException("Spell damage types must be text.");
            records[name]=(level,damage is {Count:>0});
        }
        return new(records);
    }
    public bool CanDealDamage(string spellName)=>records.TryGetValue(spellName,out var record)&&record.Damage;
    public bool IsCantrip(string spellName)=>records.TryGetValue(spellName,out var record)&&record.Level==0;
}
