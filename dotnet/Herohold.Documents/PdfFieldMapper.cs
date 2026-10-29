using Herohold.Core;
using System.Text.Json.Nodes;
using System.Globalization;
namespace Herohold.Documents;

public static class PdfFieldMapper
{
    /// <summary>The sheet fields Herohold owns, written from the character.</summary>
    public static JsonObject Create(CharacterState character)
    {
        WorkspaceRules.Validate(character);
        var fields=new JsonObject{
            ["CharacterName"]=character.Name,["CharacterName 2"]=character.Name,["ClassLevel"]=ClassLevelText(character),
            ["Background"]=SheetRules.Profile(character).Background,["Race "]=character.Species,["XP"]=Number(SheetRules.ExperiencePoints(character)),
            ["ProfBonus"]=Signed(CharacterCalculations.ProficiencyBonus(character.Level)),["AC"]=Number(character.ArmorClass),
            ["Initiative"]=Signed(CharacterCalculations.Initiative(character).Total),["Speed"]=$"{CharacterCalculations.Speed(character)} ft",
            ["HPMax"]=Number(character.MaximumHp),["HPCurrent"]=Number(character.CurrentHp),["HPTemp"]=Number(character.TemporaryHp),
            ["HDTotal"]=HitDiceText(character,false),["HD"]=HitDiceText(character,true),
            ["Passive"]=Number(10+CharacterCalculations.Skill(character,"Perception",Ability.Wisdom).Total),
            ["Equipment"]=Wrap(string.Join(", ",character.Equipment.Where(i=>i.Quantity>0).Select(i=>i.Quantity==1?i.Name:$"{i.Name} ({i.Quantity})")),31),
            ["Features and Traits"]=Wrap(Features(character),41),["Feat+Traits"]=Wrap(Features(character),65),
            ["AttacksSpellcasting"]="",["Spellcasting Class 2"]=character.ClassName};
        foreach(var ability in PdfFields.Abilities)
        {
            fields[ability.Score]=Number(character.AbilityScores[ability.Ability]);fields[ability.Modifier]=Signed(CharacterCalculations.Ability(character,ability.Ability).Total);fields[ability.Save]=Signed(CharacterCalculations.Save(character,ability.Ability).Total);
        }
        foreach(var skill in PdfFields.Skills)fields[skill.Field]=Signed(CharacterCalculations.Skill(character,skill.Name,skill.Ability).Total);
        for(var level=1;level<=9;level++)
        {
            var pools=character.Resources.Where(p=>p.SlotLevel==level||p.SlotLevel is null&&p.Id=="slot"+level).ToList();
            fields[$"SlotsTotal {18+level}"]=pools.Count>0?Number(pools.Sum(p=>p.Maximum)):"";
            fields[$"SlotsRemaining {18+level}"]=pools.Count>0?Number(pools.Sum(p=>p.Remaining)):"";
        }
        var allSpells=OwnedSpells(character);
        foreach(var (level,names) in PdfFields.Spells)
        {
            var spells=allSpells.Where(s=>s.Level==level).ToArray();for(var i=0;i<names.Length;i++)if(i<spells.Length)fields[names[i]]=spells[i].Name;
        }
        var casting=allSpells.Count>0;var modifier=CharacterCalculations.Ability(character,character.Play.SpellcastingAbility).Total;
        fields["SpellcastingAbility 2"]=casting?character.Play.SpellcastingAbility.ToString():"";
        fields["SpellSaveDC  2"]=casting?Number(8+CharacterCalculations.ProficiencyBonus(character.Level)+modifier):"";
        fields["SpellAtkBonus 2"]=casting?Signed(CharacterCalculations.ProficiencyBonus(character.Level)+modifier):"";
        PopulateSheetFields(fields,character);
        PopulateAttacks(fields,character);
        return fields;
    }
    private static void PopulateSheetFields(JsonObject fields,CharacterState character)
    {
        var p=SheetRules.Profile(character);var purse=SheetRules.Purse(character);
        if(SheetRules.HasUnresolvedExperience(character))fields.Remove("XP");
        if(!SheetRules.HasUnresolvedCoins(character))foreach(var (key,value) in new[]{("CP",purse.Cp),("SP",purse.Sp),("EP",purse.Ep),("GP",purse.Gp),("PP",purse.Pp)})fields[key]=Number(value);
        foreach(var (key,value) in new[]{("PlayerName",p.PlayerName),("Age",p.Age),("Height",p.Height),("Weight",p.Weight),("Eyes",p.Eyes),("Skin",p.Skin),("Hair",p.Hair),("Alignment",p.Alignment),("Backstory",p.Backstory),("PersonalityTraits ",p.Personality),("Ideals",p.Ideals),("Bonds",p.Bonds),("Flaws",p.Flaws),("Allies",p.Allies),("FactionName",p.Organizations),("Treasure",p.Treasure)})fields[key]=value??"";
        fields["Inspiration"]=SheetRules.HasHeroicInspiration(character)?"Yes":"";
        for(var n=0;n<3;n++){fields["Check Box "+(12+n)]=n<character.Sheet.DeathSaves.Successes;fields["Check Box "+(15+n)]=n<character.Sheet.DeathSaves.Failures;}
        var saveBoxes=new[]{11,18,19,20,21,22};for(var n=0;n<PdfFields.Abilities.Length;n++)fields["Check Box "+saveBoxes[n]]=character.SaveProficiencies.Contains(PdfFields.Abilities[n].Ability);
        for(var n=0;n<PdfFields.Skills.Length;n++)fields["Check Box "+(23+n)]=character.SkillProficiencies.Contains(PdfFields.Skills[n].Name,StringComparer.OrdinalIgnoreCase);
        fields["ProficienciesLang"]=Wrap(string.Join("\n",new[]{("Armor",p.ArmorTraining),("Weapons",p.WeaponProficiencies),("Tools",p.ToolProficiencies),("Languages",p.Languages)}.Where(v=>v.Item2?.Count>0).Select(v=>v.Item1+": "+string.Join(", ",v.Item2!))),38);
    }
    private static string ClassLevelText(CharacterState c)
    {
        if(c.Build?.ClassLevels is {Count:>1} levels&&levels.All(p=>p.Value>0)&&levels.Sum(p=>p.Value)==c.Level)
            return string.Join(" / ",levels.Select(p=>p.Key+" "+p.Value));
        return $"{c.ClassName} {c.Level}";
    }
    private static string HitDiceText(CharacterState c,bool remaining)=>c.Play.HitDiePools.Count>0
        ?string.Join(" + ",c.Play.HitDiePools.GroupBy(p=>p.Sides).OrderBy(g=>g.Key).Select(g=>$"{g.Sum(p=>remaining?p.Remaining:p.Maximum)}d{g.Key}"))
        :$"{(remaining?c.Resources.FirstOrDefault(r=>r.Id is "hitdice" or "hit-dice")?.Remaining??c.Level:c.Level)}d{c.Play.HitDieSize}";
    internal static List<SpellDefinition> OwnedSpells(CharacterState character)=>character.Play.PreparedSpellIds.Concat(character.Play.SpellbookIds).Concat(character.Play.KnownSpellIds).Concat(character.Play.GrantedSpellIds)
        .Distinct(StringComparer.Ordinal).Select(id=>character.Play.Spells.FirstOrDefault(s=>s.Id==id)).OfType<SpellDefinition>().DistinctBy(s=>(s.Level,s.Name)).ToList();
    private static void PopulateAttacks(JsonObject fields,CharacterState character)
    {
        foreach(var row in PdfFields.Attacks){fields[row.Name]="";fields[row.Attack]="";fields[row.Damage]="";}
        if(character.Derived.Attacks.FirstOrDefault(a=>a.Kind=="weapon") is {} weapon)
        {
            fields["Wpn Name"]=weapon.Name;fields["Wpn1 AtkBonus"]=Signed(CharacterCalculations.Attack(character,weapon.Ability,weapon.Proficient,weapon.FlatBonus).Total);
            var damage=CharacterCalculations.Ability(character,weapon.Ability).Total+weapon.DamageModifierAdjustment;
            fields["Wpn1 Damage"]=weapon.DamageDice is not {} dice?"":$"{dice.Count}d{dice.Sides}{(damage==0?"":Signed(damage))} {weapon.DamageType}";
        }
    }
    private static string Features(CharacterState character)
    {
        var lines=new List<string>();var feats=character.Build?.Feats??[];
        var nativeDetails=character.Derived.FeatureDetails;
        var names=character.Features.Concat(feats.Select(f=>f.Name)).Concat(character.Derived.OriginFeats).Where(name=>!name.StartsWith("Origin Feat:",StringComparison.Ordinal)).Distinct(StringComparer.Ordinal);
        foreach(var name in names)
        {
            var detail=nativeDetails.FirstOrDefault(d=>d.Name==name);
            var description=detail?.Description??character.Derived.FeatureDescriptions.GetValueOrDefault(name,"");
            var instances=feats.Where(f=>f.Name==name).ToList();
            var labels=instances.Count>0?instances.Select(FeatLabel):new[]{name};
            foreach(var label in labels)lines.Add(description.Length>0?label+": "+description:label);
            foreach(var reference in detail?.RelatedFeatures??[])lines.Add("  "+reference.Name+(reference.Cost.Length>0?" - Cost: "+reference.Cost:"")+(reference.Description.Length>0?": "+reference.Description:""));
        }
        var masteries=string.Join(", ",(character.Build?.WeaponMasteries??[]).Where(s=>s.Length>0).Distinct(StringComparer.Ordinal));if(masteries.Length>0)lines.Add("Weapon mastery: "+masteries);
        if(character.Resources.Count>0)lines.Add("Resources: "+string.Join("; ",character.Resources.Select(r=>$"{r.Name}: {r.Remaining}/{r.Maximum}")));
        return string.Join("\n",lines);
    }
    /// <summary>A feat with the choices made for it, such as "Skill Expert (Wisdom; Expertise: Stealth)".</summary>
    private static string FeatLabel(FeatFact feat)
    {
        var choices=feat.Choices;var parts=new List<string>();if(choices.GetValueOrDefault("ability") is {IsNull:false} ability)parts.Add(Text(ability));
        foreach(var (key,value) in choices.Where(p=>p.Key!="ability")){var text=Join(value);if(text.Length>0)parts.Add(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('_',' '))+": "+text);}
        return feat.Name+(parts.Count>0?" ("+string.Join("; ",parts)+")":"");
    }
    private static string Join(ChoiceValue value)=>value.Items is {} items?string.Join(", ",items.Select(Text).Where(s=>s.Length>0).Distinct(StringComparer.Ordinal)):Text(value);
    /// <summary>A choice as text: a name, a number, a flag, or the name of a named record.</summary>
    private static string Text(ChoiceValue value)=>value.Text??value.Number?.ToString(CultureInfo.InvariantCulture)??(value.Flag is {} flag?(flag?"true":"false"):value.Fields is {} fields?(fields.GetValueOrDefault("name")??fields.GetValueOrDefault("id")) is {} named?Text(named):"":"");
    private static string Number(int value)=>value.ToString(CultureInfo.InvariantCulture);
    private static string Signed(int value)=>value>=0?"+"+Number(value):Number(value);
    private static string Wrap(string text,int width)
    {
        var lines=new List<string>();foreach(var paragraph in text.Split('\n')){var line="";foreach(var word in paragraph.Split(' ',StringSplitOptions.RemoveEmptyEntries)){if(line.Length>0&&line.Length+1+word.Length>width){lines.Add(line);line="";}line+=(line.Length>0?" ":"")+word;}lines.Add(line);}return string.Join("\n",lines);
    }
}
