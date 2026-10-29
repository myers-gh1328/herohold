using Herohold.Core;
namespace Herohold.Rules;

public sealed class CreationChoiceRules(RulesCatalogSnapshot catalog,ClassProgressionCatalog classes,ClassChoiceRules classChoices,RulesReference rules,SpeciesSourceCatalog? rawSpecies=null,ToolCatalog? toolCatalog=null)
{
    private readonly SpeciesSourceCatalog speciesData=rawSpecies??SpeciesSourceCatalog.Empty;
    private static readonly string[] Abilities=["Strength","Dexterity","Constitution","Intelligence","Wisdom","Charisma"];
    /// <summary>An option from a catalog record or name.</summary>
    private static BuildChoiceOption Option(string name)=>new(){Value=ChoiceValue.Of(name),Label=name};
    private static BuildChoiceOption Option(CatalogSpell spell)=>new(){Value=ChoiceValue.Of(spell.Name),Label=spell.Name,Description=spell.Description,Source=spell.Source};
    private static BuildChoiceOption Option(CatalogSubclass subclass)=>new(){Value=ChoiceValue.Of(subclass.Name.Length>0?subclass.Name:subclass.Id),Label=subclass.Name,Source=subclass.Source};
    private static IEnumerable<BuildChoiceOption> Named(IEnumerable<string> names)=>names.Select(name=>new BuildChoiceOption{Value=ChoiceValue.Of(name),Label=name});
    public CreationChoiceReport Describe(CreationChoiceContext context)
    {
        var build=context.Build;var tree=build.AsChoiceTree();var output=new List<BuilderChoiceDefinition>();var issues=new List<BuildIssue>();
        var level=build.Level;if(level is <1 or >20)throw new ArgumentException("Character levels must be between 1 and 20.");
        ChoiceValue? At(IReadOnlyList<string> path)=>tree.At(path) is {IsNull:false} found?found:null;
        string TextAt(params string[] path)=>At(path)?.Text??"";
        var cls=catalog.Class(build.ClassName);var species=catalog.SpeciesNamed(build.Species);var background=catalog.Background(build.Background);
        BuilderChoiceDefinition Add(string id,string label,BuildChoiceMode mode,string[] path,IEnumerable<BuildChoiceOption>? options=null,int choose=1,string section="class",bool required=true,ChoiceDetails? details=null)
        {
            if(output.Any(d=>d.Path.SequenceEqual(path)))throw new ArgumentException("Two builder choices claim the same canonical path.");
            var values=(options??[]).ToArray();var selected=At(path);var allowed=values.Where(v=>!v.Disabled).Select(v=>v.Value.Text??"").ToHashSet(StringComparer.Ordinal);
            bool valid=mode switch
            {
                BuildChoiceMode.Text=>(selected?.Text??"").Length is >0 and <=200,
                BuildChoiceMode.AbilityScores=>selected?.Fields is {Count:6} scores&&Abilities.All(a=>scores.GetValueOrDefault(a)?.Number is >=1 and <=30),
                BuildChoiceMode.Multi=>selected?.Items is {} items&&items.Count>=(details?.Minimum??choose)&&items.Count<=(details?.Maximum??choose)&&items.All(v=>v.Text is {} text&&allowed.Contains(text))&&items.Select(v=>v.Text).Distinct(StringComparer.Ordinal).Count()==items.Count,
                _=>(selected?.Text??"").Length>0&&allowed.Contains(selected!.Text!)
            };
            if(!required&&(selected is null||selected.Items is {Count:0}))valid=true;
            var ownIssues=valid?Array.Empty<BuildIssue>():new[]{new BuildIssue("invalid_choice",path,"Complete "+label+" with eligible choices.")};
            var definition=new BuilderChoiceDefinition{Id=id,Label=label,Mode=mode,Path=path,Choose=choose,Section=section,Required=required,Options=values,PreservedSelection=selected,Status=valid?BuildChoiceStatus.Resolved:BuildChoiceStatus.Unresolved,Details=details??new(),Issues=ownIssues};
            output.Add(definition);issues.AddRange(ownIssues);return definition;
        }
        void Single(string id,string label,string[] path,IEnumerable<string> options,string section="class",bool required=true)=>Add(id,label,BuildChoiceMode.Single,path,Named(options),section:section,required:required);
        void Multi(string id,string label,string[] path,IEnumerable<string> options,int count,string section="class",bool required=true)=>Add(id,label,BuildChoiceMode.Multi,path,Named(options),count,section,required);
        Add("name","Character name",BuildChoiceMode.Text,["name"],section:"identity");
        foreach(var item in new[]{("class","Class",catalog.ClassEntries),("species","Species",catalog.SpeciesEntries),("background","Background",catalog.BackgroundEntries)})Add(item.Item1,item.Item2,BuildChoiceMode.Single,[item.Item1],item.Item3.Select(e=>new BuildChoiceOption{Value=ChoiceValue.Of(e.Name),Label=e.Name,Description=e.Description,Source=e.Source}),section:"identity");
        Multi("origin_languages","Choose two standard languages",["origin_languages"],LanguageRules.StandardLanguages,2,"origin");
        Add("base_ability_scores","Ability scores",BuildChoiceMode.AbilityScores,["base_ability_scores"],section:"abilities",details:new(){Minimum=1,Maximum=30,Abilities=Abilities});
        issues.AddRange(AbilityGenerationRules.Validate(build));
        var abilityOptions=(background?.AbilityOptions.Abilities??[]).ToArray();
        if(abilityOptions.Length>0)
        {
            var mode=TextAt("background_options","ability_bonus_mode");
            var definition=Add("background_bonus_mode","Background ability increases",BuildChoiceMode.Single,["background_options","ability_bonus_mode"],[new(){Value=ChoiceValue.Of("weighted"),Label="+2 to one ability, +1 to another"},new(){Value=ChoiceValue.Of("three"),Label="+1 to each of the three abilities"}],section:"background");
            if(mode.Length==0){output[output.Count-1]=definition with{Details=definition.Details with{EffectiveSelection=ChoiceValue.Of("weighted")},Status=BuildChoiceStatus.Resolved,Issues=[]};issues.RemoveAll(i=>i.Path.SequenceEqual(definition.Path));}
            if(mode!="three")
            {
                Single("primary_ability","Background +2 ability",["background_options","primary_ability"],abilityOptions,"background");
                Single("secondary_ability","Background +1 ability",["background_options","secondary_ability"],abilityOptions.Where(a=>a!=TextAt("background_options","primary_ability")),"background");
            }
        }
        if(species?.SkillChoices is {Choose:>0} speciesSkills)Multi("species_skills","Species skills",["species_options","skill_proficiencies"],speciesSkills.From,speciesSkills.Choose,"species");
        if(cls?.SkillChoices is {Choose:>0} classSkills)Multi("class_skills","Class skills",["class_options","class_skills"],classSkills.From.Except(background?.SkillProficiencies??[],StringComparer.Ordinal),classSkills.Choose);
        if((species?.Traits??[]).Contains("Versatile"))
        {
            var definition=Add("species_origin_feats","Human Origin feat",BuildChoiceMode.Multi,["species_options","origin_feats"],Named(rules.OriginFeats),1,"species");
            var owned=(background?.OriginFeats??[]).Where(name=>name!="Skilled").ToHashSet();var options=definition.Options.Select(o=>owned.Contains(o.Label)?o with{Disabled=true,Reason="Already granted by this background."}:o).ToArray();
            var invalid=ChoiceReading.Names(definition.PreservedSelection).Any(owned.Contains);var next=definition with{Options=options,Status=invalid?BuildChoiceStatus.Unresolved:definition.Status};output[output.Count-1]=next;
            if(invalid)issues.Add(new("duplicate_origin_feat",definition.Path,"Choose a different Origin feat."));
        }
        if(species?.LineageOptions is {Count:>0} lineages)
        {
            Add("lineage","Lineage",BuildChoiceMode.Single,["species_options","lineage"],lineages.Select(l=>Option(l.Name)),section:"species");
            var lineage=lineages.FirstOrDefault(l=>l.Name==TextAt("species_options","lineage"));
            if(lineage?.SpellcastingAbilities is {Count:>0} abilities)Single("lineage_spellcasting_ability","Lineage spellcasting ability",["species_options","lineage_spellcasting_ability"],abilities,"species");
            if((lineage?.Level1Spells??[]).Contains("Choose a cantrip"))Add("lineage_spells","Lineage cantrip",BuildChoiceMode.Multi,["species_options","lineage_spells"],catalog.SpellRecords.Where(s=>s.Level==0&&s.Classes.Contains("Wizard")).Select(Option),1,"species");
        }
        if(species?.ChoiceOptions is {Key.Length:>0} speciesChoice)Add(speciesChoice.Key,speciesChoice.Name,BuildChoiceMode.Single,["species_options",speciesChoice.Key],speciesChoice.Options.Select(Option),section:"species");
        if(species?.LineageOptions is not {Count:>0})
        {
            var race=speciesData.Find(build.Species) is {} found&&found.Name==build.Species?found:null;
            var selected=TextAt("species_options","lineage");
            if(selected.Length==0)selected=(build.SpeciesOptions??new Dictionary<string,ChoiceValue>()).Where(p=>p.Key.EndsWith("_lineage",StringComparison.Ordinal)).Select(p=>p.Value.Text??"").FirstOrDefault(s=>s.Length>0)??"";
            var variant=race?.Version(selected);
            var additions=variant?.AdditionalSpells??race?.AdditionalSpells??[];
            var aliases=new Dictionary<string,string>{{"str","Strength"},{"dex","Dexterity"},{"con","Constitution"},{"int","Intelligence"},{"wis","Wisdom"},{"cha","Charisma"}};
            var castingAbilities=additions.Where(a=>a.Name.Length==0||a.Name==selected).SelectMany(a=>a.AbilityChoices??[]).Select(a=>aliases.GetValueOrDefault(a,a)).Where(Abilities.Contains).Distinct(StringComparer.Ordinal).ToArray();
            if(castingAbilities.Length>0)Single("lineage_spellcasting_ability","Species spellcasting ability",["species_options","lineage_spellcasting_ability"],castingAbilities,"species");
        }
        var classPackages=(cls?.EquipmentPackages??[]).Select(p=>p.Name).ToArray();
        foreach(var pair in new[]{("class_options",classPackages,cls?.ToolChoices??new(0,[])),("background_options",(background?.EquipmentPackages??[]).Select(p=>p.Name).ToArray(),background?.ToolChoices??new(0,[]))})
        {
            if(pair.Item2.Length>0&&!context.HasInventory)Add(pair.Item1+"_equipment","Starting equipment package",BuildChoiceMode.Single,[pair.Item1,"equipment_package"],pair.Item2.Select(Option),section:"equipment",required:false);
            if(pair.Item3.Choose>0)Multi(pair.Item1+"_tools","Tool proficiencies",[pair.Item1,"tool_proficiencies"],toolCatalog?.Expand(pair.Item3.From)??pair.Item3.From,pair.Item3.Choose,"background");
        }
        if(toolCatalog is not null&&(cls?.ToolChoices.Choose??0)==0)
        {
            if(cls?.Name=="Bard")Multi("class_options_tools","Musical instrument proficiencies",["class_options","tool_proficiencies"],toolCatalog.MusicalInstruments,3);
            if(cls?.Name=="Monk")Multi("class_options_tools","Artisan tool or musical instrument proficiency",["class_options","tool_proficiencies"],toolCatalog.ArtisanTools.Concat(toolCatalog.MusicalInstruments),1);
        }
        var backgroundOrigins=(background?.OriginFeats??[]).ToArray();var chosenBackground=ChoiceReading.Names(At(["background_options","origin_feats"])).ToArray();var chosenSpecies=ChoiceReading.Names(At(["species_options","origin_feats"])).ToArray();
        var allowedOrigins=rules.OriginFeats.ToHashSet(StringComparer.Ordinal);var versatile=(species?.Traits??[]).Contains("Versatile");
        var validBackground=At(["background_options","origin_feats"]) is null or {Items:not null}&&chosenBackground.All(backgroundOrigins.Contains);
        var validSpecies=At(["species_options","origin_feats"]) is null or {Items:not null}&&chosenSpecies.Length<=1&&(versatile||chosenSpecies.Length==0)&&chosenSpecies.All(allowedOrigins.Contains);
        if(!validBackground)issues.Add(new("unsupported_origin_source",["background_options","origin_feats"],"Choose Origin feats offered by the selected background."));
        if(!validSpecies)issues.Add(new("unsupported_origin_source",["species_options","origin_feats"],"Choose an Origin feat offered by the selected species."));
        var instances=NativeOriginFeatInstances.Derive(build,background,species).Where(instance=>!instance.GeneralFeat).ToArray();
        var magic=instances.Where(instance=>instance.Name.StartsWith("Magic Initiate",StringComparison.Ordinal)).ToArray();
        foreach(var instance in magic)
        {
            var name=instance.Name;var list=new[]{"Cleric","Druid","Wizard"}.FirstOrDefault(name.Contains)??"";var path=instance.ChoicePath;
            foreach(var quota in new[]{("cantrips",2,0),("spell",1,1)})Add(instance.ChoiceId+"_"+quota.Item1,name+" "+quota.Item1,quota.Item1=="cantrips"?BuildChoiceMode.Multi:BuildChoiceMode.Single,[..path,quota.Item1],catalog.SpellRecords.Where(s=>s.Level==quota.Item3&&s.Classes.Contains(list)).Select(Option),quota.Item2,"spells",details:new(){SourceId=instance.SourceId});
            Add(instance.ChoiceId+"_ability",name+" spellcasting ability",BuildChoiceMode.Single,[..path,"spellcasting_ability"],Named(["Intelligence","Wisdom","Charisma"]),section:"spells",details:new(){SourceId=instance.SourceId});
        }
        if(magic.Select(instance=>instance.Name).Distinct(StringComparer.Ordinal).Count()!=magic.Length)issues.Add(new("duplicate_magic_initiate",["species_options","origin_feats"],"Repeated Magic Initiate feats must choose different spell lists."));
        var toolsByFeat=new Dictionary<string,string[]>{
            ["Crafter"]=["Carpenter's Tools","Leatherworker's Tools","Mason's Tools","Potter's Tools","Smith's Tools","Tinker's Tools","Weaver's Tools","Woodcarver's Tools"],
            ["Musician"]=["Bagpipes","Drum","Dulcimer","Flute","Horn","Lute","Lyre","Pan Flute","Shawm","Viol"],
            ["Skilled"]=["Any artisan's tools","Any gaming set","Any musical instrument","Any other tool"]};
        var toolFeats=instances.Where(instance=>toolsByFeat.ContainsKey(instance.Name));
        foreach(var instance in toolFeats)
        {
            var name=instance.Name;var path=instance.ChoicePath;
            if(name=="Skilled")
            {
                var skillNames=rules.Skills.Select(p=>p.Key);
                var d=Add(instance.ChoiceId,"Skilled: choose three skills or tools",BuildChoiceMode.Multi,[..path,"proficiencies"],Named(skillNames.Select(s=>"Skill: "+s).Concat((toolCatalog?.All??toolsByFeat[name]).Select(t=>"Tool: "+t))),3,"origin",details:new(){SourceId=instance.SourceId});
                var projected=ChoiceReading.Names(instance.Choices.Get("skills")).Select(s=>"Skill: "+s).Concat(ChoiceReading.Names(instance.Choices.Get("tools")).Select(t=>"Tool: "+t)).ToArray();
                var allowed=d.Options.Select(o=>o.Label).ToHashSet();var valid=projected.Length==3&&projected.Distinct(StringComparer.Ordinal).Count()==3&&projected.All(allowed.Contains);
                var own=valid?Array.Empty<BuildIssue>():new[]{new BuildIssue("invalid_choice",d.Path,"Choose three distinct eligible proficiencies for Skilled.")};
                output[output.Count-1]=d with{PreservedSelection=ChoiceValue.ListOf(projected.Select(ChoiceValue.Of)),Status=valid?BuildChoiceStatus.Resolved:BuildChoiceStatus.Unresolved,Issues=own};issues.RemoveAll(i=>i.Path.SequenceEqual(d.Path));issues.AddRange(own);
            }
            else Add(instance.ChoiceId,name+": choose three proficiencies",BuildChoiceMode.Multi,[..path,"tools"],Named(toolCatalog?.Expand(toolsByFeat[name])??toolsByFeat[name]),3,"origin",details:new(){SourceId=instance.SourceId});
        }
        if(cls is not null)
        {
            var className=cls.Name;var subclass=build.Subclass;
            if(level>=3&&cls.Subclasses.Count>0)Add("subclass","Subclass",BuildChoiceMode.Single,["subclass"],cls.Subclasses.Select(Option));
            var supported=cls.Subclasses.Any(s=>s.Name==subclass||s.ShortName==subclass);
            if(!supported||level<3)subclass="";
            var classContext=context.ClassContext with{ClassName=className,SubclassName=subclass,Level=level,ClassOptions=new Dictionary<string,ChoiceValue>(build.ClassOptions??new Dictionary<string,ChoiceValue>(),StringComparer.Ordinal)};
            var report=classChoices.Describe(classContext,level);
            foreach(var d in report.Definitions)
            {
                var definition=Add(d.Key,d.Name,d.Mode,["class_options",d.Key],Named(d.ValidExistingOptions),d.Choose);
                output[output.Count-1]=definition with{Details=definition.Details with{EffectiveSelection=d.EffectiveSelection,SourceId=d.SourceId,SourceLevel=d.SourceLevel},PreservedSelection=d.PreservedSelection,Status=d.Status,Issues=d.Issues};
                issues.RemoveAll(i=>i.Path.SequenceEqual(definition.Path));
            }
            issues.AddRange(report.Issues);
            var progression=ClassProgressionRules.AtLevel(classes,className,level,subclass);
            var masteryCount=progression.Resources.Count("weapon_mastery_count");if(masteryCount>0)Multi("weapon_masteries","Weapon Mastery",["weapon_masteries"],context.EligibleWeaponMasteries,masteryCount,"equipment");
            if(progression.Spellcasting is { } casting)
            {
                var max=casting.AvailableSpellLevels.DefaultIfEmpty(1).Max();
                var locked=report.Definitions.Where(d=>d.ApplyTo==ClassChoiceApplyTarget.Spellbook).SelectMany(d=>d.ValidSelected).Distinct(StringComparer.Ordinal).ToArray();
                var baseBook=(build.Spells?.Spellbook??[]).Where(n=>n.Length>0).ToArray();if(baseBook.Length==0)baseBook=(build.Spells?.Known??[]).Where(n=>n.Length>0).ToArray();
                var effectiveBook=baseBook.Concat(locked).Distinct(StringComparer.Ordinal).ToArray();
                foreach(var quota in new[]{("cantrips",casting.CantripLimit),("spellbook",casting.SpellbookLimit),("prepared",casting.PreparedLimit),("known",className=="Wizard"?0:casting.KnownLimit)})
                {
                    if(quota.Item2<=0)continue;
                    var lists=quota.Item1=="prepared"&&className=="Bard"&&level>=10?new[]{"Bard","Cleric","Druid","Wizard"}:new[]{casting.SpellList};
                    var options=catalog.SpellRecords.Where(s=>s.Classes.Any(lists.Contains)&&(quota.Item1=="cantrips"?s.Level==0:s.Level>=1&&s.Level<=max)).ToArray();
                    if(quota.Item1=="cantrips")
                    {
                        var ownedElsewhere=context.ClassContext.CantripSources.Where(s=>s.SourceId!="class:"+className&&s.CountsAsClasses.Contains(className)).Select(s=>s.SpellName).ToHashSet(StringComparer.Ordinal);
                        options=options.Where(s=>!ownedElsewhere.Contains(s.Name)).ToArray();
                    }
                    if(quota.Item1=="prepared"&&className=="Wizard")options=options.Where(s=>effectiveBook.Contains(s.Name)).ToArray();
                    var optional=quota.Item1=="prepared"&&className=="Wizard";
                    var count=quota.Item2+(quota.Item1=="spellbook"&&className=="Wizard"?locked.Length:0);
                    var definition=Add("spells_"+quota.Item1,quota.Item1+" spells",BuildChoiceMode.Multi,["spells",quota.Item1],options.Select(Option),count,"spells",required:!optional,details:new(){Minimum=optional?0:count,Maximum=count});
                    if(quota.Item1=="spellbook"&&className=="Wizard")
                    {
                        var allowed=options.Select(s=>s.Name).ToHashSet();var valid=effectiveBook.Length==count&&baseBook.Distinct(StringComparer.Ordinal).Count()==baseBook.Length&&effectiveBook.All(allowed.Contains);
                        var own=valid?Array.Empty<BuildIssue>():new[]{new BuildIssue("invalid_choice",definition.Path,"Complete the Wizard spellbook with eligible distinct spells.")};
                        output[output.Count-1]=definition with{Details=definition.Details with{EffectiveSelection=ChoiceValue.ListOf(effectiveBook.Select(ChoiceValue.Of)),LockedValues=locked},Status=valid?BuildChoiceStatus.Resolved:BuildChoiceStatus.Unresolved,Options=definition.Options.Select(o=>locked.Contains(o.Label)?o with{Disabled=true,Reason="Added to the spellbook by the selected Savant feature."}:o).ToArray(),Issues=own};issues.RemoveAll(i=>i.Path.SequenceEqual(definition.Path));issues.AddRange(own);
                    }
                }
            }
        }
        return new(output.AsReadOnly(),issues.Distinct().ToArray());
    }
}
