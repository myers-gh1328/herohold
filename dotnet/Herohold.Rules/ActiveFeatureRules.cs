using Herohold.Core;
namespace Herohold.Rules;
public sealed class ActiveFeatureRules(RulesCatalogSnapshot catalog,ClassProgressionCatalog classes,ClassChoiceRules choices,RulesReference rules,SpeciesSourceCatalog rawSpecies)
{
    private static readonly string[] DamageTypes=["Acid","Bludgeoning","Cold","Fire","Force","Lightning","Necrotic","Piercing","Poison","Psychic","Radiant","Slashing","Thunder"];
    /// <param name="choices">The class choice context for this build, which also names granted spells' owners.</param>
    public ActiveFeatureSummary Derive(BuildFacts facts,NativeBuildSummary build,ClassChoiceContext choiceContext)
    {
        var className=build.ClassName.Length>0?build.ClassName:facts.ClassName;var level=facts.Level;
        var cls=catalog.Class(className);var species=catalog.SpeciesNamed(facts.Species);var background=catalog.Background(facts.Background);
        var subclass=level<3?null:cls?.Subclasses.FirstOrDefault(s=>s.Name==build.SubclassName||s.ShortName==build.SubclassName);
        var shortName=subclass?.ShortName??"";var subclassName=subclass?.Name??"";
        var progression=cls is null?null:ClassProgressionRules.AtLevel(classes,className,level,subclassName);
        var options=facts.ClassOptions;var speciesOptions=facts.SpeciesOptions??new Dictionary<string,ChoiceValue>();
        var issues=new List<BuildIssue>();var details=new List<ActiveFeatureDetail>();var defenses=new List<DamageDefense>();var senses=new List<CharacterSense>();
        var descriptions=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var pair in rules.FeatureDescriptions.Concat(species?.TraitDescriptions??new Dictionary<string,string>()).Concat(background?.FeatureDescriptions??new Dictionary<string,string>()).Concat(cls?.FeatureDescriptions??new Dictionary<string,string>()).Concat(subclass?.FeatureDescriptions??new Dictionary<string,string>()))descriptions[pair.Key]=pair.Value;
        foreach(var feat in catalog.FeatRecords)if(feat.Name.Length>0&&feat.Description.Length>0)descriptions[feat.Name]=feat.Description;
        IReadOnlyList<CatalogRelatedFeature> Related(string name,string source)
        {
            var related=source.StartsWith("class:",StringComparison.Ordinal)?cls?.FeatureRelatedFeatures:source.StartsWith("subclass:",StringComparison.Ordinal)?subclass?.FeatureRelatedFeatures:null;
            return related?.GetValueOrDefault(name) is {} list?[..list.Where(r=>r.Name.Length>0&&(r.Level??0)<=level)]:[];
        }
        void Feature(string name,string source){if(name.Length>0&&!details.Any(d=>d.Name==name&&d.SourceId==source))details.Add(new(name,descriptions.GetValueOrDefault(name,""),source,Related(name,source)));}
        void Defense(string type,string source,string kind="resistance")
        {
            var normalized=kind=="conditionImmunity"?type:DamageTypes.FirstOrDefault(t=>t.Equals(type,StringComparison.OrdinalIgnoreCase));
            if(normalized is not null&&!defenses.Any(d=>d.Type==normalized&&d.Kind==kind&&d.Source==source))defenses.Add(new(){Type=normalized,Kind=kind,Source=source});
        }
        void Sense(string type,int range,string source)
        {
            if(range<=0)return;var existing=senses.FirstOrDefault(s=>s.Type==type);if(existing is null)senses.Add(new(){Type=type,Range=range,Sources=[source]});else senses[senses.IndexOf(existing)]=existing with{Range=Math.Max(range,existing.Range),Sources=[..existing.Sources.Append(source).Distinct()]};
        }
        var speciesName=species?.Name??"";foreach(var name in species?.Traits??[])Feature(name,"species:"+speciesName);
        Feature(background?.Feature??"","background:"+(background?.Name??""));
        var originInstances=NativeOriginFeatInstances.Derive(facts,background,species);var origins=originInstances.Select(instance=>instance.Name).ToList();foreach(var origin in originInstances)Feature(origin.Name,origin.SourceId);
        if(progression is not null){foreach(var feature in progression.FeaturesUpToLevel)Feature(feature,"class:"+className);foreach(var feature in progression.SubclassFeaturesUpToLevel)Feature(feature,"subclass:"+subclassName);}
        var configured=new List<FeatureConfiguration>();
        var report=cls is null?new ClassChoiceReport():choices.Describe(choiceContext with{ClassName=className,SubclassName=subclassName},level);
        var selectedDefinitions=report.Definitions.Where(d=>d.Status==BuildChoiceStatus.Resolved).ToArray();
        var featureOptionKeys=new HashSet<string>{"fighting_style","champion_additional_fighting_styles","divine_order","primal_order","hunters_prey","defensive_tactics","aspect_of_the_wilds","elemental_fury","blessed_strikes","elemental_affinity","eldritch_invocations","metamagic"};
        foreach(var definition in selectedDefinitions.Where(d=>featureOptionKeys.Contains(d.Key)))foreach(var name in definition.ValidSelected)Feature(name,definition.SourceId);
        foreach(var maneuver in selectedDefinitions.Where(d=>d.Key=="maneuvers").SelectMany(d=>d.ValidSelected))Feature("Maneuver: "+maneuver,"class_options:maneuvers");
        var existingConfigurations=facts.FeatureConfigurations??[];
        void Configuration(string key,string name,IEnumerable<string> universe,int gained,string timing,string fallback="",bool requireInitial=true)
        {
            var eligible=universe.Distinct(StringComparer.Ordinal).ToArray();var initial=ChoiceReading.TextOf(options.Get(key));if(initial.Length==0)initial=fallback;if(requireInitial&&initial.Length==0)return;
            var existing=existingConfigurations.FirstOrDefault(c=>c.FeatureId==key);var current=existing?.CurrentValue??"";if(current.Length==0)current=initial;
            configured.Add(new(){FeatureId=key,FeatureName=name,ClassName=className,SubclassName=subclassName,LevelGained=gained,InitialValue=initial,CurrentValue=current,ChangeTiming=timing,EligibleOptions=eligible});
            if(!eligible.Contains(current,StringComparer.Ordinal)){issues.Add(new("invalid_configuration",["feature_configurations",key],"Choose an eligible value for "+name+"."));return;}
            if(featureOptionKeys.Contains(key))Feature(current,"configuration:"+key);
        }
        foreach(var key in new[]{"aspect_of_the_wilds","hunters_prey","defensive_tactics","structured_spell_choice"})
        {
            var definition=report.Definitions.FirstOrDefault(d=>d.Key==key);if(definition is not null)Configuration(key,definition.Name,definition.ValidExistingOptions,definition.SourceLevel,key is "hunters_prey" or "defensive_tactics"?"short_or_long_rest":"long_rest");
        }
        if(className=="Warlock"&&shortName=="Fiend"&&level>=10)Configuration("fiendish_resilience_damage_type","Fiendish Resilience damage type",DamageTypes.Where(t=>t!="Force"),10,"short_or_long_rest","Fire",false);
        if(className=="Wizard"&&shortName=="Diviner"&&level>=10)Configuration("third_eye_mode","The Third Eye",["Darkvision","Greater Comprehension","See Invisibility"],10,"short_or_long_rest","Darkvision",false);
        if(className=="Wizard"&&level>=18)foreach(var definition in report.Definitions.Where(d=>d.Key.StartsWith("spell_mastery_level_",StringComparison.Ordinal)))Configuration(definition.Key,definition.Name,definition.ValidExistingOptions,18,"long_rest");
        var lineage=ChoiceReading.TextOf(speciesOptions.Get("lineage"));if(lineage.Length==0)lineage=speciesOptions.Where(p=>p.Key.EndsWith("_lineage",StringComparison.Ordinal)).Select(p=>ChoiceReading.TextOf(p.Value)).FirstOrDefault(s=>s.Length>0)??"";
        var allowedLineages=(species?.LineageOptions??[]).Select(l=>l.Name).Concat(species?.ChoiceOptions?.Options??[]).ToHashSet(StringComparer.Ordinal);
        if(!allowedLineages.Contains(lineage))lineage="";if(lineage.Length>0)Feature(lineage,"species_choice:"+speciesName);
        var race=rawSpecies.Find(speciesName) is {} found&&found.Name==speciesName?found:null;
        var variant=race?.Version(lineage);
        foreach(var (types,kind) in new[]{(variant?.Resist??race?.Resist,"resistance"),(variant?.Immune??race?.Immune,"immunity"),(variant?.ConditionImmune??race?.ConditionImmune,"conditionImmunity")})foreach(var type in types??[])Defense(type,"Species: "+speciesName,kind);
        Sense("Darkvision",variant?.Darkvision??race?.Darkvision??0,"Species: "+speciesName);
        if(speciesName=="Dragonborn")
        {
            var ancestry=ChoiceReading.TextOf(speciesOptions.Get("draconic_ancestry"));var mapping=new Dictionary<string,string>{{"Black","Acid"},{"Copper","Acid"},{"Blue","Lightning"},{"Bronze","Lightning"},{"Brass","Fire"},{"Gold","Fire"},{"Red","Fire"},{"Green","Poison"},{"Silver","Cold"},{"White","Cold"}};
            if((species?.ChoiceOptions?.Options??[]).Contains(ancestry)&&mapping.TryGetValue(ancestry,out var type))Defense(type,"Species: Dragonborn · "+ancestry);
        }
        var featNames=new HashSet<string>();foreach(var feat in facts.Feats??[])
        {
            var name=feat.Name;Feature(name,"feat:"+feat.InstanceId);featNames.Add(name);
            if(name=="Boon of Energy Resistance")foreach(var type in ChoiceReading.Names(feat.Choices.Get("damage_types")).Where(t=>DamageTypes.Contains(t)&&t is not ("Force" or "Bludgeoning" or "Piercing" or "Slashing")))Defense(type,"Feat: "+name);
        }
        if(featNames.Contains("Skulker"))Sense("Blindsight",10,"Feat: Skulker");if(featNames.Contains("Boon of Truesight"))Sense("Truesight",60,"Feat: Boon of Truesight");
        foreach(var style in FightingStyleRules.Sources(facts).Where(style=>style.Name=="Blind Fighting"))Sense("Blindsight",10,style.SourceId);
        if(className=="Ranger"&&level>=18)Sense("Blindsight",30,"Class: Feral Senses");
        if(className=="Ranger"&&shortName=="Gloom Stalker"&&level>=3)Sense("Darkvision",(senses.FirstOrDefault(s=>s.Type=="Darkvision")?.Range??0)+60,"Subclass: Umbral Sight");
        if(className=="Cleric"&&shortName=="War"&&level>=17)foreach(var type in new[]{"Bludgeoning","Piercing","Slashing"})Defense(type,"Subclass: Avatar of Battle");
        if(className=="Sorcerer"&&shortName=="Draconic"&&level>=6){var affinity=ChoiceReading.TextOf(options.Get("elemental_affinity"));if(new[]{"Acid","Cold","Fire","Lightning","Poison"}.Contains(affinity))Defense(affinity,"Subclass: Elemental Affinity");}
        if(className=="Sorcerer"&&shortName=="Aberrant"&&level>=6)Defense("Psychic","Subclass: Psychic Defenses");
        if(className=="Warlock"&&shortName=="Great Old One"&&level>=10)Defense("Psychic","Subclass: Thought Shield");
        var fiend=configured.FirstOrDefault(c=>c.FeatureId=="fiendish_resilience_damage_type");if(fiend is not null&&DamageTypes.Contains(fiend.CurrentValue)&&fiend.CurrentValue!="Force")Defense(fiend.CurrentValue,"Subclass: Fiendish Resilience");
        if(className=="Druid"&&shortName=="Land"&&level>=10)
        {
            Defense("Poisoned","Subclass: Nature's Ward","conditionImmunity");var terrain=configured.FirstOrDefault(c=>c.FeatureId=="structured_spell_choice")?.CurrentValue??"";
            var type=terrain switch{"Arid Land"=>"Fire","Polar Land"=>"Cold","Temperate Land"=>"Lightning","Tropical Land"=>"Poison",_=>""};if(type.Length>0)Defense(type,"Subclass: Nature's Ward · "+terrain);
        }
        return new(){Features=details.Select(d=>d.Name).ToArray(),Details=details.AsReadOnly(),Defenses=defenses.AsReadOnly(),Senses=senses.AsReadOnly(),Proficiencies=build.ProficiencySources.Select(p=>p with{}).ToArray(),FeatureConfigurations=configured.AsReadOnly(),SpeciesTraits=(species?.Traits??[]).ToArray(),OriginFeats=origins.AsReadOnly(),Size=species?.Size??"",BackgroundFeature=background?.Feature??"",Issues=issues.AsReadOnly()};
    }
}
