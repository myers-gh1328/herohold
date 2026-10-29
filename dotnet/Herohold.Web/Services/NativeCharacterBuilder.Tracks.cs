using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Rules;
namespace Herohold.Web.Services;

public sealed partial class NativeCharacterBuilder
{
    /// <summary>
    /// A multiclass draft: each class track is calculated as its own single-class character, then merged. Scores and
    /// origin facts come from the initial track; proficiencies, features, resources, spells and Hit Dice from every track.
    /// </summary>
    private CharacterCalculation CalculateTracks(CharacterDraft draft,CharacterState? source,bool addGainedHp)
    {
        var review=ClassTrackRules.Read(draft.Build);
        // Without items of its own, a multiclass draft starts with its initial class's and background's equipment.
        var initialClass=review.Tracks.FirstOrDefault(t=>t.Id==review.InitialTrackId)?.ClassName??draft.Build.ClassName;
        var gear=Gear(draft,draft.Build with{ClassName=initialClass},catalog.Class(initialClass)).Gear;
        CharacterCalculation Refused(IReadOnlyList<BuildIssue> issues)
        {
            var choices=Describe(new DraftCharacter(draft.Build,gear),draft.Inventory is not null,FoundValues.None);
            var errors=choices.Issues.Concat(issues).Where(i=>i.Severity==BuildIssueSeverity.Error).ToArray();
            return new(){Id=draft.Id,Build=draft.Build,Equipment=gear,OwnsInventory=draft.Inventory is not null,Notes=draft.Notes,CurrentHp=draft.Facts?.CurrentHp??0,TemporaryHp=draft.Facts?.TemporaryHp??0,Issues=issues,Choices=choices.Definitions,
                Validation=new(){Complete=errors.Length==0,Missing=[..errors.Select(e=>e.Message).Distinct(StringComparer.Ordinal)],Warnings=[..choices.Issues.Where(i=>i.Severity!=BuildIssueSeverity.Error).Select(i=>i.Message).Distinct(StringComparer.Ordinal)]}};
        }
        if(!review.CanCalculate)return Refused(review.Issues);
        var total=review.Tracks.Sum(t=>t.Level);
        var resolved=new List<(ClassTrack Track,CharacterCalculation Result,CharacterState State)>();
        foreach(var track in review.Tracks)
        {
            if(catalog.Class(track.ClassName) is null)return Refused([new BuildIssue("unknown_class",["folio_class_tracks"],"A class track is not in the 2024 catalog.")]);
            var initialTrack=track.Id==review.InitialTrackId;
            var scope=new CharacterDraft{Id=draft.Id,Build=ClassTrackRules.Scope(draft.Build,track,initialTrack),Inventory=[..gear],IncludeDefaultEquipment=false};
            var result=Calculate(scope,null,false,new(draft.Build.Level,!initialTrack));resolved.Add((track,result,WorkspaceProjection.FromCalculation(result)));
        }
        var initial=resolved.Single(x=>x.Track.Id==review.InitialTrackId);var additional=resolved.Where(x=>x.Track.Id!=review.InitialTrackId).ToArray();
        // Global scores and origin content are calculated once. Class lists merge facts, never numerical outputs from every track.
        var saveSet=initial.State.SaveProficiencies.ToHashSet();var skillSet=initial.State.SkillProficiencies.ToHashSet();var expertise=initial.State.Expertise.ToHashSet();
        foreach(var item in additional)
        {
            var sourceFacts=Derived(ClassTrackRules.Scope(draft.Build,item.Track,false),gear,new()).ProficiencySources;saveSet.UnionWith(sourceFacts.Where(p=>p.Kind==ProficiencySourceKind.Save&&p.SourceId!="class:"+item.Track.ClassName).Select(p=>Enum.Parse<Ability>(p.Target)));
            var initialSkills=ChoiceReading.Names(item.Track.Choices.Get("class_options")?.Fields.Get("class_skills")).ToHashSet();skillSet.UnionWith(item.State.SkillProficiencies.Where(s=>!initialSkills.Contains(s)));if(MulticlassRules.EntrySkills(item.Track.ClassName)>0)skillSet.UnionWith(initialSkills);expertise.UnionWith(item.State.Expertise);
        }
        IReadOnlyList<string> Own(string cls,string field)=>catalog.Class(cls) is {} record?field=="armor_proficiencies"?record.ArmorProficiencies:record.WeaponProficiencies:throw new InvalidOperationException("A class track is not in the catalog.");
        IEnumerable<string> Entry(string cls,string field)=>field=="armor_proficiencies"?MulticlassRules.EntryArmor(cls):MulticlassRules.EntryWeapons(cls);
        var values=new Dictionary<string,IReadOnlyList<string>>();
        foreach(var field in new[]{"damage_resistances","damage_immunities","damage_vulnerabilities","condition_immunities","tool_proficiencies"})values[field]=resolved.SelectMany(x=>x.Result.Facts.GetValueOrDefault(field)??[]).Distinct(StringComparer.Ordinal).ToArray();
        foreach(var field in new[]{"armor_proficiencies","weapon_proficiencies"})values[field]=(initial.Result.Facts.GetValueOrDefault(field)??[]).Concat(additional.SelectMany(x=>Entry(x.Track.ClassName,field).Concat((x.Result.Facts.GetValueOrDefault(field)??[]).Except(Own(x.Track.ClassName,field))))).Distinct(StringComparer.Ordinal).ToArray();
        var derivedFacts=new Dictionary<string,IReadOnlyList<string>>();
        foreach(var field in BuildFactOwnershipRules.Fields)
        {
            var facts=new List<string>();
            foreach(var item in resolved)
            {
                IEnumerable<string> own=item.Result.DerivedFacts.GetValueOrDefault(field)??[];
                if(item.Track.Id!=review.InitialTrackId&&field is "armor_proficiencies" or "weapon_proficiencies")own=own.Except(Own(item.Track.ClassName,field)).Concat(Entry(item.Track.ClassName,field));
                facts.AddRange(own);
            }
            derivedFacts[field]=facts.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        // The initial class's record says what was entered by hand; the merged lists are what the character shows.
        var shown=new ShownOwnership{Shown=values.Where(p=>BuildFactOwnershipRules.Fields.Contains(p.Key)).ToDictionary(),
            PriorManual=ShownOwnership.Lists(initial.Result.Build.ManualFacts),PriorDerived=initial.Result.Build.ManualFacts is null?new Dictionary<string,IReadOnlyList<string>>():initial.Result.DerivedFacts};
        var owners=BuildFactOwnershipRules.Derive(shown,derivedFacts);
        var initialBuild=initial.Result.Build;
        var build=draft.Build with
        {
            ClassName=initial.Track.ClassName,Subclass=initial.Track.SubclassName,
            ManualFacts=new(){ToolProficiencies=[..owners.Manual["tool_proficiencies"]],ArmorProficiencies=[..owners.Manual["armor_proficiencies"]],WeaponProficiencies=[..owners.Manual["weapon_proficiencies"]],
                DamageResistances=[..owners.Manual["damage_resistances"]],DamageImmunities=[..owners.Manual["damage_immunities"]],DamageVulnerabilities=[..owners.Manual["damage_vulnerabilities"]],ConditionImmunities=[..owners.Manual["condition_immunities"]]},
            ManualLanguages=initialBuild.ManualLanguages,ManualSkills=initialBuild.ManualSkills,ManualExpertise=initialBuild.ManualExpertise,ManualSaves=initialBuild.ManualSaves,
            WeaponMasteries=[..review.Tracks.SelectMany(t=>ChoiceReading.Names(t.Choices.Get("weapon_masteries"))).Distinct(StringComparer.Ordinal)]
        };
        var features=resolved.SelectMany(x=>x.State.Features).Distinct(StringComparer.Ordinal).ToArray();
        var aggregateScores=Enum.GetValues<Ability>().ToDictionary(a=>a,a=>resolved.Max(x=>x.State.AbilityScores[a]));
        var state=initial.State with{AbilityScores=aggregateScores,Level=total,Build=build,SaveProficiencies=[..saveSet],SkillProficiencies=[..skillSet],Expertise=[..expertise],StandingAdjustments=new(),StandingAdjustmentTerms=new(),Exhaustion=0};
        var standingTerms=new Dictionary<string,List<CalculationTerm>>(StringComparer.Ordinal);
        foreach(var item in resolved)foreach(var pair in item.State.StandingAdjustmentTerms)
        {
            var terms=pair.Value.Where(t=>!t.Label.StartsWith("Jack of All Trades")||!skillSet.Contains(pair.Key.Replace("skill:",""))).Select(t=>t.Label.StartsWith("Alert")?t with{Value=CharacterCalculations.ProficiencyBonus(total)}:t.Label.StartsWith("Jack of All Trades")?t with{Value=CharacterCalculations.ProficiencyBonus(total)/2}:t).ToArray();
            if(!standingTerms.TryGetValue(pair.Key,out var list)){list=[];standingTerms[pair.Key]=list;}foreach(var term in terms)if(!list.Any(t=>t.Label==term.Label))list.Add(term);
        }
        standingTerms=standingTerms.Where(p=>p.Value.Count>0).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);state=state with{StandingAdjustmentTerms=standingTerms,StandingAdjustments=standingTerms.ToDictionary(p=>p.Key,p=>p.Value.Sum(t=>t.Value))};
        var modifiers=Enum.GetValues<Ability>().ToDictionary(a=>a,a=>CharacterCalculations.Ability(state,a).Total);
        // HP: the initial class's Hit Die at the first level, every track's gains after it, around the character this edits.
        var con=CharacterCalculations.Ability(state,Ability.Constitution).Total;
        var hpBuild=build;
        var hp=HitPointRules.Calculate(HitPointRules.FromBuild(hpBuild,MulticlassRules.HitDie(initial.Track.ClassName),con,initial.Result.OriginFeats,initial.Result.SpeciesTraits,draft.Facts?.CurrentHp??0,draft.Facts?.TemporaryHp??0));
        var current=source?.CurrentHp??hp.CurrentHp;
        if(source is not null&&(addGainedHp||total>source.Level))
        {
            var sourceBuild=WorkspaceProjection.BuildOf(source);var sourceClass=ClassProgressionRules.AtLevel(classes,source.ClassName,source.Level,source.Subclass);
            var before=HitPointRules.Calculate(HitPointRules.FromBuild(sourceBuild,sourceClass.HitDie,CharacterCalculations.Ability(source,Ability.Constitution).Total,[],[],source.CurrentHp,source.TemporaryHp));
            var gained=hp.Maximum-before.Maximum;if(addGainedHp)current+=Math.Max(0,gained);
            if(total==source.Level+1)
            {
                if(build.LevelHistory is {Count:>0} levels&&levels[^1].ToLevel==total)build=build with{LevelHistory=[..levels.Take(levels.Count-1),levels[^1] with{HpIncrease=gained}]};
                if(build.HpRolls is {Count:>0} rolls&&rolls[^1].Level==total)build=build with{HpRolls=[..rolls.Take(rolls.Count-1),rolls[^1] with{Value=gained}]};
            }
        }
        build=build with{HpHistory=History(hp)};
        var movementTerms=resolved.SelectMany(x=>x.Result.SpeedTerms).GroupBy(t=>t.Name).Select(g=>g.First()).ToArray();var movementBase=initial.Result.BaseSpeed;
        var derived=new Dictionary<string,ChoiceValue>(StringComparer.Ordinal);var classResources=new List<KeyValuePair<string,ChoiceValue>>();
        foreach(var item in resolved)
        {
            classResources.Add(KeyValuePair.Create(item.Track.Id,ChoiceValue.Record(item.Result.DerivedResources)));
            foreach(var pair in item.Result.DerivedResources)if(pair.Key is not ("spell_slots" or "proficiency_bonus")&&!derived.ContainsKey(pair.Key))derived[pair.Key]=pair.Value;
        }
        derived["proficiency_bonus"]=ChoiceValue.Of(CharacterCalculations.ProficiencyBonus(total));derived["class_resources"]=ChoiceValue.Record(classResources);
        var resourcesOut=new List<ResourceDefinition>();
        foreach(var item in resolved)foreach(var pool in item.State.Resources)
        {
            if(pool.SlotLevel is not null||pool.Id.StartsWith("slot",StringComparison.Ordinal)||pool.Id=="pact"||pool.Id=="hitdice")continue;
            if(!resourcesOut.Any(p=>p.Id==pool.Id))resourcesOut.Add(new(pool.Id,pool.Name,pool.Maximum,pool.Recovery==RecoveryRest.Short?"short_or_long_rest":pool.Recovery==RecoveryRest.Long?"long_rest":"manual",null,pool.SourceKind=="grant"?pool.SourceId:item.Track.Id));
        }
        var castingTracks=resolved.Where(x=>ClassProgressionRules.AtLevel(classes,x.Track.ClassName,x.Track.Level,x.Track.SubclassName).Spellcasting is {CastingType:not "pact"}).ToArray();
        var slots=castingTracks.Length>1?ClassProgressionRules.AtLevel(classes,"Wizard",MulticlassRules.SpellcastingLevel(review.Tracks)).Spellcasting?.Slots:castingTracks.FirstOrDefault().Track is {} only?ClassProgressionRules.AtLevel(classes,only.ClassName,only.Level,only.SubclassName).Spellcasting?.Slots:null;
        if(slots is not null)foreach(var pair in slots.Where(p=>p.Value>0))resourcesOut.Add(new("slot"+pair.Key,"Level "+pair.Key+" slots",pair.Value,"long_rest",pair.Key));
        foreach(var pact in resolved.Where(x=>x.Track.ClassName=="Warlock"))foreach(var pool in pact.Result.Resources.Where(p=>p.SlotLevel is not null))resourcesOut.Add(pool with{SourceId=pact.Track.Id});
        var hitDiePools=review.Tracks.GroupBy(t=>MulticlassRules.HitDie(t.ClassName)).Select(g=>new HitDiePool{Id="hitdice-d"+g.Key,Sides=g.Key,Maximum=g.Sum(t=>t.Level),Remaining=g.Sum(t=>t.Level),ClassTrackIds=[..g.Select(t=>t.Id)]}).ToList();
        var sourceStates=new List<SpellSourceState>();var owned=new List<SpellDefinition>();
        foreach(var item in resolved)
        {
            var ownClass=ClassProgressionRules.AtLevel(classes,item.Track.ClassName,item.Track.Level,item.Track.SubclassName).Spellcasting;if(ownClass is null)continue;
            var p=item.State.Play;string Id(string id)=>item.Track.Id==review.InitialTrackId?id:OwnedId(item.Track.Id,id);var ordinary=p.Spells.Where(s=>!p.GrantedSpellIds.Contains(s.Id)).ToArray();owned.AddRange(ordinary.Select(s=>s with{Id=Id(s.Id),CastingAbility=ownClass.Ability,CastingSource=item.Track.ClassName}));
            sourceStates.Add(new(){Id=item.Track.Id,ClassTrackId=item.Track.Id,Name=item.Track.ClassName,Ability=ownClass.Ability,Preparation=p.Preparation with{PreparedLimit=ownClass.PreparedLimit,MaximumSpellLevel=ownClass.AvailableSpellLevels.DefaultIfEmpty(0).Max(),EligibleSpellIds=[..p.Preparation.EligibleSpellIds.Select(Id)],OwnedCantripSpellIds=[..p.Preparation.OwnedCantripSpellIds.Select(Id)],EligibleCantripSpellIds=[..p.Preparation.EligibleCantripSpellIds.Select(Id)]},PreparedSpellIds=[..p.PreparedSpellIds.Select(Id)],KnownSpellIds=[..p.KnownSpellIds.Select(Id)],SpellbookIds=[..p.SpellbookIds.Select(Id)]});
        }
        // Stable grant provenance is global for species/feat instances and source-scoped for class features. Do not re-identify an existing grant from its display label.
        var grantOwners=new Dictionary<string,(string Kind,string Name,Ability Ability,string? Track,List<string> Ids)>();
        foreach(var item in resolved)
            foreach(var spell in item.State.Play.Spells.Where(s=>item.State.Play.GrantedSpellIds.Contains(s.Id)))
            {
                if(owned.Any(s=>s.Id==spell.Id))continue;owned.Add(spell);
                var identity=item.Result.GrantSources.FirstOrDefault(g=>OwnedGrantAlias(g.GrantId)==spell.Id);var sourceId=identity?.SourceId??"";var kind=identity?.SourceKind??"";if(sourceId.Length==0)sourceId=spell.Id;
                if(!grantOwners.TryGetValue(sourceId,out var owner))owner=(kind.Length>0?kind:"grant",identity?.SourceName is {Length:>0} display?display:spell.CastingSource,spell.CastingAbility??item.State.Play.SpellcastingAbility,kind.Contains("class",StringComparison.Ordinal)?item.Track.Id:null,[]);
                owner.Ids.Add(spell.Id);grantOwners[sourceId]=owner;
            }
        foreach(var pair in grantOwners)sourceStates.Add(new(){Id=pair.Key,ClassTrackId=pair.Value.Track,SourceKind=pair.Value.Kind,Name=pair.Value.Name,Ability=pair.Value.Ability,Preparation=new(){MaximumSpellLevel=owned.Where(s=>pair.Value.Ids.Contains(s.Id)).Max(s=>s.Level)},GrantedSpellIds=pair.Value.Ids});
        var bonus=CharacterCalculations.ProficiencyBonus(total);
        var monk=resolved.FirstOrDefault(x=>x.Track.ClassName=="Monk");var attackIdentity=monk.Track??initial.Track;var attackBuild=Derived(ClassTrackRules.Scope(draft.Build,attackIdentity,attackIdentity.Id==review.InitialTrackId),gear,new());
        var weaponSources=values["weapon_proficiencies"].Select(n=>new ChoiceProficiencySource("multiclass",ProficiencySourceKind.Weapon,n)).ToArray();
        attackBuild=attackBuild with{Standing=attackBuild.Standing with{Modifiers=Enum.GetValues<Ability>().ToDictionary(a=>a,a=>CharacterCalculations.Ability(state,a))},ProficiencySources=weaponSources};
        var attackFeatures=new ActiveFeatureSummary{OriginFeats=[..initial.Result.OriginFeats],Features=features,Proficiencies=weaponSources};
        var attackResources=new ClassResourceSummary{DerivedResources=(monk.Result??initial.Result).DerivedResources};
        var actions=new AttackActionRules(weapons).Derive(build,attackBuild,attackFeatures,attackResources,gear);
        var attackActionCount=review.Tracks.Max(t=>t.ClassName=="Fighter"?t.Level>=20?4:t.Level>=11?3:t.Level>=5?2:1:ClassProgressionRules.AtLevel(classes,t.ClassName,t.Level,t.SubclassName).FeaturesUpToLevel.Concat(ClassProgressionRules.AtLevel(classes,t.ClassName,t.Level,t.SubclassName).SubclassFeaturesUpToLevel).Any(n=>n.StartsWith("Extra Attack",StringComparison.Ordinal))?2:1);
        var found=new FoundValues(features,[..expertise],[]);
        var choices=Describe(new DraftCharacter(build,gear),draft.Inventory is not null,found);
        var issues=resolved.SelectMany(x=>x.Result.Issues).ToArray();
        var errors=choices.Issues.Concat(issues).Where(i=>i.Severity==BuildIssueSeverity.Error).ToArray();
        return new()
        {
            Id=draft.Id,Build=build,KnownClass=true,Equipment=gear,OwnsInventory=draft.Inventory is not null,Notes=draft.Notes,
            Scores=aggregateScores,Modifiers=modifiers,SaveBonuses=Enum.GetValues<Ability>().ToDictionary(a=>a,a=>CharacterCalculations.Save(state,a).Total),
            SkillBonuses=StandingRules.SkillAbilities.ToDictionary(p=>p.Key,p=>CharacterCalculations.Skill(state,p.Key,p.Value).Total,StringComparer.Ordinal),Initiative=CharacterCalculations.Initiative(state).Total,
            AdjustmentBonuses=new(initial.Result.AdjustmentBonuses),SaveProficiencies=[..saveSet],SkillProficiencies=[..skillSet],Expertise=[..expertise],Languages=[..initial.Result.Languages],StandingTerms=standingTerms,
            Features=features,SpeciesTraits=initial.Result.SpeciesTraits,OriginFeats=initial.Result.OriginFeats,Senses=initial.Result.Senses,Size=initial.Result.Size,
            FeatureDetails=[..resolved.SelectMany(x=>x.Result.FeatureDetails).DistinctBy(d=>System.Text.Json.JsonSerializer.Serialize(d))],
            Facts=owners.Values,DerivedFacts=owners.Derived,Issues=issues,MaximumHp=hp.Maximum,CurrentHp=Math.Clamp(current,0,hp.Maximum),TemporaryHp=source?.TemporaryHp??hp.TemporaryHp,HitDie=MulticlassRules.HitDie(initial.Track.ClassName),
            ArmorClass=resolved.Max(x=>x.State.ArmorClass),Armor=resolved.MaxBy(x=>x.State.ArmorClass).Result.Armor,Speed=movementBase+movementTerms.Where(t=>t.Applies).Sum(t=>t.Amount),BaseSpeed=movementBase,SpeedTerms=movementTerms,DerivedResources=derived,
            Resources=resourcesOut,Grants=[],GrantSources=[],GrantedSpellNames=initial.Result.GrantedSpellNames,Spellbook=initial.Result.Spellbook,Spells=initial.Result.Spells,SpellcastingAbility=initial.Result.SpellcastingAbility,Preparation=null,PreparedLimit=initial.Result.Preparation?.PreparedLimit??0,
            CastingSources=[..sourceStates.Select(s=>new SpellcastingSource{SourceName=s.Name,SourceType=s.SourceKind,Ability=s.Ability,SpellSaveDc=8+bonus+modifiers[s.Ability],SpellAttackBonus=bonus+modifiers[s.Ability]})],
            Attacks=[..actions.Attacks.Select(a=>Attack(a,a.Ability,0,modifiers[a.Ability]))],AttackActionCount=attackActionCount,
            SpellSources=sourceStates,OwnedSpells=[..owned.DistinctBy(s=>s.Id)],HitDiePools=hitDiePools,
            Choices=choices.Definitions,Validation=new(){Complete=errors.Length==0,Missing=[..errors.Select(e=>e.Message).Distinct(StringComparer.Ordinal)],Warnings=[..choices.Issues.Where(i=>i.Severity!=BuildIssueSeverity.Error).Select(i=>i.Message).Distinct(StringComparer.Ordinal)]}
        };
    }
}
