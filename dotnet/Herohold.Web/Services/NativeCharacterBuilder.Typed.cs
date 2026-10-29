using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Rules;
namespace Herohold.Web.Services;

/// <summary>Values one calculation finds that its own choices read back: the owned features, the Expertise shown and the spell grant records.</summary>
internal sealed record FoundValues(IReadOnlyList<string> Features,IReadOnlyList<string> Expertise,IReadOnlyList<SpellGrantRecord> GrantRecords)
{
    public static readonly FoundValues None=new([],[],[]);
}

public sealed partial class NativeCharacterBuilder
{
    /// <summary>The build as the rules calculate it: owned feats keyed by identity, and a subclass only from level 3 and only one the class offers.</summary>
    internal BuildFacts Identity(BuildFacts build)
    {
        var raw=ownedFeats.NormalizeIdentities(build);
        // An imported class name may differ in case from the catalog's.
        if(catalog.Class(raw.ClassName) is null&&catalog.ClassEntries.FirstOrDefault(e=>e.Name.Equals(raw.ClassName,StringComparison.OrdinalIgnoreCase)) is {} entry)raw=raw with{ClassName=entry.Name};
        var cls=catalog.Class(raw.ClassName);
        if(cls is null)return raw;
        var sub=cls.Subclasses.FirstOrDefault(s=>s.Name==raw.Subclass||s.ShortName==raw.Subclass);
        return raw with{Subclass=raw.Level>=3&&sub is not null?sub.Name:""};
    }

    private NativeBuildSummary Derived(BuildFacts build,IReadOnlyList<EquipmentItem> equipment)
    {
        var summary=builds.Derive(build,equipment,true);
        var manual=(build.ManualFacts?.WeaponProficiencies??[]).Select(name=>new ChoiceProficiencySource("manual:weapon:"+name,ProficiencySourceKind.Weapon,name));
        return summary with{ProficiencySources=summary.ProficiencySources.Concat(manual).Distinct().ToArray()};
    }

    /// <summary>The builder choices of a draft. <paramref name="found"/> carries what this draft's own calculation found.</summary>
    internal CreationChoiceReport Describe(DraftCharacter character,bool hasInventory,FoundValues found)
    {
        var build=character.Build;
        if(build.ClassTracks is not null||build.ClassLevels is {Count:>1})return DescribeTracks(character,hasInventory,found);
        var creationContext=new CreationChoiceContext{Build=build,HasInventory=hasInventory};
        if(catalog.Class(build.ClassName) is null)return creation.Describe(creationContext);
        var calculation=Identity(build);var summary=Derived(calculation,character.Equipment);
        var classContext=ClassChoiceContextFactory.FromBuild(calculation,summary,found.GrantRecords,found.Expertise);
        var report=creation.Describe(creationContext with{ClassContext=classContext,EligibleWeaponMasteries=weapons.EligibleClassMasteries(summary.ClassName,WeaponProficiencies(summary))});
        var issues=report.Issues.Concat(features.Derive(calculation,summary,classContext).Issues).ToList();
        if(build.ClassLevels is {Count:>0} levels)
        {
            if(levels.Count>1||levels.Any(p=>!p.Key.Equals(summary.ClassName,StringComparison.OrdinalIgnoreCase)))issues.Add(new("unsupported_multiclass",["class_levels"],"This version supports one class; multiclass progression needs review in the original sheet."));
            else if(levels.Any(p=>p.Value!=build.Level))issues.Add(new("inconsistent_class_level",["class_levels"],"The class level must match the character level."));
        }
        var context=FeatContext(character,hasInventory,found);issues.AddRange(ownedFeats.Validate(build,context));
        var definitions=report.Definitions.Concat(ownedFeats.DescribeChoices(build,context).Where(f=>f.Fields.Count>0).Select(f=>
        {
            var value=ChoiceValue.Record([new("mode",ChoiceValue.Of("feat")),new("feat_id",ChoiceValue.Of(f.FeatId)),new("feat_choices",ChoiceValue.Record([]))]);
            foreach(var field in f.Fields)if(field.Path.Count>0)value=value.With(field.Path,field.PreservedSelection??new());
            var option=new BuildChoiceOption{Value=ChoiceValue.Of(f.FeatId),Label=f.Name,Children=f.Fields};
            return new BuilderChoiceDefinition{Id="existing_feat:"+f.InstanceId,Label=f.Name+" choices",Section="feats",Mode=BuildChoiceMode.AsiOrFeat,Path=[],PreservedSelection=value,Status=f.Issues.Any(i=>i.Severity==BuildIssueSeverity.Error)?BuildChoiceStatus.Unresolved:BuildChoiceStatus.Resolved,Issues=f.Issues,Details=new(){UiType="feat_selection",FeatOptions=[option]}};
        })).ToArray();
        return AdditionalClassReport(character.Build,character.Scope,report with{Definitions=definitions,Issues=issues.ToArray()});
    }

    /// <summary>What the feat rules need to know about a draft: its levels, scores, owned features and the sources of what it owns.</summary>
    internal FeatRuleContext FeatContext(DraftCharacter character,bool hasInventory,FoundValues found)
    {
        var build=character.Build;
        if(build.ClassTracks is not null)return FeatContextTracks(character,hasInventory,found);
        var calculation=Identity(build);var summary=Derived(calculation,character.Equipment);var profile=ClassProgressionRules.AtLevel(classes,summary.ClassName,build.Level,summary.SubclassName);
        var sourceLevels=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var source in summary.ProficiencySources)
            if(new[]{"class:","background:","species:","origin:"}.Any(prefix=>source.SourceId.StartsWith(prefix,StringComparison.Ordinal))||source.SourceId is "class_options:class_skills" or "class_options:tool_proficiencies" or "species_options:skill_proficiencies" or "background_options:tool_proficiencies")sourceLevels[source.SourceId]=1;
            else if(source.SourceId.StartsWith("subclass:",StringComparison.Ordinal))sourceLevels[source.SourceId]=3;
        var classChoices=creation.Describe(new CreationChoiceContext{Build=calculation,HasInventory=hasInventory,ClassContext=ClassChoiceContextFactory.FromBuild(calculation,summary,found.GrantRecords,found.Expertise),EligibleWeaponMasteries=weapons.EligibleClassMasteries(summary.ClassName,WeaponProficiencies(summary))});
        foreach(var definition in classChoices.Definitions)
            if(definition.Details.SourceId is {Length:>0} id&&definition.Details.SourceLevel is >0 and <=20)sourceLevels[id]=definition.Details.SourceLevel.Value;
        for(var level=1;level<=profile.Level;level++)foreach(var feature in ClassProgressionRules.AtLevel(classes,summary.ClassName,level,level>=3?summary.SubclassName:"").FeaturesAtLevel)sourceLevels.TryAdd("class_feature:"+feature,level);
        foreach(var feat in build.Feats??[])
        {var taken=feat.LevelTaken??(feat.Source.Type=="class_level"?feat.Source.Level??0:0);if(taken is >=1 and <=20&&taken<=profile.Level)sourceLevels["feat:"+feat.InstanceId]=taken;}
        static string FeatId(string name)=>System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(),"[^a-z0-9]+","_").Trim('_')+"_xphb";
        var background=catalog.Background(build.Background);var species=catalog.SpeciesNamed(build.Species);
        var owned=(build.Feats??[]).Select(f=>new OwnedFeatSelection("feat:"+f.InstanceId,f.FeatId,f.Name,f.LevelTaken??f.Source.Level??1,f.Choices)).ToList();
        foreach(var origin in NativeOriginFeatInstances.Derive(build,background,species).Where(o=>!o.GeneralFeat)){owned.Add(new(origin.SourceId,FeatId(origin.Name),origin.Name,1,origin.Choices));sourceLevels[origin.SourceId]=1;}
        foreach(var style in FightingStyleRules.Sources(build).Where(o=>!o.SourceId.StartsWith("feat:",StringComparison.Ordinal))){owned.Add(new(style.SourceId,FeatId(style.Name),style.Name,style.SourceId.Contains("champion",StringComparison.Ordinal)?7:summary.ClassName=="Fighter"?1:2,new Dictionary<string,ChoiceValue>()));sourceLevels[style.SourceId]=owned[^1].LevelTaken;}
        return new(){Level=character.Scope?.TotalLevel??build.Level,Scores=summary.Scores.Scores.ToDictionary(p=>p.Key.ToString(),p=>p.Value),HasSpellcastingFeature=profile.Spellcasting is not null,Features=found.Features,ProficiencySources=summary.ProficiencySources,SourceLevels=sourceLevels,EligibleWeaponMasteries=weapons.EligibleMasteries(WeaponProficiencies(summary)),ExistingWeaponMasteries=build.WeaponMasteries??[],
            OwnedFeats=owned.ToArray()};
    }

    private CreationChoiceReport AdditionalClassReport(BuildFacts build,DraftScope? scope,CreationChoiceReport report)
    {
        if(scope?.AdditionalClass!=true)return report;
        var cls=build.ClassName;var definitions=new List<BuilderChoiceDefinition>();
        foreach(var d in report.Definitions)
        {
            if(d.Path.SequenceEqual(new[]{"class_options","class_skills"}))
            {
                var count=MulticlassRules.EntrySkills(cls);if(count==0)continue;
                var selected=ChoiceReading.Names(d.PreservedSelection).ToArray();var valid=selected.Length==count&&selected.Distinct().Count()==count&&selected.All(s=>d.Options.Any(o=>o.Value.Text==s&&!o.Disabled));
                definitions.Add(d with{Choose=count,Status=valid?BuildChoiceStatus.Resolved:BuildChoiceStatus.Unresolved,Issues=valid?[]:[new("missing_multiclass_skill",d.Path,"Choose one eligible additional-class skill.")]});
            }
            else if(d.Path.SequenceEqual(new[]{"class_options","tool_proficiencies"}))
            {
                if(cls!="Bard")continue;var selected=ChoiceReading.Names(d.PreservedSelection).ToArray();var valid=selected.Length==1&&d.Options.Any(o=>o.Value.Text==selected[0]&&!o.Disabled);
                definitions.Add(d with{Choose=1,Status=valid?BuildChoiceStatus.Resolved:BuildChoiceStatus.Unresolved,Issues=valid?[]:[new("missing_multiclass_tool",d.Path,"Choose one musical instrument for Bard entry training.")]});
            }
            else if(!d.Path.SequenceEqual(new[]{"class_options","equipment_package"}))definitions.Add(d);
        }
        return report with{Definitions=definitions,Issues=report.Issues.Where(i=>!i.Path.SequenceEqual(new[]{"class_options","class_skills"})&&!i.Path.SequenceEqual(new[]{"class_options","tool_proficiencies"})).Concat(definitions.Where(d=>d.Path.SequenceEqual(new[]{"class_options","class_skills"})||d.Path.SequenceEqual(new[]{"class_options","tool_proficiencies"})).SelectMany(d=>d.Issues)).ToArray()};
    }

    private CreationChoiceReport DescribeTracks(DraftCharacter character,bool hasInventory,FoundValues found)
    {
        var review=ClassTrackRules.Read(character.Build);var issues=review.Issues.ToList();var definitions=new List<BuilderChoiceDefinition>();if(!review.CanCalculate)return new(definitions,issues);
        for(var i=0;i<review.Tracks.Count;i++)
        {
            var track=review.Tracks[i];if(catalog.Class(track.ClassName) is null){issues.Add(new("unknown_class",["folio_class_tracks",i.ToString()],"Choose a supported 2024 class."));continue;}
            var initial=track.Id==review.InitialTrackId;var report=Describe(TrackScope(character,track,initial),hasInventory,found);
            foreach(var d in report.Definitions)
            {
                var owned=d.Path.FirstOrDefault() is "class_options" or "spells" or "weapon_masteries" or "subclass";
                if(!initial&&!owned)continue;
                var path=owned?new[]{"folio_class_tracks",i.ToString()}.Concat(d.Path[0]=="subclass"?new[]{"subclass"}:new[]{"choices"}.Concat(d.Path)).ToArray():d.Path.ToArray();
                definitions.Add(d with{Id=track.Id+"--"+d.Id,Label=owned?track.ClassName+": "+d.Label:d.Label,Path=path,Issues=d.Issues.Select(e=>e with{Path=path}).ToArray()});
            }
            issues.AddRange(report.Issues.Where(e=>initial||e.Path.FirstOrDefault() is "class_options" or "spells" or "weapon_masteries" or "subclass").Select(e=>e with{Path=new[]{"folio_class_tracks",i.ToString()}.Concat(e.Path).ToArray()}));
        }
        return new(definitions,issues);
    }

    private FeatRuleContext FeatContextTracks(DraftCharacter character,bool hasInventory,FoundValues found)
    {
        var review=ClassTrackRules.Read(character.Build);if(!review.CanCalculate)throw new ArgumentException("Resolve class track history before changing class feature choices.");
        var contexts=review.Tracks.Select(t=>(Track:t,Context:FeatContext(TrackScope(character,t,t.Id==review.InitialTrackId),hasInventory,found))).ToArray();var initial=contexts.Single(x=>x.Track.Id==review.InitialTrackId).Context;
        return initial with{Level=review.Tracks.Sum(t=>t.Level),Scores=Enum.GetNames<Ability>().ToDictionary(a=>a,a=>contexts.Max(x=>x.Context.Scores[a])),HasSpellcastingFeature=contexts.Any(x=>x.Context.HasSpellcastingFeature),Features=contexts.SelectMany(x=>x.Context.Features).Distinct().ToArray(),SourceLevels=contexts.SelectMany(x=>x.Context.SourceLevels).GroupBy(p=>p.Key).ToDictionary(g=>g.Key,g=>g.Min(p=>p.Value)),OwnedFeats=contexts.SelectMany(x=>x.Context.OwnedFeats).DistinctBy(f=>f.SourceId).ToArray(),ProficiencySources=contexts.SelectMany(x=>x.Context.ProficiencySources.Where(p=>x.Track.Id==review.InitialTrackId||!(p.Kind==ProficiencySourceKind.Save&&p.SourceId=="class:"+x.Track.ClassName))).Distinct().ToArray()};
    }

    /// <summary>One class track of a multiclass draft as a single-class draft of that track's level.</summary>
    private static DraftCharacter TrackScope(DraftCharacter character,ClassTrack track,bool initial)
        =>new(ClassTrackRules.Scope(character.Build,track,initial),character.Equipment,new(character.Scope?.TotalLevel??character.Build.Level,!initial));
}
