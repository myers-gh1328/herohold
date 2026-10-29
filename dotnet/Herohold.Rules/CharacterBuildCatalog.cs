using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;
namespace Herohold.Rules;

/// <summary>Trusted source catalog and canonical source adapter. Never consumes legacy numerical deltas.</summary>
public sealed class CharacterBuildCatalog
{
    private readonly ClassProgressionCatalog classes;
    private readonly Dictionary<string,CatalogBackground> backgrounds;
    private readonly Dictionary<string,CatalogSpecies> species;
    private CharacterBuildCatalog(ClassProgressionCatalog classes,Dictionary<string,CatalogBackground> backgrounds,Dictionary<string,CatalogSpecies> species)
    {this.classes=classes;this.backgrounds=backgrounds;this.species=species;}
    public static CharacterBuildCatalog Parse(ClassProgressionCatalog classes,string backgroundsJson,string speciesJson)
    {
        ArgumentNullException.ThrowIfNull(classes);
        return new(classes,Records(backgroundsJson).ToDictionary(p=>p.Key,p=>CatalogBackground.From(p.Value),StringComparer.OrdinalIgnoreCase),Records(speciesJson).ToDictionary(p=>p.Key,p=>CatalogSpecies.From(p.Value),StringComparer.OrdinalIgnoreCase));
    }
    /// <summary>The build catalog over a parsed rules catalog's backgrounds and species.</summary>
    public static CharacterBuildCatalog From(ClassProgressionCatalog classes,RulesCatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(classes);ArgumentNullException.ThrowIfNull(catalog);
        Dictionary<string,T> Unique<T>(IEnumerable<T> records,Func<T,string> name){var result=new Dictionary<string,T>(StringComparer.OrdinalIgnoreCase);foreach(var r in records)if(name(r).Length==0||!result.TryAdd(name(r),r))throw new ArgumentException("Build catalog identities must be unique nonempty names.");return result;}
        return new(classes,Unique(catalog.BackgroundRecords,b=>b.Name),Unique(catalog.SpeciesRecords,s=>s.Name));
    }
    public DependentResourceSummary DependentResources(BuildFacts build,StandingSummary standing)
    {
        var selected=ChoiceReading.TextOf(build.ClassOptions.Get("battle_master_save_ability"));
        Ability? maneuver=selected=="Strength"?Ability.Strength:selected=="Dexterity"?Ability.Dexterity:null;
        var feats=(build.Feats??[]).Select(feat=>feat.Name).ToList();
        return DependentResourceRules.Calculate(classes,build.ClassName,build.Subclass,build.Level,standing.Modifiers.ToDictionary(pair=>pair.Key,pair=>pair.Value.Total),maneuver,feats);
    }
    private static Dictionary<string,JsonObject> Records(string json)
    {
        if(string.IsNullOrWhiteSpace(json)||Encoding.UTF8.GetByteCount(json)>16*1024*1024)throw new ArgumentException("A build catalog is empty or too large.");
        JsonArray array;
        try{array=JsonNode.Parse(json,documentOptions:new(){MaxDepth=64}) as JsonArray??throw new ArgumentException("Build catalogs must be arrays.");}
        catch(JsonException error){throw new ArgumentException("A build catalog contains invalid JSON.",error);}
        if(array.Count is <1 or >1000)throw new ArgumentException("A build catalog has too many records.");
        var result=new Dictionary<string,JsonObject>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in array)
            if(item is not JsonObject record||string.IsNullOrWhiteSpace(Text(record["name"]))||Text(record["name"]).Length>200||!result.TryAdd(Text(record["name"]),record))throw new ArgumentException("Build catalog identities must be unique nonempty names.");
        return result;
    }
    /// <param name="observed">What the character showed before this calculation, when it has one; see <see cref="ObservedProficiencies"/>.</param>
    public NativeBuildSummary Derive(BuildFacts build,IReadOnlyList<EquipmentItem> equipment,bool allowUnresolved=false,ObservedProficiencies? observed=null)
    {
        ArgumentNullException.ThrowIfNull(build);ArgumentNullException.ThrowIfNull(equipment);observed??=new();
        var className=build.ClassName;var level=build.Level;var subclassName=build.Subclass;
        var cls=classes.Find(className);var progression=ClassProgressionRules.AtLevel(classes,className,level,subclassName);
        className=progression.ClassName;
        var background=Lookup(backgrounds,build.Background,allowUnresolved);var kind=Lookup(species,build.Species,allowUnresolved);
        var options=build.ClassOptions;var bgOptions=build.BackgroundOptions;var spOptions=build.SpeciesOptions;
        var issues=new List<string>();
        foreach(var (list,saveList) in new[]{(build.ManualSkills,false),(build.ManualExpertise,false),(build.ManualSaves,true),(observed.PreviousSkills,false),(observed.PreviousExpertise,false),(observed.PreviousSaves,true)})
        {
            if(list is null)continue;
            if(list.Count>(saveList?6:18))throw new ArgumentException("Proficiency source lists must be bounded arrays.");
            foreach(var name in list)
            {
                if(saveList)_=RequiredAbility(name);
                else if(!StandingRules.IsSupportedSkill(name))throw new ArgumentException("A proficiency ownership source contains an unsupported skill.");
            }
        }
        var previousSkills=(observed.PreviousSkills??[]).ToHashSet();var previousExpertise=(observed.PreviousExpertise??[]).ToHashSet();
        var previousSaves=(observed.PreviousSaves??[]).Select(RequiredAbility).ToHashSet();
        var manualSkills=new HashSet<string>(build.ManualSkills??[]);var manualExpertise=new HashSet<string>(build.ManualExpertise??[]);
        var manualSaves=new HashSet<Ability>((build.ManualSaves??[]).Select(RequiredAbility));
        // An imported character without base scores starts from the scores it showed.
        var baseScores=new Dictionary<Ability,int>(build.BaseAbilityScores??observed.Scores??new Dictionary<Ability,int>());
        BackgroundAbilityBonus? weighted=null;
        var bgRule=background.AbilityOptions;
        if(bgRule.Mode=="choose_weighted")
        {
            var allowed=bgRule.Abilities.Select(RequiredAbility).ToArray();
            Ability? Choice(string name)
            {
                if(string.IsNullOrEmpty(name))return null;
                if(Enum.TryParse<Ability>(name,out var ability)&&Enum.IsDefined(ability)&&allowed.Contains(ability))return ability;
                if(!allowUnresolved)throw new ArgumentException("Choose a background ability offered by this background.");
                issues.Add("A background ability choice needs review.");return null;
            }
            var splitEvenly=ChoiceReading.TextOf(bgOptions.Get("ability_bonus_mode"))=="three";
            var primary=splitEvenly?null:Choice(ChoiceReading.TextOf(bgOptions.Get("primary_ability")));var secondary=splitEvenly?null:Choice(ChoiceReading.TextOf(bgOptions.Get("secondary_ability")));
            if(!splitEvenly&&(primary is null||secondary is null))
            {
                if(!allowUnresolved)throw new ArgumentException("Choose both background abilities before confirming.");
                issues.Add("Choose both background abilities.");
            }
            if(primary.HasValue&&primary==secondary)
            {
                if(!allowUnresolved)throw new ArgumentException("Choose two different background abilities.");
                issues.Add("Choose a different secondary background ability.");secondary=null;
            }
            weighted=new(){Name=background.Name.Length>0?background.Name:"Background",AllowedAbilities=allowed,Primary=primary,Secondary=secondary,PrimaryBonus=bgRule.PrimaryBonus??2,SecondaryBonus=bgRule.SecondaryBonus??1,SplitEvenly=splitEvenly};
        }
        var adjustments=new List<ScoreAdjustment>();
        foreach(var record in build.AbilityScoreAdjustments??[])
        {
            var sourceType=record.SourceType;var raises=new Dictionary<Ability,int>(record.Adjustments);
            var id=record.SourceId??record.FeatInstanceId??record.InstanceId??"";
            if(id.Length==0)id=sourceType=="class_level"?"class-level:"+record.SourceClass+":"+(record.SourceLevel??0):sourceType=="feat"?"feat:"+(record.SourceFeat??record.SourceName??""):"legacy:"+adjustments.Count;
            adjustments.Add(new(){SourceId=id,SourceName=record.SourceName??id,Increases=raises,Maximum=record.Maximum??20});
        }
        var scores=AbilityScoreRules.Calculate(new(){ClassName=className,Level=level,BaseScores=baseScores,Background=weighted,Adjustments=adjustments});
        var originInstances=NativeOriginFeatInstances.Derive(build,background,kind);
        var origins=originInstances.Select(instance=>instance.Name).ToList();
        var originSkills=originInstances.Where(instance=>instance.Name=="Skilled").SelectMany(instance=>ChoiceReading.Names(instance.Choices.Get("skills")));
        var skills=new HashSet<string>(background.SkillProficiencies.Concat(ChoiceReading.Names(spOptions.Get("skill_proficiencies"),true))
            .Concat(ChoiceReading.Names(options.Get("class_skills"),true)).Concat(originSkills).Concat(manualSkills));
        var expertise=new HashSet<string>(manualExpertise);skills.UnionWith(manualExpertise);
        var saves=new HashSet<Ability>(cls.SavingThrowProficiencies.Select(RequiredAbility).Concat(manualSaves));
        var shortName=level>=3?cls.SubclassNamed(progression.SubclassName)?.ShortName??"":"";
        void SkillsFrom(string key)=>skills.UnionWith(ChoiceReading.Names(options.Get(key),true));
        void ExpertiseFrom(string key)=>expertise.UnionWith(ChoiceReading.Names(options.Get(key),true));
        if(className=="Fighter"&&shortName=="Battle Master")SkillsFrom("student_of_war_skill");
        if(className=="Bard")
        {if(level>=2)ExpertiseFrom("bard_expertise");if(level>=9)ExpertiseFrom("bard_level_9_expertise");if(shortName=="Lore")SkillsFrom("lore_skills");}
        if(className=="Ranger")
        {if(level>=2)ExpertiseFrom("ranger_deft_explorer_expertise");if(level>=9)ExpertiseFrom("ranger_level_9_expertise");if(shortName=="Fey Wanderer")SkillsFrom("fey_wanderer_skill");if(shortName=="Gloom Stalker"&&level>=7&&ChoiceReading.TextOf(options.Get("iron_mind_saving_throw")).Length>0)saves.Add(RequiredAbility(ChoiceReading.TextOf(options.Get("iron_mind_saving_throw"))));}
        if(className=="Rogue"){ExpertiseFrom("expertise");if(level>=6)ExpertiseFrom("rogue_level_6_expertise");if(level>=15){saves.Add(Ability.Wisdom);saves.Add(Ability.Charisma);}}
        if(className=="Wizard"&&level>=2)ExpertiseFrom("wizard_scholar_skill");
        if(className=="Barbarian"&&level>=3)SkillsFrom("primal_knowledge_skill");
        if(className=="Monk"){if(level>=14)saves.UnionWith(Enum.GetValues<Ability>());if(shortName=="Mercy")skills.UnionWith(new[]{"Insight","Medicine"});}
        var feats=new List<NumericFeatSelection>();
        foreach(var feat in build.Feats??[])
        {
            var name=feat.Name;var choices=feat.Choices;
            var abilityChoice=choices.Get("ability");var featAbility=abilityChoice?.Text??"";
            if(abilityChoice is not null&&(abilityChoice.Text is null||featAbility.Length>0&&(!Enum.TryParse<Ability>(featAbility,out var parsedAbility)||!Enum.IsDefined(parsedAbility)||parsedAbility.ToString()!=featAbility)))
            {
                if(!allowUnresolved)throw new ArgumentException("A feat ability choice must name one of the six abilities.");
                issues.Add(name+" ability choice needs review.");continue;
            }
            Ability? ability=featAbility.Length==0?null:RequiredAbility(featAbility);
            var selection=new NumericFeatSelection{Name=name,Skill=ChoiceReading.TextOf(choices.Get("skill")),SkillProficiency=ChoiceReading.TextOf(choices.Get("skill_proficiency")),Expertise=ChoiceReading.TextOf(choices.Get("expertise")),Ability=ability};
            var missing=name=="Skill Expert"&&(selection.SkillProficiency.Length==0||selection.Expertise.Length==0)
                ||name is "Keen Mind" or "Observant"&&selection.Skill.Length==0||name=="Resilient"&&ability is null||name=="Boon of Skill"&&selection.Expertise.Length==0;
            if(missing&&allowUnresolved){issues.Add(name+" choices need review.");continue;}
            feats.Add(selection);
        }
        // Preserve only unexplained final grants. Known feat grants are resolved first and never fed back as base.
        var input=new StandingInput{ClassName=className,Subclass=progression.SubclassName,Level=level,Scores=scores.Scores,Equipment=equipment,
            SaveProficiencies=saves.ToList(),SkillProficiencies=skills.ToList(),Expertise=expertise.ToList(),OriginFeats=origins,Feats=feats,
            DivineOrder=className=="Cleric"?Order<DivineOrder>(options.Get("divine_order")):null,PrimalOrder=className=="Druid"?Order<PrimalOrder>(options.Get("primal_order")):null};
        var unresolvedOwnedExpertise=new HashSet<string>();var unresolvedOwnedSkills=new HashSet<string>();
        if(allowUnresolved)
        {
            HashSet<string> FutureSkills()
            {
                var future=new HashSet<string>(skills);
                foreach(var feat in feats)
                {
                    if(feat.Name=="Skill Expert"&&StandingRules.IsSupportedSkill(feat.SkillProficiency))future.Add(feat.SkillProficiency);
                    if((feat.Name is "Keen Mind" or "Observant")&&StandingRules.IsSupportedSkill(feat.Skill))future.Add(feat.Skill);
                    if(feat.Name=="Boon of Skill")future.UnionWith(StandingRules.SupportedSkills);
                }
                return future;
            }
            while(true)
            {
                var future=FutureSkills();
                var invalid=feats.Where(feat=>feat.Name=="Skill Expert"&&!future.Contains(feat.Expertise)).ToList();
                if(invalid.Count==0)break;
                foreach(var feat in invalid)
                {
                    feats.Remove(feat);unresolvedOwnedExpertise.Add(feat.Expertise);unresolvedOwnedSkills.Add(feat.SkillProficiency);
                    issues.Add("Skill Expert Expertise needs a proficient skill.");
                }
            }
            foreach(var orphan in expertise.Except(FutureSkills()).ToArray()){expertise.Remove(orphan);unresolvedOwnedExpertise.Add(orphan);issues.Add("An Expertise choice needs a proficient skill.");}
            input=input with{Expertise=expertise.ToList(),Feats=feats};
        }
        var provisional=StandingRules.Calculate(input);
        foreach(var skill in (observed.Skills??[]).Except(provisional.SkillProficiencies).Except(previousSkills).Except(unresolvedOwnedSkills))
        {skills.Add(skill);manualSkills.Add(skill);issues.Add("Retained unattributed skill proficiency: "+skill);}
        foreach(var skill in (observed.Expertise??[]).Except(provisional.Expertise).Except(previousExpertise).Except(unresolvedOwnedExpertise))
        {expertise.Add(skill);skills.Add(skill);manualExpertise.Add(skill);if(!provisional.SkillProficiencies.Contains(skill))manualSkills.Add(skill);issues.Add("Retained imported or unattributed Expertise: "+skill);}
        foreach(var ability in (observed.Saves??[]).Select(RequiredAbility).Except(provisional.SaveProficiencies).Except(previousSaves))
        {saves.Add(ability);manualSaves.Add(ability);issues.Add("Retained unattributed saving throw proficiency: "+ability);}
        foreach(var skill in manualExpertise)skills.Add(skill);
        var final=StandingRules.Calculate(input with{SkillProficiencies=skills.ToList(),Expertise=expertise.ToList(),SaveProficiencies=saves.ToList()});
        var summary=new NativeBuildSummary{ClassName=progression.ClassName,SubclassName=progression.SubclassName,Scores=scores,Standing=final,ManualSources=new(){Skills=manualSkills.ToList().AsReadOnly(),Expertise=manualExpertise.ToList().AsReadOnly(),Saves=manualSaves.ToList().AsReadOnly()},Issues=issues.Distinct().ToList().AsReadOnly()};
        var proficiencySources=ClassChoiceProficiencySources.Build(build,cls,background,kind,summary);
        var languages=LanguageRules.Derive(build,proficiencySources.Where(source=>source.Kind==ProficiencySourceKind.Language).ToArray(),allowUnresolved,observed.Languages,observed.PreviousLanguages);
        return summary with{Languages=languages,ProficiencySources=proficiencySources.Where(source=>source.Kind!=ProficiencySourceKind.Language).Concat(languages.Sources).ToList().AsReadOnly(),Issues=summary.Issues.Concat(languages.Issues).Distinct().ToList().AsReadOnly()};
    }
    private static T Lookup<T>(Dictionary<string,T> records,string name,bool draft)where T:new()=>records.TryGetValue(name,out var record)?record:draft?new():throw new ArgumentException("A selected build source is missing from the trusted catalog.");
    private static T? Order<T>(ChoiceValue? choice)where T:struct,Enum=>ChoiceReading.TextOf(choice).Length==0?null:Enum.TryParse<T>(ChoiceReading.TextOf(choice),out var value)&&Enum.IsDefined(value)?value:throw new ArgumentException("A class order choice is invalid.");
    private static Ability RequiredAbility(string name)=>Enum.TryParse<Ability>(name,out var ability)&&Enum.IsDefined(ability)?ability:throw new ArgumentException("A source contains an invalid ability name.");
    private static string Text(JsonNode? node,string fallback="")=>node is null?fallback:node is JsonValue value&&value.TryGetValue<string>(out var text)?text:throw new ArgumentException("A source identity must be text.");
}

/// <summary>
/// What the character showed before this calculation. Proficiencies and languages it showed that its sources no longer
/// explain, and that the rules did not derive last time, are kept as hand-entered (an imported sheet's own entries).
/// </summary>
public sealed record ObservedProficiencies
{
    public IReadOnlyList<string>? Skills { get; init; }
    public IReadOnlyList<string>? Expertise { get; init; }
    public IReadOnlyList<string>? Saves { get; init; }
    public IReadOnlyList<string>? PreviousSkills { get; init; }
    public IReadOnlyList<string>? PreviousExpertise { get; init; }
    public IReadOnlyList<string>? PreviousSaves { get; init; }
    public IReadOnlyList<string>? Languages { get; init; }
    public IReadOnlyList<string>? PreviousLanguages { get; init; }
    /// <summary>The final scores it showed, used as its base when the build has none.</summary>
    public IReadOnlyDictionary<Ability,int>? Scores { get; init; }

    /// <summary>Reads what a rules character document showed.</summary>
    public static ObservedProficiencies From(JsonObject canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        var native=canonical["folio_native_build_sources"] as JsonObject;
        if(native is not null&&native.Any(pair=>pair.Key is not ("skills" or "expertise" or "saves" or "derivedSkills" or "derivedExpertise" or "derivedSaves")))throw new ArgumentException("The native proficiency sources have unsupported fields.");
        var language=canonical["folio_language_sources"];
        if(language is not null and not JsonObject||language is JsonObject sidecar&&sidecar.Any(pair=>pair.Key is not ("manual" or "derived")))throw new ArgumentException("Language ownership contains an unsupported field.");
        Dictionary<Ability,int>? scores=null;
        if(canonical["ability_scores"] is JsonObject shown)
        {
            scores=[];
            foreach(var pair in shown)scores.Add(Enum.TryParse<Ability>(pair.Key,out var ability)&&Enum.IsDefined(ability)?ability:throw new ArgumentException("A source contains an invalid ability name."),pair.Value is JsonValue v&&v.TryGetValue<int>(out var n)?n:10);
        }
        return new()
        {
            Skills=List(canonical["skill_proficiencies"]),Expertise=List(canonical["expertise"]),Saves=List(canonical["saving_throw_proficiencies"]),
            PreviousSkills=List(native?["derivedSkills"]),PreviousExpertise=List(native?["derivedExpertise"]),PreviousSaves=List(native?["derivedSaves"]),
            Languages=List(canonical["languages"]),PreviousLanguages=List(language?["derived"]),Scores=scores
        };
    }
    private static IReadOnlyList<string>? List(JsonNode? node)=>node switch
    {
        null=>null,
        JsonArray array=>array.Count>100?throw new ArgumentException("Proficiency source lists must be bounded arrays."):array.Select(item=>item is JsonValue value&&value.TryGetValue<string>(out var text)?text:throw new ArgumentException("A source identity must be text.")).Where(name=>name.Length>0).ToArray(),
        JsonValue value when value.TryGetValue<string>(out var text)=>text.Length>0?[text]:[],
        _=>throw new ArgumentException("A build source must be an array.")
    };
}
