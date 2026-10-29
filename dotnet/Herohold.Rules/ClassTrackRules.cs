using Herohold.Core;
namespace Herohold.Rules;
public sealed record ClassTrack(string Id,string ClassName,string RulesSource,int Level,string SubclassName,IReadOnlyDictionary<string,ChoiceValue> Choices);
public sealed record ClassTrackReview(IReadOnlyList<ClassTrack> Tracks,string InitialTrackId,IReadOnlyList<BuildIssue> Issues)
{public bool CanCalculate=>Issues.All(i=>i.Severity!=BuildIssueSeverity.Error);}
public static class ClassTrackRules
{
    private static BuildIssue Issue(string code,string message)=>new(code,["folio_class_tracks"],message);
    /// <summary>The tracks of a build: its recorded class tracks, or its single class.</summary>
    public static ClassTrackReview Read(BuildFacts source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tracks=new List<ClassTrack>();var issues=new List<BuildIssue>();var total=source.Level;var name=source.ClassName;
        if(source.ClassTracks is {} input)
        {
            if(source.ClassTracksVersion is {} version&&version!=1)issues.Add(Issue("unsupported_class_tracks_version","This class track version needs a newer application; preserve the original document."));
            if(input.Count is <1 or >12)issues.Add(Issue("invalid_class_tracks","Use one to twelve distinct class tracks."));
            foreach(var t in input)
            {
                if(t.Id is not {Length:>0 and <=100} id||t.ClassName is not {Length:>0 and <=100} cls||t.Level is <1 or >20)
                {issues.Add(Issue("invalid_class_track","A class track needs a stable identity, class level and owned choices."));continue;}
                if(id.Length>64||!System.Text.RegularExpressions.Regex.IsMatch(id,"^[a-zA-Z0-9_-]+$"))issues.Add(Issue("invalid_class_track_id","Class track IDs use at most64 letters, digits, underscores or hyphens."));
                tracks.Add(new(id,cls,t.RulesSource??"",t.Level,t.Subclass??"",t.Choices));
            }
        }
        else if(source.ClassLevels is {Count:>1})issues.Add(Issue("missing_class_tracks","Multiclass imports need initial-class history and source-owned class choices before recalculation."));
        else if(name.Length>0)
        {
            var tree=source.AsChoiceTree();var choices=new Dictionary<string,ChoiceValue>(StringComparer.Ordinal);foreach(var key in new[]{"class_options","spells","weapon_masteries"})choices[key]=tree.At([key])??new();
            tracks.Add(new("class-"+name.ToLowerInvariant(),name,"XPHB",total,source.Subclass,choices));
        }
        var initial=source.InitialClassTrackId??"";if(initial.Length==0&&tracks.Count==1)initial=tracks[0].Id;
        if(total is <1 or >20||tracks.Sum(t=>t.Level)!=total)issues.Add(Issue("inconsistent_class_level","Class levels must sum to the character level, at most20."));
        if(tracks.Select(t=>t.Id).Distinct(StringComparer.Ordinal).Count()!=tracks.Count||tracks.Select(t=>t.ClassName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=tracks.Count)issues.Add(Issue("duplicate_class_track","Class tracks and class names must be unique."));
        if(!tracks.Any(t=>t.Id==initial))issues.Add(Issue("missing_initial_class","Identify the character's initial class; import order is not sufficient."));
        if(source.ClassLevels is {Count:>0} counts&&(counts.Count!=tracks.Count||tracks.Any(t=>!counts.Any(p=>p.Key.Equals(t.ClassName,StringComparison.OrdinalIgnoreCase)&&p.Value==t.Level))))issues.Add(Issue("inconsistent_class_level","Class-level compatibility values disagree with the class tracks."));
        return new(tracks,initial,issues);
    }
    /// <summary>The build with these class tracks written back, its class levels and total level recalculated.</summary>
    public static BuildFacts WriteClone(BuildFacts source,ClassTrackReview review)
    {
        if(!review.CanCalculate)throw new ArgumentException("Resolve class track issues before changing progression.");
        var result=source with{ClassTracksVersion=1,InitialClassTrackId=review.InitialTrackId,
            ClassTracks=[..review.Tracks.Select(t=>new ClassTrackFact{Id=t.Id,ClassName=t.ClassName,RulesSource=t.RulesSource,Level=t.Level,Subclass=t.SubclassName,Choices=new(t.Choices,StringComparer.Ordinal)})],
            ClassLevels=review.Tracks.ToDictionary(t=>t.ClassName,t=>t.Level),Level=review.Tracks.Sum(t=>t.Level)};
        if(!Read(result).CanCalculate)throw new ArgumentException("The class track candidate is inconsistent.");
        return result;
    }
    /// <summary>One class track's own build, as a single-class character of that track's level.</summary>
    public static BuildFacts Scope(BuildFacts source,ClassTrack track,bool initial)
    {
        List<string>? Names(ChoiceValue? spells,string key)=>spells?.Fields?.GetValueOrDefault(key) is {} list?[..ChoiceReading.Names(list)]:null;
        var spells=track.Choices.Get("spells");
        return source with{ClassTracks=null,InitialClassTrackId=null,ClassTracksVersion=null,ClassName=track.ClassName,Level=track.Level,Subclass=track.SubclassName,
            ClassLevels=new(){[track.ClassName]=track.Level},HpHistory=null,
            ClassOptions=new(track.Choices.Get("class_options")?.Fields??new Dictionary<string,ChoiceValue>(),StringComparer.Ordinal),
            Spells=new(){Cantrips=Names(spells,"cantrips"),Known=Names(spells,"known"),Prepared=Names(spells,"prepared"),Spellbook=Names(spells,"spellbook")},
            WeaponMasteries=[..ChoiceReading.Names(track.Choices.Get("weapon_masteries"))]};
    }
}
