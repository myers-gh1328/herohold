using System.Text.Json.Nodes;
using Herohold.Core;
namespace Herohold.Rules;
public sealed record BuildFactOwnershipSummary
{
    public IReadOnlyDictionary<string,IReadOnlyList<string>> Values {get;init;}=new Dictionary<string,IReadOnlyList<string>>();
    public IReadOnlyDictionary<string,IReadOnlyList<string>> Manual {get;init;}=new Dictionary<string,IReadOnlyList<string>>();
    public IReadOnlyDictionary<string,IReadOnlyList<string>> Derived {get;init;}=new Dictionary<string,IReadOnlyList<string>>();
    public IReadOnlyList<string> Warnings {get;init;}=[];
    public JsonObject ToSidecar()
    {
        static JsonObject Map(IReadOnlyDictionary<string,IReadOnlyList<string>> values)=>new(values.Select(pair=>KeyValuePair.Create<string,JsonNode?>(pair.Key,new JsonArray(pair.Value.Select(name=>(JsonNode?)JsonValue.Create(name)).ToArray()))));
        return new(){["manual"]=Map(Manual),["derived"]=Map(Derived)};
    }
}
public static class BuildFactOwnershipRules
{
    public static IReadOnlyList<string> Fields {get;}=Array.AsReadOnly(new[]{"tool_proficiencies","armor_proficiencies","weapon_proficiencies","damage_resistances","damage_immunities","damage_vulnerabilities","condition_immunities"});
    /// <summary>The ownership a rules character document records (its "folio_build_fact_sources") and the lists it showed.</summary>
    public static BuildFactOwnershipSummary Derive(JsonObject canonical,IReadOnlyDictionary<string,IReadOnlyList<string>> currentDerived)=>Derive(ShownOwnership.From(canonical),currentDerived);
    /// <summary>
    /// Which proficiencies and defenses were entered by hand: those recorded as manual before, and those shown that
    /// neither the build gives now nor gave before.
    /// </summary>
    public static BuildFactOwnershipSummary Derive(ShownOwnership shown,IReadOnlyDictionary<string,IReadOnlyList<string>> currentDerived)
    {
        ArgumentNullException.ThrowIfNull(shown);ArgumentNullException.ThrowIfNull(currentDerived);
        foreach(var map in new[]{shown.Shown,shown.PriorManual,shown.PriorDerived,currentDerived})if(map.Any(pair=>!Fields.Contains(pair.Key,StringComparer.Ordinal)))throw new ArgumentException("Derived build facts contain unsupported fields.");
        IReadOnlyList<string> Bounded(IEnumerable<string> names)
        {
            var values=names.Take(1001).ToArray();if(values.Length>1000||values.Any(name=>string.IsNullOrWhiteSpace(name)||name.Length>200))throw new ArgumentException("Build facts must contain at most1000 names per field, each1–200 characters.");
            return Array.AsReadOnly(values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }
        IReadOnlyList<string> Read(IReadOnlyDictionary<string,IReadOnlyList<string>> map,string field)=>map.TryGetValue(field,out var names)?Bounded(names??throw new ArgumentException("Build fact lists cannot be null.")):[];
        var values=new Dictionary<string,IReadOnlyList<string>>();var manual=new Dictionary<string,IReadOnlyList<string>>();var derived=new Dictionary<string,IReadOnlyList<string>>();var warnings=new List<string>();
        foreach(var field in Fields)
        {
            var current=currentDerived.TryGetValue(field,out var supplied)?Bounded(supplied??throw new ArgumentException("Derived build fact lists cannot be null.")):[];
            var previous=Read(shown.PriorDerived,field);var recorded=Read(shown.PriorManual,field);
            var unexplained=Read(shown.Shown,field).Except(current,StringComparer.OrdinalIgnoreCase).Except(previous,StringComparer.OrdinalIgnoreCase);
            manual[field]=Bounded(recorded.Concat(unexplained));derived[field]=current;values[field]=Bounded(manual[field].Concat(current));
            if(manual[field].Count>0)warnings.Add("Retained manual or imported "+field.Replace('_',' ')+"; their source is not automatically reconstructed.");
        }
        static IReadOnlyDictionary<string,IReadOnlyList<string>> Freeze(Dictionary<string,IReadOnlyList<string>> map)=>new System.Collections.ObjectModel.ReadOnlyDictionary<string,IReadOnlyList<string>>(map);
        return new(){Values=Freeze(values),Manual=Freeze(manual),Derived=Freeze(derived),Warnings=warnings.AsReadOnly()};
    }
}

/// <summary>The proficiency and defense lists a character showed, with the hand-entered and derived lists it recorded last time.</summary>
public sealed record ShownOwnership
{
    public static readonly ShownOwnership None=new();
    public IReadOnlyDictionary<string,IReadOnlyList<string>> Shown {get;init;}=new Dictionary<string,IReadOnlyList<string>>();
    public IReadOnlyDictionary<string,IReadOnlyList<string>> PriorManual {get;init;}=new Dictionary<string,IReadOnlyList<string>>();
    public IReadOnlyDictionary<string,IReadOnlyList<string>> PriorDerived {get;init;}=new Dictionary<string,IReadOnlyList<string>>();

    /// <summary>Hand-entered facts recorded last time, with nothing shown beside them.</summary>
    public static ShownOwnership Recorded(ManualBuildFacts? manual)=>new(){PriorManual=Lists(manual)};
    public static IReadOnlyDictionary<string,IReadOnlyList<string>> Lists(ManualBuildFacts? manual)=>manual is null?new Dictionary<string,IReadOnlyList<string>>():new Dictionary<string,IReadOnlyList<string>>
    {
        ["tool_proficiencies"]=manual.ToolProficiencies,["armor_proficiencies"]=manual.ArmorProficiencies,["weapon_proficiencies"]=manual.WeaponProficiencies,
        ["damage_resistances"]=manual.DamageResistances,["damage_immunities"]=manual.DamageImmunities,["damage_vulnerabilities"]=manual.DamageVulnerabilities,["condition_immunities"]=manual.ConditionImmunities
    };
    /// <summary>What a rules character document showed and recorded.</summary>
    public static ShownOwnership From(JsonObject canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        JsonObject Object(JsonNode? node)=>node is null?new():node as JsonObject??throw new ArgumentException("Build fact ownership must contain objects.");
        var sidecar=Object(canonical["folio_build_fact_sources"]);if(sidecar.Any(pair=>pair.Key is not ("manual" or "derived")))throw new ArgumentException("Build fact ownership contains unsupported sections.");
        var oldManual=Object(sidecar["manual"]);var oldDerived=Object(sidecar["derived"]);
        foreach(var map in new[]{oldManual,oldDerived})if(map.Any(pair=>!BuildFactOwnershipRules.Fields.Contains(pair.Key,StringComparer.Ordinal)))throw new ArgumentException("Build fact ownership contains unsupported fields.");
        static IReadOnlyList<string> Read(JsonNode? node)
        {
            if(node is null)return [];
            if(node is not JsonArray array||array.Any(n=>n is not JsonValue value||!value.TryGetValue<string>(out _)))throw new ArgumentException("Build fact lists must contain text names.");
            return [..array.Select(n=>n!.GetValue<string>())];
        }
        Dictionary<string,IReadOnlyList<string>> Lists(Func<string,JsonNode?> field)=>BuildFactOwnershipRules.Fields.ToDictionary(f=>f,f=>Read(field(f)));
        return new(){Shown=Lists(f=>canonical[f]),PriorManual=Lists(f=>oldManual[f]),PriorDerived=Lists(f=>oldDerived[f])};
    }
}
