using Herohold.Core;
namespace Herohold.Rules;
public sealed record NativeOriginFeatInstance(string SourceId,string Name,IReadOnlyDictionary<string,ChoiceValue> Choices,IReadOnlyList<string> ChoicePath,string ChoiceId,bool GeneralFeat);
public static class NativeOriginFeatInstances
{
    private static readonly HashSet<string> PlainNames=["Alert","Crafter","Healer","Lucky","Musician","Savage Attacker","Skilled","Tavern Brawler","Tough"];
    private static readonly HashSet<string> ToolNames=["Crafter","Musician","Skilled"];
    public static bool IsOrigin(string name)=>PlainNames.Contains(name)||name is "Magic Initiate (Cleric)" or "Magic Initiate (Druid)" or "Magic Initiate (Wizard)";
    public static IReadOnlyList<NativeOriginFeatInstance> Derive(BuildFacts build,CatalogBackground? background,CatalogSpecies? species)
    {
        ArgumentNullException.ThrowIfNull(build);var result=new List<NativeOriginFeatInstance>();var toolIndex=0;var magicIndex=0;
        void Add(string source,string name,IReadOnlyDictionary<string,ChoiceValue>? own,IReadOnlyList<string>? ownPath=null,bool general=false)
        {
            if(!IsOrigin(name))return;
            IReadOnlyList<string> path=ownPath??[];
            if(!general)
            {
                var index=ToolNames.Contains(name)?toolIndex++:name.StartsWith("Magic Initiate",StringComparison.Ordinal)?magicIndex++:-1;
                path=index<=0?["origin_feat_choices"]:["folio_origin_feat_choices",name];
                own=index<=0?build.OriginFeatChoices:build.HeroholdOriginFeatChoices.Get(name)?.Fields;
            }
            var choices=new Dictionary<string,ChoiceValue>(own??new Dictionary<string,ChoiceValue>(),StringComparer.Ordinal);
            if(System.Text.Json.JsonSerializer.Serialize(choices).Length>32768)throw new ArgumentException("Origin feat choices exceed their supported size.");
            if(name=="Skilled"&&choices.Get("proficiencies")?.Items is {} proficiencies)
            {
                var skills=new List<string>();var tools=new List<string>();
                foreach(var value in proficiencies.Select(p=>p.Text??""))
                    if(value.StartsWith("Skill: ",StringComparison.Ordinal))skills.Add(value[7..]);else if(value.StartsWith("Tool: ",StringComparison.Ordinal))tools.Add(value[6..]);
                choices["skills"]=ChoiceValue.ListOf(skills.Distinct(StringComparer.Ordinal).Select(ChoiceValue.Of));choices["tools"]=ChoiceValue.ListOf(tools.Distinct(StringComparer.Ordinal).Select(ChoiceValue.Of));
            }
            var identity=name.StartsWith("Magic Initiate",StringComparison.Ordinal)?new[]{"Cleric","Druid","Wizard"}.First(name.Contains):Slug(name);
            var id=general?"feat_"+Slug(source[5..]):"origin_"+identity+(source.StartsWith("origin:species:",StringComparison.Ordinal)?"_species":"");
            result.Add(new(source,name,choices,Array.AsReadOnly(path.ToArray()),id,general));
        }
        foreach(var name in (background?.OriginFeats??[]).Distinct(StringComparer.Ordinal))Add(Owner("background",background?.Name??"",name),name,null);
        if((species?.Traits??[]).Contains("Versatile",StringComparer.Ordinal))
            foreach(var name in ChoiceReading.Names(build.SpeciesOptions.Get("origin_feats")).Distinct(StringComparer.Ordinal))Add(Owner("species",species?.Name??"",name),name,null);
        var general=build.Feats??[];if(general.Count>100)throw new ArgumentException("The character has too many feat instances.");
        for(var index=0;index<general.Count;index++)
        {
            var record=general[index];var name=record.Name;if(!IsOrigin(name))continue;
            var id=record.InstanceId;if(id.Length==0)id="legacy-"+index+"-"+Slug(name);if(id.Length>100)throw new ArgumentException("A feat instance identity is too long.");
            Add("feat:"+id,name,record.Choices,["general_feats",index.ToString(System.Globalization.CultureInfo.InvariantCulture),"choices"],true);
        }
        if(result.Select(instance=>instance.SourceId).Distinct(StringComparer.Ordinal).Count()!=result.Count)throw new ArgumentException("Origin feat instance source IDs must be unique.");
        return result.AsReadOnly();
    }
    private static string Owner(string kind,string source,string name)
    {
        var id="origin:"+kind+":"+source+":"+name;return id.Length<=200?id:"origin:"+kind+":"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id))).ToLowerInvariant()[..24];
    }
    private static string Slug(string value)=>System.Text.RegularExpressions.Regex.Replace(value,"[^a-zA-Z0-9_-]+","_").Trim('_');
}
