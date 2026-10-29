using System.Collections.ObjectModel;
using Herohold.Core;
namespace Herohold.Rules;
public static class AbilityScoreRules
{
    public static AbilityScoreSummary Calculate(AbilityScoreInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if(input.ClassName is null||input.ClassName.Length>200||input.Level is <1 or >20||input.BaseScores is null||input.Adjustments is null||input.Adjustments.Count>100)
            throw new ArgumentException("Ability score inputs exceed the supported bounds.");
        if(input.BaseScores.Any(pair=>!Enum.IsDefined(pair.Key)||pair.Value is <1 or >30))throw new ArgumentException("Base scores must use valid abilities and scores1–30.");
        var scores=Enum.GetValues<Ability>().ToDictionary(a=>a,a=>input.BaseScores.GetValueOrDefault(a,10));
        var background=scores.ToDictionary(pair=>pair.Key,_=>0);var bonuses=scores.ToDictionary(pair=>pair.Key,_=>0);
        var maximums=scores.ToDictionary(pair=>pair.Key,_=>20);
        var terms=scores.ToDictionary(pair=>pair.Key,pair=>new List<CalculationTerm>{new("Base score",pair.Value)});
        void Apply(Ability ability,int amount,int maximum,string label,Dictionary<Ability,int> totals)
        {
            var applied=Math.Min(amount,Math.Max(0,maximum-scores[ability]));
            scores[ability]+=applied;totals[ability]+=applied;terms[ability].Add(new(label+(applied<amount?" · capped at "+maximum:""),applied));
        }
        if(input.Background is {} bg)
        {
            if(bg.Name is null||bg.Name.Length>200||bg.AllowedAbilities is null||bg.AllowedAbilities.Count>6
                ||bg.AllowedAbilities.Any(a=>!Enum.IsDefined(a))||bg.AllowedAbilities.Distinct().Count()!=bg.AllowedAbilities.Count
                ||bg.PrimaryBonus is <0 or >2||bg.SecondaryBonus is <0 or >1
                ||bg.Primary is {} primary&&(!Enum.IsDefined(primary)||!bg.AllowedAbilities.Contains(primary))
                ||bg.Secondary is {} secondary&&(!Enum.IsDefined(secondary)||!bg.AllowedAbilities.Contains(secondary))
                ||bg.Primary.HasValue&&bg.Primary==bg.Secondary
                ||bg.SplitEvenly&&(bg.AllowedAbilities.Count!=3||bg.Primary.HasValue||bg.Secondary.HasValue))
                throw new ArgumentException("Background ability choices must be distinct allowed abilities with bounded bonuses.");
            if(bg.SplitEvenly)foreach(var ability in bg.AllowedAbilities)Apply(ability,1,20,bg.Name,background);
            else
            {
                if(bg.Primary is {} first)Apply(first,bg.PrimaryBonus,20,bg.Name,background);
                if(bg.Secondary is {} second)Apply(second,bg.SecondaryBonus,20,bg.Name,background);
            }
        }
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var source in input.Adjustments)
        {
            if(source is null||string.IsNullOrWhiteSpace(source.SourceId)||source.SourceId.Length>200||!ids.Add(source.SourceId)
                ||source.SourceName is null||source.SourceName.Length>200||source.Maximum is <1 or >30||source.Increases is null||source.Increases.Count is <1 or >6
                ||source.Increases.Any(pair=>!Enum.IsDefined(pair.Key)||pair.Value is <1 or >10))
                throw new ArgumentException("Ability score sources need unique IDs and valid bounded positive increases.");
            foreach(var pair in source.Increases)Apply(pair.Key,pair.Value,source.Maximum,string.IsNullOrWhiteSpace(source.SourceName)?source.SourceId:source.SourceName,bonuses);
        }
        if(input.Level==20)
        {
            var abilities=input.ClassName=="Barbarian"?new[]{Ability.Strength,Ability.Constitution}:input.ClassName=="Monk"?new[]{Ability.Dexterity,Ability.Wisdom}:[];
            var name=input.ClassName=="Barbarian"?"Primal Champion":"Body and Mind";
            var id="class:"+input.ClassName+":20:"+name;
            if(abilities.Length>0&&ids.Contains(id))throw new ArgumentException("Automatic class score sources must not also be supplied explicitly.");
            foreach(var ability in abilities){Apply(ability,4,25,name,bonuses);maximums[ability]=25;}
        }
        return new(){Scores=ReadOnly(scores),BackgroundBonuses=ReadOnly(background),SourceBonuses=ReadOnly(bonuses),Maximums=ReadOnly(maximums),
            Calculations=ReadOnly(terms.ToDictionary(pair=>pair.Key,pair=>new Calculation(scores[pair.Key],pair.Value.AsReadOnly())))};
    }
    private static IReadOnlyDictionary<TKey,TValue> ReadOnly<TKey,TValue>(Dictionary<TKey,TValue> dictionary)where TKey:notnull=>new ReadOnlyDictionary<TKey,TValue>(dictionary);
}
