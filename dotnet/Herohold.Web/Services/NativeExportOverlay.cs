using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Rules;
namespace Herohold.Web.Services;
public static class NativeExportOverlay
{
    private static readonly string[] Required=["ability_scores","ability_modifiers","ability_score_bonuses","ability_score_adjustment_bonuses","saving_throw_bonuses","skill_bonuses","saving_throw_proficiencies","skill_proficiencies","expertise","initiative","armor_class","speed"];
    private static readonly string[] Optional=["armor_class_details","speed_details","movement_modes","derived_resources","spellcasting_sources"];
    public static void Apply(JsonObject character,JsonObject snapshot)
    {
        void Invalid()=>throw new CharacterWorkflowException("invalid_native_stats","The numerical export snapshot is invalid.");
        if(Required.Any(k=>!snapshot.ContainsKey(k))||snapshot.Any(p=>!Required.Contains(p.Key)&&!Optional.Contains(p.Key)))Invalid();
        var abilities=Enum.GetNames<Ability>();var skills=NativeExportProjection.SkillNames;
        foreach(var p in new[]{("ability_scores",(IReadOnlyList<string>)abilities,1,30),("ability_modifiers",abilities,-20000,20000),("ability_score_bonuses",abilities,0,30),("ability_score_adjustment_bonuses",abilities,0,30),("saving_throw_bonuses",abilities,-20000,20000),("skill_bonuses",skills,-20000,20000)})
        {if(snapshot[p.Item1] is not JsonObject values||values.Count!=p.Item2.Count||p.Item2.Any(k=>values[k] is not JsonValue value||!value.TryGetValue<int>(out var number)||number<p.Item3||number>p.Item4))Invalid();}
        foreach(var p in new[]{("saving_throw_proficiencies",(IReadOnlyList<string>)abilities),("skill_proficiencies",skills),("expertise",skills)})
        {if(snapshot[p.Item1] is not JsonArray list||list.Count>p.Item2.Count||list.Any(n=>n is not JsonValue value||!value.TryGetValue<string>(out var text)||!p.Item2.Contains(text))||list.Select(n=>n!.GetValue<string>()).Distinct().Count()!=list.Count)Invalid();}
        if(snapshot["expertise"]!.AsArray().Any(n=>!snapshot["skill_proficiencies"]!.AsArray().Any(s=>s!.GetValue<string>()==n!.GetValue<string>())))Invalid();
        foreach(var p in new[]{("initiative",-20000,20000),("armor_class",0,100),("speed",0,10000)})if(snapshot[p.Item1] is not JsonValue value||!value.TryGetValue<int>(out var number)||number<p.Item2||number>p.Item3)Invalid();
        foreach(var p in new[]{("armor_class_details","armor_class"),("speed_details","speed")})if(snapshot.ContainsKey(p.Item1)&&(snapshot[p.Item1] is not JsonObject detail||detail["value"] is not JsonValue value||!value.TryGetValue<int>(out var number)||number!=snapshot[p.Item2]!.GetValue<int>()))Invalid();
        if(snapshot.ContainsKey("derived_resources")&&(snapshot["derived_resources"] is not JsonObject resources||resources.Count>500))Invalid();
        foreach(var field in new[]{"movement_modes","spellcasting_sources"})if(snapshot.ContainsKey(field)&&(snapshot[field] is not JsonArray rows||rows.Count>100||rows.Any(n=>n is not JsonObject)))Invalid();
        foreach(var p in snapshot)character[p.Key]=p.Value?.DeepClone();
    }
}
