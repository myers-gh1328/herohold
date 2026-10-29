using Herohold.Core;
namespace Herohold.Rules;
internal static class ClassChoiceProficiencySources
{
    internal static IReadOnlyList<ChoiceProficiencySource> Build(BuildFacts build,CatalogClass cls,CatalogBackground background,CatalogSpecies species,NativeBuildSummary summary)
    {
        var result=new List<ChoiceProficiencySource>();
        void Add(string source,ProficiencySourceKind kind,IEnumerable<string> values)
        {
            foreach(var target in values.Where(value=>!string.IsNullOrWhiteSpace(value)))
            {var record=new ChoiceProficiencySource(source,kind,target);if(!result.Contains(record))result.Add(record);}
        }
        var name=summary.ClassName;var level=build.Level;var options=build.ClassOptions;var sp=build.SpeciesOptions;var bg=build.BackgroundOptions;
        Add("class:"+name,ProficiencySourceKind.Save,cls.SavingThrowProficiencies);
        Add("class:"+name,ProficiencySourceKind.Armor,cls.ArmorProficiencies);
        Add("class:"+name,ProficiencySourceKind.Weapon,cls.WeaponProficiencies);
        Add("class:"+name,ProficiencySourceKind.Tool,cls.ToolProficiencies);
        if(name=="Rogue")Add("class_feature:Thieves' Cant",ProficiencySourceKind.Language,["Thieves' Cant"]);
        if(name=="Rogue")Add("class:Rogue",ProficiencySourceKind.Tool,["Thieves' Tools"]);
        if(name=="Druid")Add("class_feature:Druidic",ProficiencySourceKind.Language,["Druidic"]);
        foreach(var pair in new[]{(background.SkillProficiencies,ProficiencySourceKind.Skill),(background.ToolProficiencies,ProficiencySourceKind.Tool),(background.Languages,ProficiencySourceKind.Language)})
            Add("background:"+background.Name,pair.Item2,pair.Item1);
        Add("species:"+species.Name,ProficiencySourceKind.Language,species.Languages);
        Add("species_options:skill_proficiencies",ProficiencySourceKind.Skill,ChoiceReading.Names(sp.Get("skill_proficiencies")));
        Add("background_options:tool_proficiencies",ProficiencySourceKind.Tool,ChoiceReading.Names(bg.Get("tool_proficiencies")));
        Add("class_options:class_skills",ProficiencySourceKind.Skill,ChoiceReading.Names(options.Get("class_skills")));
        Add("class_options:tool_proficiencies",ProficiencySourceKind.Tool,ChoiceReading.Names(options.Get("tool_proficiencies")));
        var shortName=level>=3?cls.SubclassNamed(summary.SubclassName)?.ShortName??"":"";
        void Choice(string key,ProficiencySourceKind kind)=>Add("class_options:"+key,kind,ChoiceReading.Names(options.Get(key)));
        if(name=="Bard"){if(level>=2)Choice("bard_expertise",ProficiencySourceKind.Expertise);if(level>=9)Choice("bard_level_9_expertise",ProficiencySourceKind.Expertise);if(shortName=="Lore")Choice("lore_skills",ProficiencySourceKind.Skill);}
        if(name=="Ranger")
        {
            if(level>=2){Choice("ranger_deft_explorer_expertise",ProficiencySourceKind.Expertise);Choice("ranger_languages",ProficiencySourceKind.Language);}
            if(level>=9)Choice("ranger_level_9_expertise",ProficiencySourceKind.Expertise);
            if(shortName=="Fey Wanderer")Choice("fey_wanderer_skill",ProficiencySourceKind.Skill);
            if(shortName=="Gloom Stalker"&&level>=7)Choice("iron_mind_saving_throw",ProficiencySourceKind.Save);
        }
        if(name=="Rogue"){Choice("expertise",ProficiencySourceKind.Expertise);Choice("rogue_thieves_cant_language",ProficiencySourceKind.Language);if(level>=6)Choice("rogue_level_6_expertise",ProficiencySourceKind.Expertise);if(level>=15)Add("class_feature:Slippery Mind",ProficiencySourceKind.Save,["Wisdom","Charisma"]);}
        if(name=="Wizard"&&level>=2)Choice("wizard_scholar_skill",ProficiencySourceKind.Expertise);
        if(name=="Barbarian"&&level>=3)Choice("primal_knowledge_skill",ProficiencySourceKind.Skill);
        if(name=="Monk")
        {
            if(level>=14)Add("class_feature:Disciplined Survivor",ProficiencySourceKind.Save,Enum.GetNames<Herohold.Core.Ability>());
            if(shortName=="Mercy"){Add("subclass:Mercy",ProficiencySourceKind.Skill,["Insight","Medicine"]);Add("subclass:Mercy",ProficiencySourceKind.Tool,["Herbalism Kit"]);}
        }
        if(shortName=="Battle Master"){Choice("student_of_war_skill",ProficiencySourceKind.Skill);Choice("student_of_war_tool",ProficiencySourceKind.Tool);}
        if(shortName=="Assassin")Add("subclass:Assassin",ProficiencySourceKind.Tool,["Disguise Kit","Poisoner's Kit"]);
        if(shortName=="Valor"){Add("subclass:Valor",ProficiencySourceKind.Armor,["Medium Armor","Shields"]);Add("subclass:Valor",ProficiencySourceKind.Weapon,["Martial Weapons"]);}
        if(name=="Cleric"&&ChoiceReading.TextOf(options.Get("divine_order"))=="Protector"){Add("class_options:divine_order",ProficiencySourceKind.Armor,["Heavy Armor"]);Add("class_options:divine_order",ProficiencySourceKind.Weapon,["Martial Weapons"]);}
        if(name=="Druid"&&ChoiceReading.TextOf(options.Get("primal_order"))=="Warden"){Add("class_options:primal_order",ProficiencySourceKind.Armor,["Medium Armor"]);Add("class_options:primal_order",ProficiencySourceKind.Weapon,["Martial Weapons"]);}
        var originInstances=NativeOriginFeatInstances.Derive(build,background,species);
        foreach(var origin in originInstances.Where(instance=>instance.Name=="Tavern Brawler"))Add(origin.SourceId,ProficiencySourceKind.Weapon,["Improvised Weapons"]);
        var origins=originInstances.Where(instance=>instance.Name is "Skilled" or "Crafter" or "Musician");
        foreach(var origin in origins)
        {
            Add(origin.SourceId,ProficiencySourceKind.Tool,ChoiceReading.Names(origin.Choices.Get("tools")));if(origin.Name=="Skilled")Add(origin.SourceId,ProficiencySourceKind.Skill,ChoiceReading.Names(origin.Choices.Get("skills")));
        }
        Add("manual:skill",ProficiencySourceKind.Skill,summary.ManualSources.Skills.Concat(summary.ManualSources.Expertise));
        Add("manual:expertise",ProficiencySourceKind.Expertise,summary.ManualSources.Expertise);
        Add("manual:save",ProficiencySourceKind.Save,summary.ManualSources.Saves.Select(value=>value.ToString()));
        var knownSkills=result.Where(source=>source.Kind==ProficiencySourceKind.Skill).Select(source=>source.Target).ToHashSet(StringComparer.Ordinal);
        foreach(var feat in build.Feats??[])
        {
            var featName=feat.Name;var choices=feat.Choices;string Picked(string key)=>ChoiceReading.TextOf(choices.Get(key));
            var source="feat:"+(feat.InstanceId.Length>0?feat.InstanceId:feat.FeatId.Length>0?feat.FeatId:featName);
            if(featName=="Skill Expert")
            {var skill=Picked("skill_proficiency");Add(source,ProficiencySourceKind.Skill,[skill]);knownSkills.Add(skill);Add(source,ProficiencySourceKind.Expertise,[Picked("expertise")]);}
            if(featName is "Keen Mind" or "Observant")
            {var skill=Picked("skill");Add(source,knownSkills.Contains(skill)?ProficiencySourceKind.Expertise:ProficiencySourceKind.Skill,[skill]);knownSkills.Add(skill);}
            if(featName=="Resilient")Add(source,ProficiencySourceKind.Save,[Picked("ability")]);
            if(featName=="Boon of Skill"){Add(source,ProficiencySourceKind.Skill,StandingRules.SupportedSkills);knownSkills.UnionWith(StandingRules.SupportedSkills);Add(source,ProficiencySourceKind.Expertise,[Picked("expertise")]);}
            if(featName=="Chef")Add(source,ProficiencySourceKind.Tool,["Cook's Utensils"]);
            if(featName=="Poisoner")Add(source,ProficiencySourceKind.Tool,["Poisoner's Kit"]);
            if(featName=="Heavily Armored")Add(source,ProficiencySourceKind.Armor,["Heavy Armor"]);
            if(featName=="Lightly Armored")Add(source,ProficiencySourceKind.Armor,["Light Armor","Shields"]);
            if(featName=="Moderately Armored")Add(source,ProficiencySourceKind.Armor,["Medium Armor"]);
            if(featName=="Martial Weapon Training")Add(source,ProficiencySourceKind.Weapon,["Martial Weapons"]);
        }
        // Draft-only orphan choices remain in their raw fields but never become active provenance.
        return result.Where(source=>source.Kind switch
        {ProficiencySourceKind.Skill=>summary.Standing.SkillProficiencies.Contains(source.Target),ProficiencySourceKind.Expertise=>summary.Standing.Expertise.Contains(source.Target),ProficiencySourceKind.Save=>summary.Standing.SaveProficiencies.Any(ability=>ability.ToString()==source.Target),_=>true}).ToList().AsReadOnly();
    }
}
