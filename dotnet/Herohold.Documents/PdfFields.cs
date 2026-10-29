using Herohold.Core;
using System.Text.Json.Nodes;
using System.Globalization;
using System.Text.RegularExpressions;
namespace Herohold.Documents;

internal static class PdfFields
{
    public static readonly (Ability Ability,string Score,string Modifier,string Save)[] Abilities=[
        (Ability.Strength,"STR","STRmod","ST Strength"),(Ability.Dexterity,"DEX","DEXmod ","ST Dexterity"),
        (Ability.Constitution,"CON","CONmod","ST Constitution"),(Ability.Intelligence,"INT","INTmod","ST Intelligence"),
        (Ability.Wisdom,"WIS","WISmod","ST Wisdom"),(Ability.Charisma,"CHA","CHamod","ST Charisma")];
    public static readonly (string Name,Ability Ability,string Field)[] Skills=[
        ("Acrobatics",Ability.Dexterity,"Acrobatics"),("Animal Handling",Ability.Wisdom,"Animal"),("Arcana",Ability.Intelligence,"Arcana"),
        ("Athletics",Ability.Strength,"Athletics"),("Deception",Ability.Charisma,"Deception "),("History",Ability.Intelligence,"History "),
        ("Insight",Ability.Wisdom,"Insight"),("Intimidation",Ability.Charisma,"Intimidation"),("Investigation",Ability.Intelligence,"Investigation "),
        ("Medicine",Ability.Wisdom,"Medicine"),("Nature",Ability.Intelligence,"Nature"),("Perception",Ability.Wisdom,"Perception "),
        ("Performance",Ability.Charisma,"Performance"),("Persuasion",Ability.Charisma,"Persuasion"),("Religion",Ability.Intelligence,"Religion"),
        ("Sleight of Hand",Ability.Dexterity,"SleightofHand"),("Stealth",Ability.Dexterity,"Stealth "),("Survival",Ability.Wisdom,"Survival")];
    public static readonly (string Name,string Attack,string Damage)[] Attacks=[("Wpn Name","Wpn1 AtkBonus","Wpn1 Damage"),("Wpn Name 2","Wpn2 AtkBonus ","Wpn2 Damage "),("Wpn Name 3","Wpn3 AtkBonus  ","Wpn3 Damage ")];
    public static readonly IReadOnlyDictionary<int,string[]> Spells=CreateSpells();
    private static Dictionary<int,string[]> CreateSpells()
    {
        Dictionary<int,string[]> result=[];
        foreach(var (level,start,end) in new[]{(0,1014,1023),(1,1023,1034),(2,1034,1047),(3,1047,1060),(4,1060,1073),(5,1073,1082),(6,1082,1091),(7,1091,1100)})
            result[level]=Enumerable.Range(start,end-start).Select(i=>$"Spells {i}").ToArray();
        result[8]=Enumerable.Range(0,7).Select(i=>$"Spells 1010{i}").ToArray();result[9]=Enumerable.Range(7,7).Select(i=>$"Spells 1010{i}").ToArray();return result;
    }
    public static string Text(JsonObject fields,string name)=>fields[name] is JsonValue value&&value.TryGetValue<string>(out var text)?PdfCodec.Normalize(text):"";
    private static int? Integer(JsonObject fields,string name)=>int.TryParse(Text(fields,name),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var result)?result:null;
    public static JsonObject Evidence(JsonObject fields)
    {
        var classLevel=Text(fields,"ClassLevel");
        var match=classLevel.IndexOfAny(['/','&',',','+'])<0?Regex.Match(classLevel,@"^(.+?\S)\s+(\d{1,3})$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)):Match.Empty;
        var identity=new JsonObject{["name"]=Text(fields,"CharacterName"),["alternate_name"]=Text(fields,"CharacterName 2"),["class_level"]=classLevel,["class_name"]=match.Success?match.Groups[1].Value:"",["level"]=match.Success?JsonValue.Create(int.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture)):null,["species"]=Text(fields,"Race "),["background"]=Text(fields,"Background")};
        var abilities=new JsonObject();var modifiers=new JsonObject();var saves=new JsonObject();var skills=new JsonObject();
        foreach(var ability in Abilities){abilities[ability.Ability.ToString()]=JsonValue.Create(Integer(fields,ability.Score));modifiers[ability.Ability.ToString()]=Text(fields,ability.Modifier);saves[ability.Ability.ToString()]=Text(fields,ability.Save);}
        foreach(var skill in Skills)skills[skill.Name]=Text(fields,skill.Field);
        var levels=new JsonObject();var totals=new JsonObject();var remaining=new JsonObject();
        foreach(var (level,names) in Spells)levels[level.ToString(CultureInfo.InvariantCulture)]=new JsonArray(names.Select(n=>Text(fields,n)).Where(s=>s.Length>0).Select(s=>(JsonNode?)JsonValue.Create(s)).ToArray());
        for(var level=1;level<=9;level++){totals[level.ToString()]=JsonValue.Create(Integer(fields,$"SlotsTotal {level+18}"));remaining[level.ToString()]=JsonValue.Create(Integer(fields,$"SlotsRemaining {level+18}"));}
        return new(){["identity"]=identity,["abilities"]=abilities,
            ["hit_points"]=new JsonObject{["maximum"]=JsonValue.Create(Integer(fields,"HPMax")),["current"]=JsonValue.Create(Integer(fields,"HPCurrent")),["temporary"]=JsonValue.Create(Integer(fields,"HPTemp")),["hit_die"]=Text(fields,"HD"),["hit_dice_total"]=Text(fields,"HDTotal")},
            ["spells"]=new JsonObject{["by_level"]=levels,["slot_totals"]=totals,["slot_remaining"]=remaining,["spellcasting_class"]=Text(fields,"Spellcasting Class 2"),["spellcasting_ability"]=Text(fields,"SpellcastingAbility 2"),["spell_save_dc"]=JsonValue.Create(Integer(fields,"SpellSaveDC  2")),["spell_attack_bonus"]=Text(fields,"SpellAtkBonus 2")},
            ["equipment"]=new JsonObject{["raw"]=Text(fields,"Equipment")},
            ["presentation"]=new JsonObject{["attacks"]=new JsonArray(Attacks.Select(a=>(JsonNode?)new JsonObject{["name"]=Text(fields,a.Name),["attack_bonus"]=Text(fields,a.Attack),["damage"]=Text(fields,a.Damage)}).ToArray()),["features"]=Text(fields,"Features and Traits"),["additional_features"]=Text(fields,"Feat+Traits"),["proficiencies_and_languages"]=Text(fields,"ProficienciesLang")},
            ["comparison"]=new JsonObject{["armor_class"]=JsonValue.Create(Integer(fields,"AC")),["initiative"]=Text(fields,"Initiative"),["proficiency_bonus"]=Text(fields,"ProfBonus"),["passive_perception"]=JsonValue.Create(Integer(fields,"Passive")),["ability_modifiers"]=modifiers,["saving_throws"]=saves,["skills"]=skills}};
    }
}
