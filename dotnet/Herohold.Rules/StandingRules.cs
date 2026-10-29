using System.Collections.ObjectModel;
using Herohold.Core;
namespace Herohold.Rules;
public static class StandingRules
{
    internal static bool IsSupportedSkill(string name)=>SkillAbilities.ContainsKey(name);
    internal static IEnumerable<string> SupportedSkills=>SkillAbilities.Keys;
    public static readonly IReadOnlyDictionary<string,Ability> SkillAbilities=new ReadOnlyDictionary<string,Ability>(new Dictionary<string,Ability>{
        ["Acrobatics"]=Ability.Dexterity,["Animal Handling"]=Ability.Wisdom,["Arcana"]=Ability.Intelligence,["Athletics"]=Ability.Strength,
        ["Deception"]=Ability.Charisma,["History"]=Ability.Intelligence,["Insight"]=Ability.Wisdom,["Intimidation"]=Ability.Charisma,
        ["Investigation"]=Ability.Intelligence,["Medicine"]=Ability.Wisdom,["Nature"]=Ability.Intelligence,["Perception"]=Ability.Wisdom,
        ["Performance"]=Ability.Charisma,["Persuasion"]=Ability.Charisma,["Religion"]=Ability.Intelligence,["Sleight of Hand"]=Ability.Dexterity,
        ["Stealth"]=Ability.Dexterity,["Survival"]=Ability.Wisdom});
    public static StandingSummary Calculate(StandingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if(input.ClassName is null||input.ClassName.Length>200||input.Subclass is null||input.Subclass.Length>200||input.Level is <1 or >20
            ||input.Scores is null||input.Scores.Count!=6||input.Scores.Any(pair=>!Enum.IsDefined(pair.Key)||pair.Value is <1 or >30)
            ||input.Equipment is null||input.Equipment.Count>500||input.Feats is null||input.Feats.Count>100
            ||input.SaveProficiencies is null||input.SaveProficiencies.Count>6||input.SaveProficiencies.Any(a=>!Enum.IsDefined(a))
            ||input.SkillProficiencies is null||input.SkillProficiencies.Count>18||input.Expertise is null||input.Expertise.Count>18
            ||input.OriginFeats is null||input.OriginFeats.Count>100||input.OriginFeats.Any(name=>string.IsNullOrWhiteSpace(name)||name.Length>200)
            ||input.DivineOrder is {} divine&&(!Enum.IsDefined(divine)||input.ClassName!="Cleric")
            ||input.PrimalOrder is {} primal&&(!Enum.IsDefined(primal)||input.ClassName!="Druid"))
            throw new ArgumentException("Standing inputs exceed their supported bounds or contain invalid class choices.");
        static void Skill(string name)
        {if(string.IsNullOrWhiteSpace(name)||!SkillAbilities.ContainsKey(name))throw new ArgumentException("Choose a supported skill name.");}
        var skills=new HashSet<string>(input.SkillProficiencies,StringComparer.Ordinal);var expertise=new HashSet<string>(input.Expertise,StringComparer.Ordinal);
        foreach(var name in skills.Concat(expertise))Skill(name);
        var saves=new HashSet<Ability>(input.SaveProficiencies);var grants=new List<NumericProficiencyGrant>();var unmodeled=new List<string>();
        void AddSkill(string name,string source){Skill(name);if(skills.Add(name))grants.Add(new(source,"skill",name));}
        void AddExpertise(string name,string source){Skill(name);if(expertise.Add(name))grants.Add(new(source,"expertise",name));}
        var featNames=new HashSet<string>(StringComparer.Ordinal);
        foreach(var feat in input.Feats)
        {
            if(feat is null||string.IsNullOrWhiteSpace(feat.Name)||feat.Name.Length>200
                ||feat.SkillProficiency is null||feat.SkillProficiency.Length>200||feat.Expertise is null||feat.Expertise.Length>200
                ||feat.Skill is null||feat.Skill.Length>200||feat.Ability is {} ability&&!Enum.IsDefined(ability))
                throw new ArgumentException("Numeric feat selections need distinct valid names and typed choices.");
            if((feat.Name is "Skill Expert" or "Keen Mind" or "Observant" or "Resilient" or "Boon of Skill")&&!featNames.Add(feat.Name))
                throw new ArgumentException("A nonrepeatable numeric feat must not be applied twice.");
            switch(feat.Name)
            {
                case "Skill Expert":AddSkill(feat.SkillProficiency,feat.Name);AddExpertise(feat.Expertise,feat.Name);break;
                case "Keen Mind":
                case "Observant":
                    var allowed=feat.Name=="Keen Mind"?new[]{"Arcana","History","Investigation","Nature","Religion"}:new[]{"Insight","Investigation","Perception"};
                    if(!allowed.Contains(feat.Skill))throw new ArgumentException("That skill is not an eligible choice for the selected feat.");
                    if(skills.Contains(feat.Skill))AddExpertise(feat.Skill,feat.Name);else AddSkill(feat.Skill,feat.Name);break;
                case "Resilient":
                    if(feat.Ability is not {} save)throw new ArgumentException("Resilient needs a selected ability.");
                    if(saves.Add(save))grants.Add(new(feat.Name,"save",save.ToString()));break;
                case "Boon of Skill":foreach(var name in SkillAbilities.Keys)AddSkill(name,feat.Name);AddExpertise(feat.Expertise,feat.Name);break;
                default:if(!unmodeled.Contains(feat.Name))unmodeled.Add(feat.Name);break;
            }
        }
        if(expertise.Any(name=>!skills.Contains(name)))throw new ArgumentException("Expertise requires skill proficiency after resolving feat grants.");
        var character=new CharacterState{Id="calculation",Name="Calculation",ClassName=input.ClassName,Subclass=input.Subclass,Level=input.Level,
            MaximumHp=1,CurrentHp=1,AbilityScores=input.Scores.ToDictionary(pair=>pair.Key,pair=>pair.Value),
            Equipment=input.Equipment.Select(item=>item is null?throw new ArgumentException("Equipment items cannot be null."):item with{ModifierBonuses=new(item.ModifierBonuses)}).ToList(),
            SaveProficiencies=saves.ToList(),SkillProficiencies=skills.ToList(),Expertise=expertise.ToList()};
        WorkspaceRules.Validate(character);
        var modifiers=Enum.GetValues<Ability>().ToDictionary(a=>a,a=>CharacterCalculations.Ability(character,a));
        var savingThrows=Enum.GetValues<Ability>().ToDictionary(a=>a,a=>CharacterCalculations.Save(character,a));
        var checks=new Dictionary<string,Calculation>();var proficiency=CharacterCalculations.ProficiencyBonus(input.Level);
        foreach(var pair in SkillAbilities)
        {
            var calculation=CharacterCalculations.Skill(character,pair.Key,pair.Value);
            if(input.ClassName=="Bard"&&input.Level>=2&&!skills.Contains(pair.Key))calculation=Add(calculation,"Jack of All Trades · half proficiency",proficiency/2);
            if(input.DivineOrder==DivineOrder.Thaumaturge&&(pair.Key is "Arcana" or "Religion"))calculation=Add(calculation,"Thaumaturge · Wisdom (minimum1)",Math.Max(1,modifiers[Ability.Wisdom].Total));
            if(input.PrimalOrder==PrimalOrder.Magician&&(pair.Key is "Arcana" or "Nature"))calculation=Add(calculation,"Magician · Wisdom (minimum1)",Math.Max(1,modifiers[Ability.Wisdom].Total));
            checks.Add(pair.Key,calculation);
        }
        var initiative=CharacterCalculations.Initiative(character);
        if(input.OriginFeats.Contains("Alert",StringComparer.Ordinal))initiative=Add(initiative,"Alert · proficiency",proficiency);
        return new(){Modifiers=Freeze(modifiers),Saves=Freeze(savingThrows),Skills=Freeze(checks),Initiative=initiative,
            SaveProficiencies=saves.ToList().AsReadOnly(),SkillProficiencies=skills.ToList().AsReadOnly(),Expertise=expertise.ToList().AsReadOnly(),Grants=grants.AsReadOnly(),UnmodeledStandingFeats=unmodeled.AsReadOnly()};
    }
    private static Calculation Add(Calculation calculation,string source,int amount)=>new(calculation.Total+amount,calculation.Terms.Append(new(source,amount)).ToList().AsReadOnly());
    private static IReadOnlyDictionary<TKey,TValue> Freeze<TKey,TValue>(Dictionary<TKey,TValue> dictionary)where TKey:notnull=>new ReadOnlyDictionary<TKey,TValue>(dictionary);
}
