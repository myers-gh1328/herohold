using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace Herohold.Rules;
public sealed record SpellGrantIds(string GrantId,string ResourceId);
/// <summary>Retains the existing source-derived identities so native migration cannot refill spent grant pools.</summary>
public static class SpellGrantIdentity
{
    public static SpellGrantIds ForSource(SpellGrantSource source,string spellName)=>ForLabel(SerializeSource(source),spellName);
    /// <summary>A source in its saved identity form: the fields it sets, by their saved names in code-point order.</summary>
    public static string SerializeSource(SpellGrantSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var fields=new JsonObject{["type"]=source.Type};
        void Set(string key,JsonNode? value){if(value is not null)fields[key]=value;}
        Set("source_name",source.SourceName);Set("feature_name",source.FeatureName);Set("class_name",source.ClassName);Set("subclass_name",source.SubclassName);
        Set("subclass_id",source.SubclassId);Set("source",source.Book);Set("instance_id",source.InstanceId);Set("feat_id",source.FeatId);Set("level",source.Level);Set("level_gained",source.LevelGained);
        return SerializeSource(fields);
    }
    public static SpellGrantIds ForLabel(string sourceLabel,string spellName)
    {
        ArgumentNullException.ThrowIfNull(sourceLabel);ArgumentException.ThrowIfNullOrWhiteSpace(spellName);
        if(sourceLabel.Length>65536||spellName.Length>200)throw new ArgumentException("A spell grant identity exceeds its supported bounds.");
        var token=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceLabel+"|"+spellName)))[..24].ToLowerInvariant();
        return new("grant-"+token,"grant-uses-"+token);
    }
    public static string SerializeSource(JsonObject source)
    {
        ArgumentNullException.ThrowIfNull(source);var output=new StringBuilder();var nodes=0;
        void Write(JsonNode? node,int depth)
        {
            if(depth>16||++nodes>1000||output.Length>65536)throw new ArgumentException("A grant source is too large or deeply nested.");
            if(node is null){output.Append("null");return;}
            if(node is JsonObject record)
            {
                output.Append('{');var first=true;
                foreach(var pair in record.OrderBy(pair=>pair.Key,CodePointOrder.Instance))
                {if(!first)output.Append(", ");first=false;Quote(output,pair.Key);output.Append(": ");Write(pair.Value,depth+1);}
                output.Append('}');return;
            }
            if(node is JsonArray array)
            {output.Append('[');for(var i=0;i<array.Count;i++){if(i>0)output.Append(", ");Write(array[i],depth+1);}output.Append(']');return;}
            if(node is JsonValue value&&value.TryGetValue<string>(out var text)){Quote(output,text);return;}
            if(node.GetValueKind() is JsonValueKind.True or JsonValueKind.False){output.Append(node.GetValue<bool>()?"true":"false");return;}
            if(node.GetValueKind()==JsonValueKind.Number)
            {
                var number=node.ToJsonString();
                // Reviewed source schemas contain integer levels. Unsupported float provenance must not silently change a saved identity.
                if(number.Contains('.')||number.Contains('e')||number.Contains('E'))throw new ArgumentException("Spell grant source numbers must be whole-number source metadata.");
                output.Append(number);return;
            }
            throw new ArgumentException("A grant source contains an unsupported JSON value.");
        }
        Write(source,0);if(output.Length>65536)throw new ArgumentException("A grant source is too large.");return output.ToString();
    }
    private static void Quote(StringBuilder output,string text)
    {
        output.Append('"');
        foreach(var character in text)
            switch(character)
            {
                case '"':output.Append("\\\"");break;case '\\':output.Append("\\\\");break;
                case '\b':output.Append("\\b");break;case '\f':output.Append("\\f");break;case '\n':output.Append("\\n");break;case '\r':output.Append("\\r");break;case '\t':output.Append("\\t");break;
                default:if(character<0x20||character>=0x7f)output.Append("\\u").Append(((int)character).ToString("x4"));else output.Append(character);break;
            }
        output.Append('"');
    }
    private sealed class CodePointOrder:IComparer<string>
    {
        internal static readonly CodePointOrder Instance=new();
        public int Compare(string? x,string? y)
        {
            if(x is null)return y is null?0:-1;if(y is null)return 1;
            var left=x.EnumerateRunes().GetEnumerator();var right=y.EnumerateRunes().GetEnumerator();
            while(true)
            {var hasLeft=left.MoveNext();var hasRight=right.MoveNext();if(!hasLeft||!hasRight)return hasLeft?1:hasRight?-1:0;var result=left.Current.Value.CompareTo(right.Current.Value);if(result!=0)return result;}
        }
    }
}
