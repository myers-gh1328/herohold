using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace Herohold.Rules;

/// <summary>A private frozen copy of trusted catalog JSON. Every outward view is deeply detached.</summary>
public sealed class RulesCatalogSnapshot
{
    private const int MaximumInputBytes=16*1024*1024;
    private const int MaximumTotalBytes=32*1024*1024;
    private const int MaximumNodes=250000;
    private const int MaximumRecords=50000;
    private readonly JsonObject catalogs;
    private static readonly Regex Tags=new(@"\{@([^{}]+)\}",RegexOptions.CultureInvariant|RegexOptions.NonBacktracking);
    // Python str.strip includes these four control separators in addition to Unicode whitespace.
    private static readonly char[] PythonWhitespace=Enumerable.Range(9,5).Concat(Enumerable.Range(28,5))
        .Concat(new[]{0x85,0xa0,0x1680}).Concat(Enumerable.Range(0x2000,11))
        .Concat(new[]{0x2028,0x2029,0x202f,0x205f,0x3000}).Select(value=>(char)value).ToArray();
    private RulesCatalogSnapshot(JsonObject catalogs)
    {
        this.catalogs=catalogs;
        SpellRecords=catalogs["spells"]!.AsArray().OfType<JsonObject>().Select(Spell).ToList().AsReadOnly();
        WeaponRecords=catalogs["weapons"]!.AsArray().OfType<JsonObject>().Select(w=>new CatalogWeapon(Field(w["name"]),Field(w["category"]),Field(w["damage"]),Field(w["damage_type"]),Field(w["mastery"]))).ToList().AsReadOnly();
        FeatRecords=catalogs["feats"]!.AsArray().OfType<JsonObject>().Select(CatalogFeat.From).ToList().AsReadOnly();
        BackgroundRecords=catalogs["backgrounds"]!.AsArray().OfType<JsonObject>().Select(CatalogBackground.From).ToList().AsReadOnly();
        SpeciesRecords=catalogs["species"]!.AsArray().OfType<JsonObject>().Select(CatalogSpecies.From).ToList().AsReadOnly();
        ClassRecords=catalogs["classes"]!.AsArray().OfType<JsonObject>().Select(CatalogClass.From).ToList().AsReadOnly();
        spellsByName=SpellRecords.GroupBy(s=>s.Name,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
    }
    private readonly Dictionary<string,CatalogSpell> spellsByName;
    /// <summary>The catalog's classes, in catalog order.</summary>
    public IReadOnlyList<CatalogClass> ClassRecords { get; }
    /// <summary>The catalog's backgrounds and species, in catalog order.</summary>
    public IReadOnlyList<CatalogBackground> BackgroundRecords { get; }
    public IReadOnlyList<CatalogSpecies> SpeciesRecords { get; }
    /// <summary>The catalog's feats, in catalog order.</summary>
    public IReadOnlyList<CatalogFeat> FeatRecords { get; }
    /// <summary>The catalog's weapons, in catalog order.</summary>
    public IReadOnlyList<CatalogWeapon> WeaponRecords { get; }
    /// <summary>The catalog's spells, in catalog order.</summary>
    public IReadOnlyList<CatalogSpell> SpellRecords { get; }
    /// <summary>A spell by its name, ignoring case.</summary>
    public CatalogSpell? Spell(string name)=>spellsByName.GetValueOrDefault(name);
    private static CatalogSpell Spell(JsonObject s)=>new()
    {
        Name=Field(s["name"]),Source=s["source"] is JsonValue source&&source.TryGetValue<string>(out var text)?text:"XPHB",Level=s["level"] is JsonValue level&&level.TryGetValue<int>(out var number)?number:0,
        School=Field(s["school"]),SchoolCode=Field(s["school_code"]),Description=Field(s["description"]),Classes=[..(s["classes"] as JsonArray??[]).Select(Field).Where(c=>c.Length>0)],
        CastingTime=Field(s["casting_time"]),Range=Field(s["range"]),Components=[..(s["components"] as JsonArray??[]).Select(Field).Where(c=>c.Length>0)],ComponentsKnown=s["components_known"] is JsonValue known&&known.TryGetValue<bool>(out var k)&&k,
        Duration=Field(s["duration"]),Ritual=s["ritual"] is JsonValue ritual&&ritual.TryGetValue<bool>(out var r)&&r,MaterialDescription=Field(s["material_description"]),
        MaterialCostCp=s["material_cost_cp"] is JsonValue cost&&cost.TryGetValue<int>(out var cp)?cp:null,MaterialConsumed=s["material_consumed"] is JsonValue used&&used.TryGetValue<bool>(out var consumed)?consumed:null,
        MaterialConsumptionOptional=s["material_consumption_optional"] is JsonValue optional&&optional.TryGetValue<bool>(out var o)&&o,
        SpellAttackKind=s["spell_attack_kind"] is JsonValue kind&&kind.TryGetValue<string>(out var attack)?attack:null,SpellAttackPattern=s["spell_attack_pattern"] is JsonValue pattern&&pattern.TryGetValue<string>(out var p)?p:null
    };
    public IReadOnlyList<string> Equipment=>catalogs["equipment"]!.AsArray().Select(node=>node!.GetValue<string>()).ToList().AsReadOnly();
    /// <summary>A class by its exact name.</summary>
    public CatalogClass? Class(string name)=>ClassRecords.FirstOrDefault(c=>c.Name==name);
    public CatalogSpecies? SpeciesNamed(string name)=>SpeciesRecords.FirstOrDefault(s=>s.Name==name);
    public CatalogBackground? Background(string name)=>BackgroundRecords.FirstOrDefault(b=>b.Name==name);
    /// <summary>The names, descriptions and sources a picker lists, without copying the records.</summary>
    public IReadOnlyList<CatalogEntry> ClassEntries=>Entries("classes");
    public IReadOnlyList<CatalogEntry> SpeciesEntries=>Entries("species");
    public IReadOnlyList<CatalogEntry> BackgroundEntries=>Entries("backgrounds");
    private IReadOnlyList<CatalogEntry> Entries(string key)=>catalogs[key]!.AsArray().OfType<JsonObject>()
        .Select(r=>new CatalogEntry(Field(r["name"]),Field(r["description"]??r["short_summary"]),Field(r["source"]))).ToList().AsReadOnly();
    private static string Field(JsonNode? node)=>node is JsonValue value&&value.TryGetValue<string>(out var text)?text:"";
    public JsonObject ToEnvelope()=>new(){["catalogs"]=catalogs.DeepClone()};

    public static RulesCatalogSnapshot Parse(string classesJson,string speciesJson,string backgroundsJson,string featsJson,string spellsJson,string equipmentJson,string weaponsJson,string rawSpellsJson)
    {
        var inputs=new[]{classesJson,speciesJson,backgroundsJson,featsJson,spellsJson,equipmentJson,weaponsJson,rawSpellsJson};
        long bytes=0;
        foreach(var input in inputs)
        {
            if(string.IsNullOrWhiteSpace(input))throw new ArgumentException("Catalog inputs must contain JSON.");
            var count=Encoding.UTF8.GetByteCount(input);
            if(count>MaximumInputBytes||(bytes+=count)>MaximumTotalBytes)throw new ArgumentException("Catalog inputs exceed their supported byte bounds.");
        }
        var budget=0;
        JsonNode ParseNode(string json)
        {
            JsonNode node;
            try{node=JsonNode.Parse(json,documentOptions:new(){MaxDepth=64})??throw new ArgumentException("A catalog cannot be JSON null.");}
            catch(JsonException error){throw new ArgumentException("A catalog contains invalid or excessively nested JSON.",error);}
            Count(node,ref budget);return node;
        }
        var keys=new[]{"classes","species","backgrounds","feats","spells","equipment","weapons"};
        var output=new JsonObject();
        for(var index=0;index<keys.Length;index++)
        {
            if(ParseNode(inputs[index]) is not JsonArray array||array.Count>MaximumRecords)throw new ArgumentException("Catalog collections must be bounded arrays.");
            if(keys[index]=="equipment")
            {
                if(array.Any(item=>item is not JsonValue value||!value.TryGetValue<string>(out _)))throw new ArgumentException("Equipment catalog entries must remain strings.");
            }
            else if(array.Any(item=>item is not JsonObject))throw new ArgumentException("Catalog record entries must be objects.");
            output[keys[index]]=Clean(array);
        }
        if(ParseNode(rawSpellsJson) is not JsonObject raw)throw new ArgumentException("The raw spell catalog must be an object.");
        var rawSpells=raw["spell"] is null?new JsonArray():raw["spell"] as JsonArray??throw new ArgumentException("Raw spell records must be an array.");
        if(rawSpells.Count>MaximumRecords||rawSpells.Any(item=>item is not JsonObject))throw new ArgumentException("Raw spell records must be bounded objects.");
        var spellReferences=new Dictionary<(string Name,string Source),JsonObject>(new SpellKeyComparer());
        var ritual=new Dictionary<(string Name,string Source),bool>(new SpellKeyComparer());
        foreach(var record in rawSpells.OfType<JsonObject>())
        {
            if(record["meta"] is not null&&record["meta"] is not JsonObject)throw new ArgumentException("Raw spell metadata must be an object.");
            ritual[(Text(record["name"]),Text(record["source"]))]=Truthy(record["meta"]?["ritual"]);
            spellReferences[(Text(record["name"]),Text(record["source"]))]=record;
        }
        foreach(var spell in output["spells"]!.AsArray().OfType<JsonObject>())
        {
            var name=Text(spell["name"]);if(name.Length==0)throw new ArgumentException("Each spell needs a nonempty name.");
            var source=spell.ContainsKey("source")?Text(spell["source"]):"XPHB";
            spell["ritual"]=ritual.GetValueOrDefault((name,source));
            if(spellReferences.TryGetValue((name,source),out var attackReference)&&attackReference["spellAttack"] is JsonArray attacks)
            {
                var kinds=attacks.Select(n=>Text(n)).Distinct().ToArray();spell["spell_attack_kind"]=kinds.SequenceEqual(new[]{"R"})?"ranged":kinds.SequenceEqual(new[]{"M"})?"melee":"mixed";
                spell["spell_attack_pattern"]=source!="XPHB"?"Unmodeled":name switch{
                    "Eldritch Blast"=>"EldritchBlast","Scorching Ray"=>"ScorchingRay",
                    "Fire Bolt" or "Chill Touch" or "Poison Spray" or "Ray of Frost" or "Shocking Grasp" or "Sorcerous Burst" or "Starry Wisp" or "Thorn Whip" or "Guiding Bolt" or "Ice Knife" or "Melf's Acid Arrow" or "Ray of Sickness" or "Witch Bolt"=>"Single",_=>"Unmodeled"};
            }
            if(spellReferences.TryGetValue((name,source),out var reference)&&reference["components"] is JsonObject components)
            {
                var values=new JsonArray();foreach(var (key,label) in new[]{("v","V"),("s","S"),("m","M")})if(components[key] is not null&&Truthy(components[key]))values.Add(label);
                spell["components"]=values;spell["components_known"]=true;
                var material=components["m"];
                if(material is JsonObject details){spell["material_description"]=Clean(details["text"]);spell["material_cost_cp"]=details["cost"]?.DeepClone();spell["material_consumed"]=details["consume"] is JsonValue flag&&flag.TryGetValue<bool>(out var consumed)&&consumed;spell["material_consumption_optional"]=details["consume"] is JsonValue optional&&optional.TryGetValue<string>(out var mode)&&mode=="optional";}
                else if(material is JsonValue text&&text.TryGetValue<string>(out var description)){spell["material_description"]=Clean(JsonValue.Create(description));spell["material_consumed"]=false;}
            }

        }
        return new(output);
    }
    private static void Count(JsonNode? node,ref int budget)
    {
        if(++budget>MaximumNodes)throw new ArgumentException("Catalog inputs exceed their supported node count.");
        if(node is JsonObject record)foreach(var pair in record)Count(pair.Value,ref budget);
        else if(node is JsonArray array)foreach(var item in array)Count(item,ref budget);
    }
    private static JsonNode? Clean(JsonNode? node)
    {
        if(node is JsonObject record)
        {
            var clean=new JsonObject();foreach(var pair in record)clean[pair.Key]=Clean(pair.Value);return clean;
        }
        if(node is JsonArray array)return new JsonArray(array.Select(Clean).ToArray());
        if(node is JsonValue value&&value.TryGetValue<string>(out var text)&&text.Contains("{@",StringComparison.Ordinal))
            return JsonValue.Create(Tags.Replace(text,match=>
            {
                var content=match.Groups[1].Value;var space=content.IndexOf(' ');
                var tag=(space<0?content:content[..space]).ToLowerInvariant();var body=space<0?"":content[(space+1)..];
                var parts=body.Split('|').Where(part=>part.Length>0).ToArray();
                if(parts.Length>0&&tag=="dc")return "DC "+parts[0];
                if(parts.Length>0&&tag=="filter")return parts[0];
                return parts.Length>=3?parts[2]:parts.Length>0?parts[0]:body;
            }).Trim(PythonWhitespace));
        return node?.DeepClone();
    }
    private static string Text(JsonNode? node)=>node is null?"":node is JsonValue value&&value.TryGetValue<string>(out var text)?text:throw new ArgumentException("Catalog spell identities must be text.");
    private static bool Truthy(JsonNode? node)=>node switch
    {
        null=>false,JsonObject record=>record.Count>0,JsonArray array=>array.Count>0,
        JsonValue value when value.TryGetValue<bool>(out var boolean)=>boolean,
        JsonValue value when value.TryGetValue<string>(out var text)=>text.Length>0,
        JsonValue value when value.TryGetValue<double>(out var number)=>number!=0,
        _=>throw new ArgumentException("A ritual marker contains unsupported JSON.")
    };
    private sealed class SpellKeyComparer:IEqualityComparer<(string Name,string Source)>
    {
        public bool Equals((string Name,string Source) x,(string Name,string Source) y)=>StringComparer.OrdinalIgnoreCase.Equals(x.Name,y.Name)&&StringComparer.OrdinalIgnoreCase.Equals(x.Source,y.Source);
        public int GetHashCode((string Name,string Source) key)=>HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(key.Name),StringComparer.OrdinalIgnoreCase.GetHashCode(key.Source));
    }
}
/// <summary>What a picker shows for one catalog record.</summary>
public sealed record CatalogEntry(string Name,string Description,string Source);
