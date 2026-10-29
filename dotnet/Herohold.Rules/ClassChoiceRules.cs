using System.Text.Json;
using Herohold.Core;
namespace Herohold.Rules;

/// <summary>Finite source-schema interpreter for builder class choices. Never executes descriptions or mutates selections.</summary>
public sealed class ClassChoiceRules(ClassProgressionCatalog classes,RulesCatalogSnapshot catalog,BeastChoiceCatalog beasts,SpellCapabilityCatalog spells)
{
    private readonly IReadOnlyList<CatalogSpell> spellRecords=catalog.SpellRecords;
    private static readonly string[] Languages=["Common","Common Sign Language","Draconic","Dwarvish","Elvish","Giant","Gnomish","Goblin","Halfling","Orc","Abyssal","Celestial","Deep Speech","Infernal","Primordial","Sylvan","Undercommon","Druidic","Thieves' Cant"];
    private static readonly string[] Scholar=["Arcana","History","Investigation","Medicine","Nature","Religion"];
    private static readonly string[] Metamagic=["Careful Spell","Distant Spell","Empowered Spell","Extended Spell","Heightened Spell","Quickened Spell","Seeking Spell","Subtle Spell","Transmuted Spell","Twinned Spell"];
    private static readonly string[] FighterStyles=["Archery","Blind Fighting","Defense","Dueling","Great Weapon Fighting","Interception","Protection","Thrown Weapon Fighting","Two-Weapon Fighting","Unarmed Fighting"];
    private static readonly string[] PaladinStyles=["Blessed Warrior","Blind Fighting","Defense","Dueling","Great Weapon Fighting","Interception","Protection","Thrown Weapon Fighting","Two-Weapon Fighting","Unarmed Fighting"];
    private static readonly string[] RangerStyles=["Archery","Blind Fighting","Defense","Druidic Warrior","Dueling","Thrown Weapon Fighting","Two-Weapon Fighting"];
    private static readonly int[] SavantLevels=[3,5,7,9,11,13,15,17];
    private static readonly HashSet<string> ConfigurationKeys=["aspect_of_the_wilds","hunters_prey","defensive_tactics","structured_spell_choice"];
    private static readonly IReadOnlyDictionary<string,string[]> Owned=new Dictionary<string,string[]>{
        ["Barbarian"]=["primal_knowledge_skill","aspect_of_the_wilds"],
        ["Bard"]=["bard_expertise","bard_level_9_expertise","magical_discoveries","lore_skills"],
        ["Cleric"]=["divine_order","extra_cantrips","blessed_strikes"],["Druid"]=["primal_order","extra_cantrips","wild_shape_forms","elemental_fury","structured_spell_choice"],
        ["Fighter"]=["fighting_style","champion_additional_fighting_styles","maneuvers","battle_master_save_ability","student_of_war_skill","student_of_war_tool"],
        ["Monk"]=[],["Paladin"]=["fighting_style","blessed_warrior_cantrips"],
        ["Ranger"]=["ranger_deft_explorer_expertise","ranger_languages","fighting_style","defensive_tactics","iron_mind_saving_throw","fey_wanderer_skill","ranger_level_9_expertise","druidic_warrior_cantrips","primal_companion","hunters_prey"],
        ["Rogue"]=["expertise","rogue_thieves_cant_language","rogue_level_6_expertise"],
        ["Sorcerer"]=["metamagic","elemental_affinity"],
        ["Warlock"]=["eldritch_invocations","pact_tome_cantrips","pact_tome_rituals","mystic_arcanum_6","mystic_arcanum_7","mystic_arcanum_8","mystic_arcanum_9"],
        ["Wizard"]=["wizard_scholar_skill","spell_mastery_level_1","spell_mastery_level_2","signature_spells",..SavantLevels.Select(level=>level==3?"savant_spells":"savant_spells_level_"+level)]};
    public IReadOnlyList<string> OwnedKeys(string className)
    {
        var canonical=classes.Find(className).Name;
        return Owned.GetValueOrDefault(canonical,[]).ToList().AsReadOnly();
    }
    public ClassChoiceReport Describe(ClassChoiceContext context,int targetLevel)
    {
        Validate(context,targetLevel);
        var progression=ClassProgressionRules.AtLevel(classes,context.ClassName,targetLevel,context.SubclassName);
        var name=progression.ClassName;var cls=classes.Find(name);
        var subclass=targetLevel<3?null:cls.SubclassNamed(progression.SubclassName);
        var shortName=subclass?.ShortName??"";
        var definitions=new List<ClassChoiceDefinition>();
        IReadOnlyList<string> Sources(ProficiencySourceKind kind,string? except=null)=>context.ProficiencySources.Where(source=>source.Kind==kind&&(except is null||source.SourceId!="class_options:"+except)).Select(source=>source.Target).Distinct(StringComparer.Ordinal).ToList();
        IReadOnlyList<string> Skills(string key)=>Sources(ProficiencySourceKind.Skill,key);
        IReadOnlyList<string> ExpertiseNew(string key)
        {
            var proficient=Skills(key);var known=Sources(ProficiencySourceKind.Expertise);
            var options=proficient.Except(known,StringComparer.Ordinal).ToList();return options.Count>0?options:proficient;
        }
        IReadOnlyList<string> LanguageUniverse(string key)=>Languages.Except(Sources(ProficiencySourceKind.Language,key).Append("Common"),StringComparer.Ordinal).ToList();
        ClassChoiceDependency SubDependency()=>new(SubclassName:progression.SubclassName);
        void Add(int level,string key,string label,BuildChoiceMode mode,int count,IEnumerable<string> universe,ClassChoiceApplyTarget apply=ClassChoiceApplyTarget.None,IEnumerable<string>? fresh=null,ClassChoiceDependency? dependency=null)
        {
            if(targetLevel<level)return;
            if(definitions.Any(definition=>definition.Key==key))throw new ArgumentException("Two class choice owners claim the same canonical path.");
            var allowed=universe.Where(option=>!string.IsNullOrEmpty(option)).Distinct(StringComparer.Ordinal).ToList();
            var newOptions=(fresh??allowed).Where(option=>allowed.Contains(option,StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToList();
            context.ClassOptions.TryGetValue(key,out var preserved);
            if(preserved is {IsNull:true})preserved=null;
            var effective=ConfigurationKeys.Contains(key)&&context.FeatureConfigurations.TryGetValue(key,out var configured)?ChoiceValue.Of(configured):preserved;
            if(name=="Rogue"&&key=="expertise"&&!context.ClassOptions.ContainsKey(key))effective=ChoiceValue.ListOf(context.LegacyRogueExpertise.Where(skill=>allowed.Contains(skill)).Distinct().Take(2).Select(ChoiceValue.Of));
            var selected=new List<string>();var issues=new List<BuildIssue>();
            void Issue(string code,string message)=>issues.Add(new(code,new[]{"class_options",key},message));
            if(effective?.Text is {} single)
            {if(single.Length>0)selected.Add(single);if(mode==BuildChoiceMode.Multi)Issue("selection_shape","Choose a list of values for this class option.");}
            else if(effective?.Items is {} array)
            {
                foreach(var item in array)
                    if(item.Text is {} itemText){if(itemText.Length>0)selected.Add(itemText);}else Issue("selection_shape","Class choice values must be text.");
                if(mode==BuildChoiceMode.Single)Issue("selection_shape","Choose one value for this class option.");
            }
            else if(effective is not null)Issue("selection_shape","Class choices must be text or a list of text values.");
            if(selected.Distinct(StringComparer.Ordinal).Count()!=selected.Count)Issue("duplicate_selection","Choose distinct values for this class option.");
            if(selected.Count>count)Issue("too_many_selections",$"Choose no more than {count} values.");
            if(selected.Any(option=>!allowed.Contains(option,StringComparer.Ordinal)))Issue("ineligible_selection","A selected value is no longer eligible; choose a replacement.");
            var valid=selected.Where(option=>allowed.Contains(option,StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToList();
            var remaining=Math.Max(0,count-valid.Count);
            if(remaining>0)Issue("missing_selection",label);
            definitions.Add(new(){Key=key,SourceId="class_options:"+key,SourceLevel=level,Name=label,Mode=mode,Choose=count,
                NewOptions=newOptions.Except(selected,StringComparer.Ordinal).ToList().AsReadOnly(),ValidExistingOptions=allowed.AsReadOnly(),
                PreservedSelection=preserved?.Copy(),EffectiveSelection=effective?.Copy(),ValidSelected=valid.AsReadOnly(),Remaining=remaining,Status=issues.Count==0?BuildChoiceStatus.Resolved:BuildChoiceStatus.Unresolved,
                Dependency=dependency,ApplyTo=apply,Issues=issues.AsReadOnly()});
        }
        void Multi(int level,string key,string label,int count,IEnumerable<string> universe,ClassChoiceApplyTarget apply=ClassChoiceApplyTarget.None,IEnumerable<string>? fresh=null,ClassChoiceDependency? dependency=null)=>Add(level,key,label,BuildChoiceMode.Multi,count,universe,apply,fresh,dependency);
        void Single(int level,string key,string label,IEnumerable<string> universe,ClassChoiceApplyTarget apply=ClassChoiceApplyTarget.None,IEnumerable<string>? fresh=null,ClassChoiceDependency? dependency=null)=>Add(level,key,label,BuildChoiceMode.Single,1,universe,apply,fresh,dependency);
        IReadOnlyList<string> Spells(IEnumerable<string> lists,IEnumerable<int> levels,bool sorted=true,string school="",IEnumerable<string>? excluded=null)=>SpellChoiceRules.Filter(spellRecords,new(){Classes=lists.ToList(),Levels=levels.ToList(),SchoolCode=school,SortByName=sorted,ExcludedNames=(excluded??[]).ToList()});
        IReadOnlyList<string> Styles()
        {
            string[] fallback=name=="Fighter"?FighterStyles:name=="Paladin"?["Blessed Warrior",..FighterStyles]:["Druidic Warrior",..FighterStyles];
            return cls.Options.FightingStyle.Where(fallback.Contains).Concat(fallback).Distinct().ToList();
        }
        void Expertise(int level,string key,string label,int count)=>Multi(level,key,label,count,
            name=="Ranger"?Skills(key).Except(Sources(ProficiencySourceKind.Expertise,key),StringComparer.Ordinal):Skills(key),ClassChoiceApplyTarget.Expertise,ExpertiseNew(key));
        if(name=="Fighter")Single(1,"fighting_style","Choose Fighter Fighting Style",Styles());
        if(name=="Cleric")Single(1,"divine_order","Choose Divine Order",cls.Options.DivineOrder);
        if(name=="Druid")Single(1,"primal_order","Choose Primal Order",cls.Options.PrimalOrder);
        if((name=="Cleric"&&ReadValues(context,"divine_order").FirstOrDefault()=="Thaumaturge")||(name=="Druid"&&ReadValues(context,"primal_order").FirstOrDefault()=="Magician"))
        {
            var known=context.CantripSources.Where(source=>source.SourceId!="class_options:extra_cantrips").Select(source=>source.SpellName);
            Multi(1,"extra_cantrips","Choose Order cantrip",1,Spells([name],[0],excluded:known),ClassChoiceApplyTarget.SpellGrant,dependency:new(name=="Cleric"?"divine_order":"primal_order",name=="Cleric"?"Thaumaturge":"Magician"));
        }
        if(name=="Bard")
        {
            Expertise(2,"bard_expertise","Choose 2 Bard Expertise skills",2);
            if(shortName=="Lore")Multi(6,"magical_discoveries","Choose 2 Magical Discoveries spells",2,Spells(["Cleric","Druid","Wizard"],[0,..(progression.Spellcasting?.AvailableSpellLevels??[])]),ClassChoiceApplyTarget.SpellGrant,dependency:SubDependency());
            Expertise(9,"bard_level_9_expertise","Choose 2 additional Bard Expertise skills",2);
        }
        if(name=="Ranger")
        {
            Expertise(2,"ranger_deft_explorer_expertise","Choose Deft Explorer Expertise skill",1);
            Multi(2,"ranger_languages","Choose 2 Deft Explorer languages",2,LanguageUniverse("ranger_languages"),ClassChoiceApplyTarget.Language);
            Single(2,"fighting_style","Choose Ranger Fighting Style",Styles());
            if(shortName=="Hunter")Single(7,"defensive_tactics","Choose Defensive Tactics",["Escape the Horde","Multiattack Defense"],dependency:SubDependency());
            if(shortName=="Gloom Stalker")
            {
                var known=Sources(ProficiencySourceKind.Save,"iron_mind_saving_throw");var options=!known.Contains("Wisdom")?new[]{"Wisdom"}:new[]{"Intelligence","Charisma"}.Except(known);
                Single(7,"iron_mind_saving_throw","Choose Iron Mind saving throw",options,ClassChoiceApplyTarget.Save,dependency:SubDependency());
            }
            if(shortName=="Fey Wanderer")Single(3,"fey_wanderer_skill","Choose Otherworldly Glamour skill",["Deception","Performance","Persuasion"],ClassChoiceApplyTarget.Skill,new[]{"Deception","Performance","Persuasion"}.Except(Skills("fey_wanderer_skill")),SubDependency());
            Expertise(9,"ranger_level_9_expertise","Choose 2 additional Ranger Expertise skills",2);
        }
        if(name=="Sorcerer")
        {
            var source=targetLevel>=17?17:targetLevel>=10?10:2;var count=targetLevel>=17?6:targetLevel>=10?4:2;
            Multi(source,"metamagic",$"Choose {count} total Metamagic options",count,Metamagic);
            if(shortName=="Draconic")Single(6,"elemental_affinity","Choose Elemental Affinity damage type",["Acid","Cold","Fire","Lightning","Poison"],dependency:SubDependency());
        }
        if(name=="Wizard")
        {
            var proficient=Scholar.Intersect(Skills("wizard_scholar_skill"),StringComparer.Ordinal).ToList();
            Single(2,"wizard_scholar_skill","Choose Scholar Expertise skill",proficient,ClassChoiceApplyTarget.Expertise,proficient.Except(Sources(ProficiencySourceKind.Expertise),StringComparer.Ordinal));
            var book=context.Spellbook.Concat(context.SavantSpells.Select(source=>source.SpellName)).ToHashSet(StringComparer.Ordinal);
            foreach(var spellLevel in new[]{1,2})
            {
                var eligible=SpellChoiceRules.Filter(spellRecords,new(){Classes=["Wizard"],Levels=[spellLevel],CastingTime="1 action"}).Where(book.Contains);
                Single(18,"spell_mastery_level_"+spellLevel,$"Choose Spell Mastery level {spellLevel} spell",eligible,ClassChoiceApplyTarget.SpellGrant);
            }
            Multi(20,"signature_spells","Choose 2 Signature Spells",2,Spells(["Wizard"],[3],false).Where(book.Contains),ClassChoiceApplyTarget.SpellGrant);
        }
        if(name=="Paladin")Single(2,"fighting_style","Choose Paladin Fighting Style",Styles());
        if(name=="Warlock")
        {
            var count=progression.Resources.Count("eldritch_invocations");
            if(count>0)Multi(targetLevel,"eldritch_invocations",$"Choose {count} Eldritch Invocations",count,InvocationOptions(context,cls,targetLevel));
            if(ReadValues(context,"eldritch_invocations").Contains("Pact of the Tome",StringComparer.Ordinal))
            {
                var cantrips=context.CantripSources.Where(source=>source.SourceId!="class_options:pact_tome_cantrips").Select(source=>source.SpellName).ToHashSet(StringComparer.Ordinal);
                var prepared=context.PreparedSpellSources.Where(source=>source.SourceId!="class_options:pact_tome_rituals").Select(source=>source.SpellName).ToHashSet(StringComparer.Ordinal);
                var dependency=new ClassChoiceDependency("eldritch_invocations","Pact of the Tome");
                Multi(1,"pact_tome_cantrips","Choose 3 Book of Shadows cantrips",3,spellRecords.Where(spell=>spell.Level==0&&!cantrips.Contains(spell.Name)).Select(spell=>spell.Name).Order(StringComparer.Ordinal),ClassChoiceApplyTarget.SpellGrant,dependency:dependency);
                Multi(1,"pact_tome_rituals","Choose 2 Book of Shadows level 1 rituals",2,spellRecords.Where(spell=>spell.Level==1&&spell.Ritual&&!prepared.Contains(spell.Name)).Select(spell=>spell.Name).Order(StringComparer.Ordinal),ClassChoiceApplyTarget.SpellGrant,dependency:dependency);
            }
            foreach(var pair in new[]{(11,6),(13,7),(15,8),(17,9)})Multi(pair.Item1,"mystic_arcanum_"+pair.Item2,$"Choose Mystic Arcanum level {pair.Item2} spell",1,Spells(["Warlock"],[pair.Item2]),ClassChoiceApplyTarget.SpellGrant);
        }
        if(name=="Cleric")Single(7,"blessed_strikes","Choose Blessed Strikes option",["Divine Strike","Potent Spellcasting"]);
        if(name=="Barbarian")
        {
            var allowed=cls.SkillChoices.From;
            Multi(3,"primal_knowledge_skill","Choose Primal Knowledge skill",1,allowed,ClassChoiceApplyTarget.Skill,allowed.Except(Skills("primal_knowledge_skill")));
            if(shortName=="Wild Heart")Single(6,"aspect_of_the_wilds","Choose Aspect of the Wilds",["Owl","Panther","Salmon"],dependency:SubDependency());
        }
        if(name=="Druid")
        {
            var source=targetLevel>=8?8:targetLevel>=4?4:2;var count=targetLevel>=8?8:targetLevel>=4?6:4;
            Multi(source,"wild_shape_forms",$"Choose {count} Wild Shape forms",count,beasts.WildShapeOptions(targetLevel,shortName=="Moon"));
            Single(7,"elemental_fury","Choose Elemental Fury option",["Potent Spellcasting","Primal Strike"]);
        }
        if(name=="Fighter"&&shortName=="Champion")Multi(7,"champion_additional_fighting_styles","Choose Additional Fighting Style",1,FighterStyles.Except(ReadValues(context,"fighting_style")),dependency:SubDependency());
        var style=ReadValues(context,"fighting_style").FirstOrDefault();
        if(name=="Paladin"&&style=="Blessed Warrior")Multi(2,"blessed_warrior_cantrips","Choose 2 Blessed Warrior cantrips",2,Spells(["Cleric"],[0]),ClassChoiceApplyTarget.SpellGrant,dependency:new("fighting_style","Blessed Warrior"));
        if(name=="Ranger"&&style=="Druidic Warrior")Multi(2,"druidic_warrior_cantrips","Choose 2 Druidic Warrior cantrips",2,Spells(["Druid"],[0]),ClassChoiceApplyTarget.SpellGrant,dependency:new("fighting_style","Druidic Warrior"));
        if(name=="Rogue")
        {
            Expertise(1,"expertise","Choose 2 Rogue Expertise skills",2);
            Single(1,"rogue_thieves_cant_language","Choose Thieves' Cant language",LanguageUniverse("rogue_thieves_cant_language"),ClassChoiceApplyTarget.Language);
            Expertise(6,"rogue_level_6_expertise","Choose 2 additional Rogue Expertise skills",2);
        }
        if(subclass is not null)AddSubclassChoices(context,targetLevel,name,progression.SubclassName,subclass.Options,cls,definitions,Multi,Single,Skills,Spells);
        return Report(definitions);
    }
    public ClassChoiceReport ForLevelUp(ClassChoiceContext context,int currentLevel,int targetLevel)
    {
        if(currentLevel is <1 or >19||targetLevel!=currentLevel+1)throw new ArgumentException("Advance class choices one level at a time.");
        var report=Describe(context,targetLevel);
        return Report(report.Definitions.Where(definition=>definition.SourceLevel>currentLevel&&definition.Status!=BuildChoiceStatus.Resolved).ToList());
    }
    private static ClassChoiceReport Report(List<ClassChoiceDefinition> definitions)=>new(){Definitions=definitions.AsReadOnly(),Unresolved=definitions.Where(definition=>definition.Status!=BuildChoiceStatus.Resolved).ToList().AsReadOnly(),Issues=definitions.SelectMany(definition=>definition.Issues).ToList().AsReadOnly()};
    private static IReadOnlyList<string> ReadValues(ClassChoiceContext context,string key)
    {
        if(!context.ClassOptions.TryGetValue(key,out var raw))return [];
        if(raw.Text is {} text)return text.Length==0?[]:[text];
        return raw.Items is {} array?array.Select(item=>item.Text??"").Where(item=>item.Length>0).ToList():[];
    }
    private IReadOnlyList<string> InvocationOptions(ClassChoiceContext context,CatalogClass cls,int level)
    {
        var rule=cls.Options.EldritchInvocations??new();
        var records=rule.Options.Count>0?rule.Options:rule.From.Select(value=>new CatalogOptionRecord{Name=value}).ToList();var selected=ReadValues(context,"eldritch_invocations").ToHashSet(StringComparer.Ordinal);
        var damaging=context.CantripSources.Any(source=>source.CountsAsClasses.Contains("Warlock",StringComparer.Ordinal)&&spells.IsCantrip(source.SpellName)&&spells.CanDealDamage(source.SpellName));
        var result=new List<string>();
        foreach(var record in records)
        {
            var name=record.Name;
            var minimum=record.MinimumWarlockLevel??1;
            IReadOnlyList<string> required=record.RequiredInvocations;var needsDamage=record.RequiresDamageCantrip;
            var reviewedMinimum=name switch
            {"Agonizing Blast" or "Eldritch Spear" or "Repelling Blast"=>2,"Ascendant Step" or "Eldritch Smite" or "Master of Myriad Forms" or "One with Shadows" or "Thirsting Blade"=>5,"Whispers of the Grave"=>7,"Lifedrinker" or "Visions of Distant Realms"=>9,"Devouring Blade"=>12,"Witch Sight"=>15,_=>0};
            if(reviewedMinimum>0)minimum=reviewedMinimum;
            if(name is "Agonizing Blast" or "Eldritch Spear" or "Repelling Blast")needsDamage=true;
            required=name switch
            {"Devouring Blade"=>["Pact of the Blade","Thirsting Blade"],"Eldritch Smite" or "Lifedrinker" or "Thirsting Blade"=>["Pact of the Blade"],"Gift of the Protectors"=>["Pact of the Tome"],"Investment of the Chain Master"=>["Pact of the Chain"],_=>required};
            if(level<minimum||required.Any(value=>!selected.Contains(value))||needsDamage&&!damaging)continue;
            if(name.Length>0)result.Add(name);
        }
        return result.AsReadOnly();
    }
    private static void Validate(ClassChoiceContext context,int level)
    {
        ArgumentNullException.ThrowIfNull(context);
        if(level is <1 or >20||context.Level is <1 or >20||string.IsNullOrWhiteSpace(context.ClassName)||context.ClassName.Length>200||context.SubclassName is null||context.SubclassName.Length>200
            ||context.ClassOptions is null||context.ClassOptions.Count>100||context.FeatureConfigurations is null||context.FeatureConfigurations.Count>100
            ||context.ProficiencySources is null||context.ProficiencySources.Count>1000||context.CantripSources is null||context.CantripSources.Count>500
            ||context.PreparedSpellSources is null||context.PreparedSpellSources.Count>5000||context.Spellbook is null||context.Spellbook.Count>5000||context.SavantSpells is null||context.SavantSpells.Count>1000||context.LegacyRogueExpertise is null||context.LegacyRogueExpertise.Count>18)
            throw new ArgumentException("Class choice context exceeds supported bounds.");
        foreach(var source in context.ProficiencySources)
            if(source is null||string.IsNullOrWhiteSpace(source.SourceId)||source.SourceId.Length>200||!Enum.IsDefined(source.Kind)||string.IsNullOrWhiteSpace(source.Target)||source.Target.Length>200)throw new ArgumentException("A proficiency source is invalid.");
        foreach(var pair in context.ClassOptions)
        {
            if(string.IsNullOrWhiteSpace(pair.Key)||pair.Key.Length>200)throw new ArgumentException("A class option key is invalid.");
            if(JsonSerializer.Serialize(pair.Value).Length>32768)throw new ArgumentException("A class selection is too large.");
        }
        foreach(var source in context.CantripSources)
            if(source is null||string.IsNullOrWhiteSpace(source.SpellName)||source.SpellName.Length>200||source.SourceId is null||source.SourceId.Length>200||source.CountsAsClasses is null||source.CountsAsClasses.Count>12)throw new ArgumentException("A cantrip source is invalid.");
        foreach(var source in context.PreparedSpellSources)
            if(source is null||string.IsNullOrWhiteSpace(source.SpellName)||source.SpellName.Length>200||string.IsNullOrWhiteSpace(source.SourceId)||source.SourceId.Length>200)throw new ArgumentException("A prepared spell source is invalid.");
    }
    private static void AddSubclassChoices(ClassChoiceContext context,int level,string className,string subclassName,SubclassOptionCatalog options,CatalogClass cls,List<ClassChoiceDefinition> definitions,
        Action<int,string,string,int,IEnumerable<string>,ClassChoiceApplyTarget,IEnumerable<string>?,ClassChoiceDependency?> multi,
        Action<int,string,string,IEnumerable<string>,ClassChoiceApplyTarget,IEnumerable<string>?,ClassChoiceDependency?> single,
        Func<string,IReadOnlyList<string>> skills,
        Func<IEnumerable<string>,IEnumerable<int>,bool,string,IEnumerable<string>?,IReadOnlyList<string>> spellOptions)
    {
        var dependency=new ClassChoiceDependency(SubclassName:subclassName);
        void One(string key,string label,IEnumerable<string> values,ClassChoiceApplyTarget apply=ClassChoiceApplyTarget.None)=>single(3,key,label,values,apply,null,dependency);
        if(options.LoreSkills is {} lore)multi(3,"lore_skills","Choose College of Lore skills",lore.Choose,lore.From,ClassChoiceApplyTarget.Skill,null,dependency);
        if(options.Maneuvers is {} maneuvers)
        {
            var count=level>=15?9:level>=10?7:level>=7?5:3;var source=level>=15?15:level>=10?10:level>=7?7:3;
            multi(source,"maneuvers",$"Choose {count} total Battle Master maneuvers",count,maneuvers.From,ClassChoiceApplyTarget.None,null,dependency);
        }
        if(options.ManeuverSaveAbility is {} saveAbilities)One("battle_master_save_ability","Maneuver save ability",saveAbilities);
        if(options.StudentOfWarSkill is not null)One("student_of_war_skill","Student of War skill",cls.SkillChoices.From,ClassChoiceApplyTarget.Skill);
        if(options.StudentOfWarTool is {} tool)One("student_of_war_tool","Student of War artisan tool",tool.From,ClassChoiceApplyTarget.Tool);
        if(options.HuntersPrey is {} prey)One("hunters_prey","Hunter's Prey",prey.From);
        if(options.PrimalCompanion is {} companion)One("primal_companion","Primal Companion",companion.From);
        if(options.StructuredSpellChoice is {} group)One("structured_spell_choice",group.Name.Length>0?group.Name:"Subclass spell group",group.Options.Select(record=>record.Name));
        if(options.SavantSpells is {} savant)
        {
            var byLevel=options.SavantSpellChoicesByLevel;
            var levels=byLevel.Count==0?new[]{3}:SavantLevels;
            foreach(var source in levels.Where(source=>source<=level))
            {
                var rule=byLevel.GetValueOrDefault(source);if(byLevel.Count>0&&rule is null)continue;
                var school=(rule??savant).School;
                var maximum=rule?.MaximumSpellLevel??(source==3?2:(source+1)/2);
                var key=source==3?"savant_spells":"savant_spells_level_"+source;
                var owned=context.SavantSpells.Where(spell=>spell.SourceId=="class_options:"+key).Select(spell=>spell.SpellName).ToHashSet(StringComparer.Ordinal);
                var blocked=context.Spellbook.Concat(context.SavantSpells.Where(spell=>spell.SourceId!="class_options:"+key).Select(spell=>spell.SpellName)).Except(owned,StringComparer.Ordinal);
                var eligible=spellOptions(["Wizard"],Enumerable.Range(1,maximum),false,school,blocked);
                multi(source,key,rule is {Label.Length:>0}?rule.Label:"Choose "+subclassName+" Savant spells",rule?.Choose??savant.Choose??2,eligible,ClassChoiceApplyTarget.Spellbook,null,dependency);
            }
        }
    }
}
