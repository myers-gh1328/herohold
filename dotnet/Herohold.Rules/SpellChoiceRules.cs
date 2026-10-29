namespace Herohold.Rules;
public sealed record SpellChoiceQuery
{
    public IReadOnlyList<string> Classes { get; init; } = [];
    public IReadOnlyList<int> Levels { get; init; } = [];
    public string SchoolCode { get; init; } = "";
    public string CastingTime { get; init; } = "";
    public IReadOnlyList<string> ExcludedNames { get; init; } = [];
    public bool SortByName { get; init; }
}
public static class SpellChoiceRules
{
    public static IReadOnlyList<string> Filter(IReadOnlyList<CatalogSpell> spells,SpellChoiceQuery query)
    {
        ArgumentNullException.ThrowIfNull(spells);ArgumentNullException.ThrowIfNull(query);
        if(spells.Count>50000||query.Classes is null||query.Classes.Count>20||query.Classes.Any(name=>string.IsNullOrWhiteSpace(name)||name.Length>200)
            ||query.Levels is null||query.Levels.Count>10||query.Levels.Any(level=>level is <0 or >9)||query.SchoolCode is null||query.SchoolCode.Length>20||query.CastingTime is null||query.CastingTime.Length>200
            ||query.ExcludedNames is null||query.ExcludedNames.Count>5000)throw new ArgumentException("Spell query inputs exceed supported bounds.");
        var excluded=query.ExcludedNames.ToHashSet(StringComparer.Ordinal);var names=new List<string>();
        foreach(var spell in spells)
        {
            var name=spell.Name;var level=spell.Level;var classes=spell.Classes;
            if(name.Length==0||excluded.Contains(name)||query.Levels.Count>0&&!query.Levels.Contains(level)
                ||query.Classes.Count>0&&!classes.Intersect(query.Classes,StringComparer.Ordinal).Any()
                ||query.SchoolCode.Length>0&&spell.SchoolCode!=query.SchoolCode
                ||query.CastingTime.Length>0&&spell.CastingTime!=query.CastingTime)continue;
            if(!names.Contains(name,StringComparer.Ordinal))names.Add(name);
        }
        if(query.SortByName)names.Sort(StringComparer.Ordinal);
        return names.AsReadOnly();
    }
}
