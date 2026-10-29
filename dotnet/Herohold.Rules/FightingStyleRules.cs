using Herohold.Core;
namespace Herohold.Rules;
public sealed record FightingStyleSource(string Name,string SourceId);
public static class FightingStyleRules
{
    private static readonly HashSet<string> Styles=["Archery","Blind Fighting","Defense","Dueling","Great Weapon Fighting","Interception","Protection","Thrown Weapon Fighting","Two-Weapon Fighting","Unarmed Fighting"];
    public static IReadOnlyList<FightingStyleSource> Sources(BuildFacts build)
    {
        ArgumentNullException.ThrowIfNull(build);
        if(build.ClassTracks is not null)
        {
            var tracks=ClassTrackRules.Read(build);if(!tracks.CanCalculate)return [];
            return tracks.Tracks.SelectMany(t=>Sources(ClassTrackRules.Scope(build,t,t.Id==tracks.InitialTrackId)).Select(s=>s.SourceId.StartsWith("feat:",StringComparison.Ordinal)?s:s with{SourceId=t.Id+":"+s.SourceId})).Distinct().ToArray();
        }
        var result=new List<FightingStyleSource>();
        void Add(string name,string source){if((Styles.Contains(name)||name is "Blessed Warrior" or "Druidic Warrior")&&!result.Contains(new(name,source)))result.Add(new(name,source));}
        var options=build.ClassOptions;var className=build.ClassName;var level=build.Level;
        var primary=ChoiceReading.TextOf(options.Get("fighting_style"));
        var primaryActive=className=="Fighter"&&level>=1||className is "Paladin" or "Ranger"&&level>=2;
        if(primaryActive&&(Styles.Contains(primary)||primary=="Blessed Warrior"&&className=="Paladin"||primary=="Druidic Warrior"&&className=="Ranger"))Add(primary,"class_options:fighting_style");
        if(options.Get("champion_additional_fighting_styles")?.Items is {} choices)
        {
            if(choices.Count>10)throw new ArgumentException("There are too many additional fighting styles.");
            if(className=="Fighter"&&build.Subclass=="Champion"&&level>=7)
                foreach(var choice in choices)if(Styles.Contains(choice.Text??""))Add(choice.Text!,"class_options:champion_additional_fighting_styles");
        }
        var feats=build.Feats??[];if(feats.Count>100)throw new ArgumentException("There are too many permanent feat sources.");
        for(var index=0;index<feats.Count;index++)
        {
            var name=feats[index].Name;if(!Styles.Contains(name))continue;
            var id=feats[index].InstanceId;if(id.Length==0)id="legacy-style-"+index;if(id.Length>100)throw new ArgumentException("A fighting style source identity is too long.");Add(name,"feat:"+id);
        }
        return result.AsReadOnly();
    }
    public static IReadOnlyList<string> ActiveStyles(BuildFacts build)=>Sources(build).Select(source=>source.Name).Distinct(StringComparer.Ordinal).ToArray();
}
