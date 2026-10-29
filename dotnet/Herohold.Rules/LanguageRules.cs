using Herohold.Core;
namespace Herohold.Rules;
public sealed record LanguageSummary
{
    public IReadOnlyList<string> Languages {get;init;}=[];
    public IReadOnlyList<string> Manual {get;init;}=[];
    public IReadOnlyList<string> Derived {get;init;}=[];
    public IReadOnlyList<ChoiceProficiencySource> Sources {get;init;}=[];
    public IReadOnlyList<string> Issues {get;init;}=[];
}
public static class LanguageRules
{
    public static IReadOnlyList<string> StandardLanguages {get;}=Array.AsReadOnly(new[]{"Common Sign Language","Draconic","Dwarvish","Elvish","Giant","Gnomish","Goblin","Halfling","Orc"});
    /// <param name="existing">The languages the character showed before this calculation; unexplained ones are kept as hand-entered.</param>
    /// <param name="previouslyDerived">The languages the rules derived last time, so a removed source drops its language.</param>
    public static LanguageSummary Derive(BuildFacts build,IReadOnlyList<ChoiceProficiencySource> grantedSources,bool allowUnresolved=false,IReadOnlyList<string>? existing=null,IReadOnlyList<string>? previouslyDerived=null)
    {
        ArgumentNullException.ThrowIfNull(build);ArgumentNullException.ThrowIfNull(grantedSources);
        if(grantedSources.Count>100||grantedSources.Any(source=>source is null||source.Kind!=ProficiencySourceKind.Language||string.IsNullOrWhiteSpace(source.SourceId)||source.SourceId.Length>200||string.IsNullOrWhiteSpace(source.Target)||source.Target.Length>200))throw new ArgumentException("Language sources must be bounded, named language grants.");
        var manual=Names(build.ManualLanguages);var previous=Names(previouslyDerived).ToHashSet(StringComparer.Ordinal);var shown=Names(existing);
        var issues=new List<string>();var selected=new List<string>();
        void Unresolved(string message,bool invalid=false){if(invalid&&!allowUnresolved)throw new ArgumentException(message);issues.Add(message);}
        if(build.OriginLanguages is {} choices)
        {
            if(choices.Count>100)throw new ArgumentException("Origin language choices exceed their supported bound.");
            foreach(var text in choices)
            {
                if(!StandardLanguages.Contains(text,StringComparer.Ordinal)){Unresolved("Choose origin languages only from the standard language list.",true);continue;}
                if(selected.Contains(text,StringComparer.Ordinal)){Unresolved("Choose two different origin languages.",true);continue;}
                selected.Add(text);
            }
            if(choices.Count>2)Unresolved("Choose no more than two origin languages.",true);
        }
        if(selected.Count<2)Unresolved("Choose two standard origin languages; existing imported languages have been retained.");
        var sources=new List<ChoiceProficiencySource>{new("origin:common",ProficiencySourceKind.Language,"Common")};
        sources.AddRange(selected.Select(name=>new ChoiceProficiencySource("origin:languages",ProficiencySourceKind.Language,name)));sources.AddRange(grantedSources.Select(source=>source with{}));
        var derived=sources.Select(source=>source.Target).Distinct(StringComparer.Ordinal).ToArray();
        foreach(var name in shown.Except(derived,StringComparer.Ordinal).Except(previous,StringComparer.Ordinal))
            if(!manual.Contains(name,StringComparer.Ordinal)){manual.Add(name);issues.Add("Retained imported or unattributed language: "+name);}
        foreach(var name in manual)sources.Add(new("manual:languages",ProficiencySourceKind.Language,name));
        var all=derived.Concat(manual).Distinct(StringComparer.Ordinal).ToArray();if(all.Length>100)throw new ArgumentException("The character has too many languages.");
        return new(){Languages=Array.AsReadOnly(all),Manual=manual.AsReadOnly(),Derived=Array.AsReadOnly(derived),Sources=sources.Distinct().ToList().AsReadOnly(),Issues=issues.Distinct().ToList().AsReadOnly()};
    }
    private static List<string> Names(IReadOnlyList<string>? names)
    {
        if(names is null)return [];
        if(names.Count>100)throw new ArgumentException("Language lists must contain at most 100 names.");
        var result=new List<string>();
        foreach(var name in names)
        {
            if(string.IsNullOrWhiteSpace(name)||name.Length>200)throw new ArgumentException("Language names must be nonempty text of at most 200 characters.");
            if(!result.Contains(name,StringComparer.Ordinal))result.Add(name);
        }
        return result;
    }
}
