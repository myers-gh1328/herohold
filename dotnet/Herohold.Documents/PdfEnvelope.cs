using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;
namespace Herohold.Documents;

public sealed class PdfFormatException(string code,string message):Exception(message)
{
    public string Code {get;}=code;
    /// <summary>Worker error output for the server log; never shown to players.</summary>
    public string Diagnostics {get;init;}="";
}

public static class PdfEnvelope
{
    public const string AttachmentName="dnd-character-builder-state.json";
    public static byte[] Create(JsonObject character,JsonObject fieldSnapshot)
    {
        var envelope=new JsonObject{
            ["format_id"]="dnd-character-builder.character",["payload_version"]=1,
            ["character_schema_version"]=character["schema_version"]?.DeepClone()??JsonValue.Create(5),
            ["ruleset"]="dnd-5e-2024",["builder_version"]="1.3",["exported_at"]=DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",CultureInfo.InvariantCulture),
            ["character_sha256"]=Hash(character),["character"]=character.DeepClone(),["owned_pdf_field_snapshot"]=fieldSnapshot.DeepClone()};
        var bytes=CanonicalBytes(envelope);
        if(bytes.Length>PdfCodec.MaximumPayloadBytes)throw new PdfFormatException("payload_too_large","The embedded character exceeds the supported 2 MB limit.");
        return bytes;
    }
    public static JsonObject Parse(byte[] payload)
    {
        if(payload.Length>PdfCodec.MaximumPayloadBytes)throw new PdfFormatException("payload_too_large","The embedded character exceeds the supported 2 MB limit.");
        JsonObject envelope;
        try{envelope=JsonNode.Parse(payload,documentOptions:new(){MaxDepth=64}) as JsonObject??throw new JsonException();}
        catch(JsonException){throw new PdfFormatException("payload_malformed_json","The embedded character is not valid UTF-8 JSON.");}
        ValidateShape(envelope);
        if(envelope["format_id"] is not JsonValue format||!format.TryGetValue<string>(out var formatId)||formatId!="dnd-character-builder.character")throw new PdfFormatException("payload_unknown_format","The attachment is not a supported character payload.");
        if(envelope["payload_version"] is not JsonValue version||!version.TryGetValue<int>(out var payloadVersion)||payloadVersion!=1)throw new PdfFormatException("payload_unsupported_version","The embedded payload version is unsupported.");
        if(envelope["character_schema_version"] is not JsonValue schemaValue||!schemaValue.TryGetValue<int>(out var schema))throw new PdfFormatException("payload_invalid_schema_version","The embedded character schema must be an integer.");
        if(schema is <1 or >5)throw new PdfFormatException("payload_unsupported_schema","The embedded character schema is unsupported.");
        if(envelope["character"] is not JsonObject character)throw new PdfFormatException("payload_invalid_character","The embedded character must be an object.");
        foreach(var key in new[]{"character_id","name","class","subclass","species","background"})
            if(character.TryGetPropertyValue(key,out var textValue)&&(textValue is not JsonValue textScalar||!textScalar.TryGetValue<string>(out _)))throw new PdfFormatException("payload_invalid_character",$"The embedded character field '{key}' must be text.");
        foreach(var key in new[]{"ability_scores","base_ability_scores","class_options","species_options","background_options","spells"})
            if(character.TryGetPropertyValue(key,out var objectValue)&&objectValue is not JsonObject)throw new PdfFormatException("payload_invalid_character",$"The embedded character field '{key}' must be an object.");
        if(character["schema_version"] is {} characterSchema&&(!int.TryParse(characterSchema.ToJsonString(),out var actualSchema)||actualSchema!=schema))throw new PdfFormatException("payload_schema_mismatch","The envelope and character schemas do not match.");
        if(character["level"] is {} level&&(!int.TryParse(level.ToJsonString(),out var number)||number is <1 or >20))throw new PdfFormatException("payload_invalid_character_level","The embedded character level must be 1 through 20.");
        if(envelope["character_sha256"] is not JsonValue checksum||!checksum.TryGetValue<string>(out var expected)||expected.Length!=64)throw new PdfFormatException("payload_invalid_checksum","The embedded character checksum is missing or invalid.");
        if(!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected.ToLowerInvariant()),Encoding.ASCII.GetBytes(Hash(character))))throw new PdfFormatException("payload_checksum_mismatch","The embedded character checksum does not match its contents.");
        if(envelope["owned_pdf_field_snapshot"] is not JsonObject snapshot||snapshot.Any(p=>p.Value is not JsonValue))throw new PdfFormatException("payload_invalid_field_snapshot","The PDF field snapshot must contain scalar values.");
        return envelope;
    }
    internal static string Hash(JsonNode node)=>Convert.ToHexStringLower(SHA256.HashData(CanonicalBytes(node)));
    internal static byte[] CanonicalBytes(JsonNode node)
    {
        ValidateShape(node);var output=new StringBuilder();Write(node,output);return Encoding.UTF8.GetBytes(output.ToString());
    }
    private static void ValidateShape(JsonNode? node,int depth=0,int[]? count=null)
    {
        count??=[0];if(depth>24)throw new PdfFormatException("payload_too_deep","The embedded character contains too many nested levels.");
        if(++count[0]>50_000)throw new PdfFormatException("payload_too_complex","The embedded character contains too many values.");
        if(node is JsonObject obj)foreach(var entry in obj)ValidateShape(entry.Value,depth+1,count);
        else if(node is JsonArray array)foreach(var value in array)ValidateShape(value,depth+1,count);
    }
    private static void Write(JsonNode? node,StringBuilder output)
    {
        if(node is null){output.Append("null");return;}
        if(node is JsonObject obj){output.Append('{');var first=true;foreach(var entry in obj.OrderBy(p=>p.Key,CodePointComparer.Instance)){if(!first)output.Append(',');first=false;WriteString(entry.Key,output);output.Append(':');Write(entry.Value,output);}output.Append('}');return;}
        if(node is JsonArray array){output.Append('[');for(var i=0;i<array.Count;i++){if(i>0)output.Append(',');Write(array[i],output);}output.Append(']');return;}
        var scalar=(JsonValue)node;
        if(scalar.GetValueKind()==JsonValueKind.String){WriteString(scalar.GetValue<string>(),output);return;}
        var raw=scalar.ToJsonString();
        if(scalar.GetValueKind()==JsonValueKind.Number&&(raw.Contains('.')||raw.Contains('e')||raw.Contains('E')||scalar.TryGetValue<double>(out _)&&!scalar.TryGetValue<JsonElement>(out _)))raw=PythonFloat(double.Parse(raw,CultureInfo.InvariantCulture));
        output.Append(raw);
    }
    private static string PythonFloat(double value)
    {
        if(!double.IsFinite(value))throw new PdfFormatException("payload_invalid_number","The embedded character contains a non-finite number.");
        var text=value.ToString("R",CultureInfo.InvariantCulture).ToLowerInvariant();
        if(!text.Contains('e')&&Math.Abs(value)>=1e16)
        {
            var sign=text.StartsWith('-')?"-":"";var digits=text.TrimStart('-').Replace(".","");var exponent=text.TrimStart('-').Split('.')[0].Length-1;digits=digits.TrimEnd('0');
            text=sign+digits[0]+(digits.Length>1?"."+digits[1..]:"")+"e+"+exponent;
        }
        if(text.Contains('e')){var parts=text.Split('e');var exponent=int.Parse(parts[1],CultureInfo.InvariantCulture);return parts[0]+"e"+(exponent<0?"-":"+")+Math.Abs(exponent).ToString("D2",CultureInfo.InvariantCulture);}
        return text.Contains('.')?text:text+".0";
    }
    private static void WriteString(string text,StringBuilder output)
    {
        output.Append('"');foreach(var c in text){output.Append(c switch{'"'=>"\\\"",'\\'=>"\\\\",'\b'=>"\\b",'\f'=>"\\f",'\n'=>"\\n",'\r'=>"\\r",'\t'=>"\\t",< ' '=>"\\u"+((int)c).ToString("x4",CultureInfo.InvariantCulture),_=>c.ToString()});}output.Append('"');
    }
    private sealed class CodePointComparer:IComparer<string>
    {
        public static readonly CodePointComparer Instance=new();
        public int Compare(string? a,string? b){var first=(a??"").EnumerateRunes().GetEnumerator();var second=(b??"").EnumerateRunes().GetEnumerator();while(true){var x=first.MoveNext();var y=second.MoveNext();if(!x||!y)return x?1:y?-1:0;var comparison=first.Current.Value.CompareTo(second.Current.Value);if(comparison!=0)return comparison;}}
    }
}
