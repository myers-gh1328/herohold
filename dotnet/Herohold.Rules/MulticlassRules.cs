using Herohold.Core;
namespace Herohold.Rules;
public static class MulticlassRules
{
    public static readonly string[] ClassNames=["Barbarian","Bard","Cleric","Druid","Fighter","Monk","Paladin","Ranger","Rogue","Sorcerer","Warlock","Wizard"];
    public static bool MeetsPrerequisite(string name,IReadOnlyDictionary<Ability,int> scores)
    {
        bool At(Ability a)=>scores.TryGetValue(a,out var value)&&value>=13;
        return name switch{"Barbarian"=>At(Ability.Strength),"Bard" or "Sorcerer" or "Warlock"=>At(Ability.Charisma),"Cleric" or "Druid"=>At(Ability.Wisdom),"Fighter"=>At(Ability.Strength)||At(Ability.Dexterity),"Monk" or "Ranger"=>At(Ability.Dexterity)&&At(Ability.Wisdom),"Paladin"=>At(Ability.Strength)&&At(Ability.Charisma),"Rogue"=>At(Ability.Dexterity),"Wizard"=>At(Ability.Intelligence),_=>false};
    }
    public static IReadOnlyList<BuildIssue> ValidateEntry(IReadOnlyList<ClassTrack> tracks,string targetClass,IReadOnlyDictionary<Ability,int> permanentAbilityScores)
        =>tracks.Select(t=>t.ClassName).Append(targetClass).Distinct().Where(c=>!MeetsPrerequisite(c,permanentAbilityScores)).Select(c=>new BuildIssue("multiclass_prerequisite",["class_levels",c],c+" requires13 in its primary ability scores before multiclassing.")).ToArray();
    public static int SpellcastingLevel(IEnumerable<ClassTrack> tracks)=>tracks.Sum(t=>t.ClassName switch{"Bard" or "Cleric" or "Druid" or "Sorcerer" or "Wizard"=>t.Level,"Paladin" or "Ranger"=>(t.Level+1)/2,"Fighter" when t.SubclassName=="Eldritch Knight"=>t.Level/3,"Rogue" when t.SubclassName=="Arcane Trickster"=>t.Level/3,_=>0});
    public static int HitDie(string name)=>name switch{"Barbarian"=>12,"Fighter" or "Ranger" or "Paladin"=>10,"Wizard" or "Sorcerer"=>6,_ when ClassNames.Contains(name)=>8,_=>throw new ArgumentException("Unknown class Hit Die.")};
    public static string[] EntryArmor(string name)=>name switch{"Barbarian"=>["Shields"],"Bard" or "Rogue" or "Warlock"=>["Light Armor"],"Cleric" or "Fighter" or "Paladin" or "Ranger"=>["Light Armor","Medium Armor","Shields"],"Druid"=>["Light Armor","Shields"],_=>[]};
    public static string[] EntryWeapons(string name)=>name is "Barbarian" or "Fighter" or "Paladin" or "Ranger"?["Martial Weapons"]:[];
    public static int EntrySkills(string name)=>name is "Bard" or "Ranger" or "Rogue"?1:0;
}
