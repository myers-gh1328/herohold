using System.Text.Json.Nodes;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;
namespace Herohold.Rules;
public static class CanonicalCharacterRules
{
    private static readonly JsonObject Defaults=LoadDefaults();
    private static readonly HashSet<string> Transients=["level_up_draft","pdf_import_draft","_pending_current_hp_increase"];
    private static JsonObject LoadDefaults()
    {
        using var stream=typeof(CanonicalCharacterRules).Assembly.GetManifestResourceStream("Herohold.Rules.Assets.character-defaults.json")??throw new InvalidOperationException("The character defaults are missing.");
        return JsonNode.Parse(stream)!.AsObject();
    }
    public static JsonObject Create()
    {
        var created=Defaults.DeepClone().AsObject();created["character_id"]=Guid.NewGuid().ToString("N");return created;
    }
    public static JsonObject Migrate(JsonObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(Encoding.UTF8.GetByteCount(source.ToJsonString())>8*1024*1024)throw new ArgumentException("The character exceeds its 8 MB limit.");
        if(source["schema_version"] is not null&&(source["schema_version"] is not JsonValue schema||!schema.TryGetValue<int>(out var version)||version is <1 or >5))throw new ArgumentException("This character schema is not supported.");
        foreach(var field in new[]{"hit_points","spells","class_options","species_options","background_options","base_ability_scores","origin_feat_choices","folio_origin_feat_choices","presentation_preferences"})
            if(source[field] is not null&&source[field] is not JsonObject)throw new ArgumentException(field+" must be an object.");
        foreach(var field in new[]{"general_feats","origin_languages","equipment","custom_equipment","feature_configurations","level_history","hp_history","hp_rolls","ability_score_adjustments"})
            if(source[field] is not null&&source[field] is not JsonArray)throw new ArgumentException(field+" must be an array.");
        var nodes=0;var clean=Clean(source,0,ref nodes)!.AsObject();var result=Merge(Defaults,clean);
        var savedHp=clean["hit_points"] as JsonObject??new();var hp=result["hit_points"]!.AsObject();
        if(!savedHp.ContainsKey("maximum"))hp["maximum"]=hp["max"]?.DeepClone();if(!savedHp.ContainsKey("max"))hp["max"]=hp["maximum"]?.DeepClone();
        if(Text(result["character_id"]).Length==0)result["character_id"]="legacy_"+Identity(clean);
        var feats=new JsonArray();var index=0;
        foreach(var entry in (result["general_feats"] as JsonArray??[]))
        {
            JsonObject feat;if(entry is JsonObject record)feat=record.DeepClone().AsObject();else if(Text(entry).Length>0)feat=new(){["name"]=Text(entry)};else continue;
            if(Text(feat["instance_id"]).Length==0)feat["instance_id"]="legacy_feat_"+Identity(new JsonObject{["index"]=index,["entry"]=entry?.DeepClone()});
            if(!feat.ContainsKey("feat_id"))feat["feat_id"]=null;feat["name"]??="";feat["source"]??=new JsonObject{["type"]="legacy"};feat["choices"]??=new JsonObject();feats.Add(feat);index++;
        }
        result["general_feats"]=feats;
        var spells=result["spells"]!.AsObject();
        if(Text(result["class"])=="Wizard")
        {
            var known=Values(clean["spells"]?["known"]).ToArray();if(known.Length>0)
            {spells["spellbook"]=Array(Values(spells["spellbook"]).Concat(known));spells["known"]=new JsonArray();if(!Values(spells["prepared"]).Any())Warn(result,"Legacy Wizard known spells were moved into the spellbook. Review prepared spells before play.");}
        }
        if(Text(result["class"])=="Bard"&&Integer(result["level"],1)>=10)
        {
            var options=result["class_options"]!.AsObject();var legacy=Values(options["magical_secrets"]).ToArray();if(legacy.Length>0)
            {spells["prepared"]=Array(Values(spells["prepared"]).Concat(legacy));options["legacy_magical_secrets"]=Array(legacy);options.Remove("magical_secrets");Warn(result,"Legacy Magical Secrets bonus spells were moved into Bard prepared spells. Review the prepared-spell total before play.");}
        }
        var equipment=Values(result["equipment"]).ToArray();result["equipment"]=Array(equipment);
        if(!clean.ContainsKey("custom_equipment")){result["custom_equipment"]=Array(equipment);result["equipment_migration_pending"]=equipment.Length>0;}
        else result["custom_equipment"]=Array(Values(result["custom_equipment"]));
        if(Text(result["class"])=="Warlock")
        {
            var options=result["class_options"]!.AsObject();var legacy=Values(options["mystic_arcanum"]).ToArray();
            if(legacy.Length>0)
            {for(var i=0;i<legacy.Length;i++)options["mystic_arcanum_"+(i<4?i+6:6)]=new JsonArray(legacy[i]);options["legacy_mystic_arcanum"]=Array(legacy);options.Remove("mystic_arcanum");Warn(result,"Legacy Mystic Arcanum selections were converted into level-specific Arcanum records.");}
            var arcanum=result["mystic_arcanum"] as JsonArray??new();result["mystic_arcanum"]=arcanum;
            foreach(var pair in new[]{(11,6),(13,7),(15,8),(17,9)})
            {
                if(Integer(result["level"],1)<pair.Item1&&legacy.Length==0)continue;
                foreach(var spell in Values(options["mystic_arcanum_"+pair.Item2]))
                {
                    var id="warlock_mystic_arcanum_"+pair.Item2;
                    var spellId=new string(spell.Select(ch=>char.IsLetterOrDigit(ch)?char.ToLowerInvariant(ch):'_').ToArray()).Trim('_')+"_xphb";
                    var record=new JsonObject{["instance_id"]=id,["spell_id"]=spellId,["spell_name"]=spell,["name"]=spell,["spell_level"]=pair.Item2,["source_level"]=pair.Item1,["spellcasting_ability"]="Charisma",["source"]=new JsonObject{["type"]="class_feature",["class_name"]="Warlock",["feature_name"]="Mystic Arcanum",["source_name"]="Mystic Arcanum",["level"]=pair.Item1},["casting_rules"]=new JsonObject{["free_casts"]=1,["recharge"]="long_rest",["uses_pact_slots"]=false},["always_prepared"]=true,["counts_against_limit"]=false,["grant_type"]="mystic_arcanum"};
                    var existing=arcanum.OfType<JsonObject>().FirstOrDefault(r=>Text(r["instance_id"])==id);if(existing is null)arcanum.Add(record);else arcanum[arcanum.IndexOf(existing)]=record;
                }
            }
        }
        var provenance=new JsonObject();var allowed=new HashSet<string>{"source_kind","payload_version","template_signature","imported_at","status","ability_history_reconstructed","hp_history_reconstructed"};
        if(result["import_provenance"] is JsonObject old)
        {
            foreach(var pair in old.Where(p=>allowed.Contains(p.Key)&&p.Value is JsonValue))provenance[pair.Key]=pair.Value!.DeepClone();
            provenance["warnings"]=old["warnings"] is JsonArray warnings?new JsonArray(warnings.Where(v=>v is JsonValue).Select(v=>(JsonNode?)JsonValue.Create(TextScalar(v!))).ToArray()):new JsonArray();
        }
        result["import_provenance"]=provenance;
        var adjustment=Integer((result["import_overrides"] as JsonObject)?["maximum_hp_adjustment"]);if(adjustment is <-100000 or >100000)throw new ArgumentException("The imported HP adjustment exceeds supported bounds.");
        result["import_overrides"]=new JsonObject{["maximum_hp_adjustment"]=adjustment};result["schema_version"]=5;
        result["pdf_passthrough_fields"]=Herohold.Core.PdfPassthrough.Normalize(result["pdf_passthrough_fields"]);
        return result;
    }
    public static JsonObject Merge(JsonObject defaults,JsonObject saved)
    {
        var merged=defaults.DeepClone().AsObject();foreach(var pair in saved)merged[pair.Key]=merged[pair.Key] is JsonObject left&&pair.Value is JsonObject right?Merge(left,right):pair.Value?.DeepClone();return merged;
    }
    private static JsonNode? Clean(JsonNode? value,int depth,ref int count)
    {
        if(depth>64||++count>250000)throw new ArgumentException("The character exceeds supported structure limits.");
        if(value is JsonObject record){var copy=new JsonObject();foreach(var pair in record)if(!Transients.Contains(pair.Key)&&pair.Key!="path"&&!pair.Key.EndsWith("_path",StringComparison.Ordinal))copy[pair.Key]=Clean(pair.Value,depth+1,ref count);return copy;}
        if(value is JsonArray array){var copy=new JsonArray();foreach(var item in array)copy.Add(Clean(item,depth+1,ref count));return copy;}return value?.DeepClone();
    }
    private static JsonNode? Sorted(JsonNode? node)=>node switch{JsonObject obj=>new JsonObject(obj.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>KeyValuePair.Create(p.Key,Sorted(p.Value)))),JsonArray array=>new JsonArray(array.Select(Sorted).ToArray()),_=>node?.DeepClone()};
    private static string Identity(JsonObject value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Sorted(value)!.ToJsonString()))).ToLowerInvariant()[..32];
    private static string Text(JsonNode? value)=>value is JsonValue v&&v.TryGetValue<string>(out var text)?text:"";
    private static string TextScalar(JsonNode value)=>value is JsonValue v&&v.TryGetValue<string>(out var text)?text:value.ToJsonString();
    private static int Integer(JsonNode? value,int fallback=0)
    {
        if(value is not JsonValue v)return fallback;if(v.TryGetValue<int>(out var i))return i;
        if(v.TryGetValue<decimal>(out var number)&&number>=int.MinValue&&number<=int.MaxValue)return (int)number;
        return v.TryGetValue<string>(out var text)&&int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out i)?i:fallback;
    }
    private static IEnumerable<string> Values(JsonNode? node)=>node is JsonArray array?array.Select(Text).Where(s=>s.Length>0):Text(node).Length>0?[Text(node)]:[];
    private static JsonArray Array(IEnumerable<string> values)=>new(values.Distinct(StringComparer.Ordinal).Select(s=>(JsonNode?)JsonValue.Create(s)).ToArray());
    private static void Warn(JsonObject character,string text){var warnings=character["migration_warnings"] as JsonArray??new();character["migration_warnings"]=warnings;if(!Values(warnings).Contains(text))warnings.Add(text);}
}
