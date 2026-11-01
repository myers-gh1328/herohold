using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Herohold.Core;
using Herohold.Rules;
namespace Herohold.Web.Services;

public sealed partial class NativeCharacterBuilder
{
    private static readonly IReadOnlyDictionary<Ability,int> DefaultScores=Enum.GetValues<Ability>().ToDictionary(a=>a,_=>10);
    private static readonly string[] LongRestPreparers=["Cleric","Druid","Wizard","Paladin","Ranger"];

    /// <summary>
    /// Calculates a draft. <paramref name="source"/> is the character it edits, or the play facts it brings: its HP in play
    /// and its items. <paramref name="addGainedHp"/> adds the maximum HP a level-up gains to current HP.
    /// </summary>
    internal CharacterCalculation Calculate(CharacterDraft draft,CharacterState? source,bool addGainedHp,DraftScope? scope=null)
    {
        if(draft.Build.ClassTracks is not null||draft.Build.ClassLevels is {Count:>1})return CalculateTracks(draft,source,addGainedHp);
        var shownSubclass=draft.Build.Subclass;
        var build=Identity(draft.Build);var cls=catalog.Class(build.ClassName);
        var (gear,custom,ownsInventory,equipped)=Gear(draft,build,cls);build=equipped;
        if(cls is null)return Unknown(draft,build,gear,custom,ownsInventory,scope);
        var observed=new ObservedProficiencies{Skills=[],Expertise=[],Saves=[],Languages=["Common"],Scores=DefaultScores};
        // The first pass: features, the facts they own, standing, HP and speed, as the rules find them in order.
        var first=builds.Derive(build,gear,true,observed);
        var active=features.Derive(build,first,ClassChoiceContextFactory.FromBuild(build,first,[],[]));
        build=build with{FeatureConfigurations=Configurations(active.FeatureConfigurations)};
        var owned=Ownership(build.ManualFacts,DerivedFacts(active));
        build=build with{ManualFacts=new(){ToolProficiencies=[..owned.Manual["tool_proficiencies"]],ArmorProficiencies=[..owned.Manual["armor_proficiencies"]],WeaponProficiencies=[..owned.Manual["weapon_proficiencies"]],
            DamageResistances=[..owned.Manual["damage_resistances"]],DamageImmunities=[..owned.Manual["damage_immunities"]],DamageVulnerabilities=[..owned.Manual["damage_vulnerabilities"]],ConditionImmunities=[..owned.Manual["condition_immunities"]]}};
        var activeLanguages=active.Proficiencies.Where(s=>s.Kind==ProficiencySourceKind.Language).Select(s=>s.Target).Distinct(StringComparer.Ordinal).ToArray();
        var standing=builds.Derive(build,gear,true,observed with{Languages=activeLanguages});
        build=Owned(build,standing);
        var progression=ClassProgressionRules.AtLevel(classes,standing.ClassName,build.Level,standing.SubclassName);
        var startHp=draft.Facts?.CurrentHp??0;var startTemporary=draft.Facts?.TemporaryHp??0;
        var firstHp=HitPointRules.Calculate(HitPointRules.FromBuild(build,progression.HitDie,standing.Standing.Modifiers[Ability.Constitution].Total,active.OriginFeats,active.SpeciesTraits,startHp,startTemporary));
        build=build with{HpHistory=History(firstHp)};
        var shown=Shown(standing);
        var firstMovement=Movement(build,standing,progression,gear);
        // The second pass: what reads the first pass back (manual weapon training, spell grants, resources, attacks, spells).
        var settled=Derived(build,gear,shown);
        var spellGrants=grants.Derive(build,settled.Standing.Modifiers.ToDictionary(p=>p.Key,p=>p.Value.Total),[]);
        build=build with{SpeciesOptions=new(build.SpeciesOptions??[],StringComparer.Ordinal){["lineage_spells"]=ChoiceValue.ListOf(spellGrants.SpeciesSpells.Distinct(StringComparer.Ordinal).Select(ChoiceValue.Of))}};
        var pools=resources.Derive(build,settled,active,firstMovement.Speed);
        var actions=new AttackActionRules(weapons).Derive(build,settled,active,pools,gear);
        var profile=ClassProgressionRules.AtLevel(classes,settled.ClassName,build.Level,settled.SubclassName);var casting=profile.Spellcasting;var maximum=casting?.AvailableSpellLevels.DefaultIfEmpty(0).Max()??0;
        var picks=build.Spells;var selected=(picks?.Cantrips??[]).Concat(picks?.Prepared??[]).Concat(picks?.Known??[]).Concat(picks?.Spellbook??[]).Where(n=>n.Length>0).Concat(spellGrants.GrantedSpellNames).Concat(spellGrants.SpeciesSpells).ToHashSet(StringComparer.Ordinal);
        var eligible=catalog.SpellRecords.Where(s=>selected.Contains(s.Name)||casting is not null&&s.Classes.Contains(casting.SpellList)&&(s.Level>=1&&s.Level<=maximum||settled.ClassName=="Wizard"&&s.Level==0)).ToArray();
        var castingSources=new List<(string Name,string Kind,Ability Ability)>();
        void CastingSource(string name,string kind,Ability ability){if(!castingSources.Any(s=>s.Name==name&&s.Ability==ability))castingSources.Add((name,kind,ability));}
        if(casting is not null)CastingSource(settled.ClassName,"class",casting.Ability);foreach(var s in spellGrants.Sources)CastingSource(s.SourceName,s.SourceKind,s.Ability);
        var wizard=settled.ClassName=="Wizard";
        var preparation=new SpellPreparation{CanChangeCantripsOnLongRest=wizard,CantripReplacementLimit=wizard?1:0,OwnedCantrips=wizard?[..(picks?.Cantrips??[]).Where(n=>n.Length>0).Distinct(StringComparer.Ordinal)]:[],
            EligibleCantrips=[..catalog.SpellRecords.Where(s=>wizard&&s.Classes.Contains("Wizard")&&s.Level==0).Select(s=>s.Name).Distinct(StringComparer.Ordinal)],
            CanChangeOnLongRest=LongRestPreparers.Contains(settled.ClassName),PreparedLimit=casting?.PreparedLimit??0,ReplacementLimit=settled.ClassName is "Ranger" or "Paladin"?1:null,
            EligibleSpells=[..catalog.SpellRecords.Where(s=>casting is not null&&s.Classes.Contains(casting.SpellList)&&s.Level>=1&&s.Level<=maximum).Select(s=>s.Name).Distinct(StringComparer.Ordinal)]};
        var found=new FoundValues(active.Features,standing.Standing.Expertise,spellGrants.Records);
        var book=Describe(new DraftCharacter(build,gear,scope),ownsInventory,found).Definitions.FirstOrDefault(d=>d.Id=="spells_spellbook")?.Details.EffectiveSelection is {} effective?ChoiceReading.Names(effective).ToList():picks?.Spellbook??[];
        build=build with{SpellbookSpells=[..book],Subclass=shownSubclass};
        var choices=Describe(new DraftCharacter(build,gear,scope),ownsInventory,found);
        var warnings=spellGrants.Warnings.Concat(owned.Warnings).Concat(settled.Issues).Concat(actions.Warnings).Distinct().ToArray();
        var errors=choices.Issues.Concat(active.Issues).Where(i=>i.Severity==BuildIssueSeverity.Error).ToArray();
        var validation=new WorkflowValidation{Complete=errors.Length==0,Missing=[..errors.Select(e=>e.Message).Distinct(StringComparer.Ordinal)],
            Warnings=[..warnings.Concat(choices.Issues.Where(i=>i.Severity!=BuildIssueSeverity.Error).Select(i=>i.Message)).Distinct(StringComparer.Ordinal)]};
        // The character's own standing, HP and defenses, around the character it edits.
        var items=source?.Equipment??gear;
        var final=builds.Derive(build,items,!validation.Complete,shown);
        var dependent=builds.DependentResources(build,final.Standing);
        build=Owned(build,final);
        var attacks=Rescored(actions.Attacks,standing,final);
        var levelBonus=CharacterCalculations.ProficiencyBonus(build.Level);
        var hp=Hp(build,final,active,source,addGainedHp,Math.Clamp(firstHp.CurrentHp,0,firstHp.Maximum),firstHp.TemporaryHp,out var hitDie,out var finalProgression);
        build=hp.Build;
        var defenses=Defenses(build,final,finalProgression,items);
        var derived=new Dictionary<string,ChoiceValue>(pools.DerivedResources,StringComparer.Ordinal);foreach(var pair in dependent.Values)derived[pair.Key]=ChoiceValue.Of(pair.Value);
        derived["spell_slots"]=ChoiceValue.Record(finalProgression.Spellcasting?.Slots.Where(p=>p.Value>0).Select(p=>KeyValuePair.Create(p.Key.ToString(),ChoiceValue.Of(p.Value)))??[]);
        if(defenses.Movement.ClimbSpeed is {} climb)derived["climb_speed"]=ChoiceValue.Of(climb);
        if(defenses.Movement.SwimSpeed is {} swim)derived["swim_speed"]=ChoiceValue.Of(swim);
        var finalCasting=finalProgression.Spellcasting;
        return new()
        {
            Id=draft.Id,Build=build,KnownClass=true,Equipment=gear,OwnsInventory=ownsInventory,CustomEquipment=custom,Notes=draft.Notes,
            Scores=new(final.Scores.Scores),Modifiers=final.Standing.Modifiers.ToDictionary(p=>p.Key,p=>p.Value.Total),SaveBonuses=final.Standing.Saves.ToDictionary(p=>p.Key,p=>p.Value.Total),
            SkillBonuses=final.Standing.Skills.ToDictionary(p=>p.Key,p=>p.Value.Total,StringComparer.Ordinal),Initiative=final.Standing.Initiative.Total,AdjustmentBonuses=new(final.Scores.SourceBonuses),
            SaveProficiencies=[..final.Standing.SaveProficiencies],SkillProficiencies=[..final.Standing.SkillProficiencies],Expertise=[..final.Standing.Expertise],Languages=[..final.Languages.Languages],StandingTerms=Terms(final),
            Features=active.Features,SpeciesTraits=active.SpeciesTraits,OriginFeats=active.OriginFeats,Senses=active.Senses,Size=active.Size,
            FeatureDetails=[..active.Details.Select(d=>new FeatureDetail{Name=d.Name,Description=d.Description,Source=d.SourceId,RelatedFeatures=[..d.RelatedFeatures.Select(r=>new RelatedFeature{Name=r.Name,Cost=r.Cost,Description=r.Description})]}).Where(d=>d.Name.Length>0)],
            Facts=owned.Values,DerivedFacts=owned.Derived,Issues=active.Issues,MaximumHp=hp.Maximum,CurrentHp=hp.Current,TemporaryHp=hp.Temporary,HitDie=hitDie,
            ArmorClass=defenses.ArmorClass,Armor=defenses.Detail,Speed=defenses.Movement.Speed,BaseSpeed=SpeciesSpeed(build.Species),SpeedTerms=defenses.Movement.Terms,DerivedResources=derived,
            Resources=Pools(pools.Metadata,dependent.Pools,finalCasting),Grants=spellGrants.Metadata,GrantSources=spellGrants.Sources,GrantedSpellNames=spellGrants.GrantedSpellNames,Spellbook=book,
            Spells=[..eligible.Select(s=>WorkspaceProjection.MapSpell(s,build.ClassName))],SpellcastingAbility=finalCasting?.Ability,
            Preparation=finalCasting is null?preparation:preparation with{PreparedLimit=finalCasting.PreparedLimit,CanChangeOnLongRest=LongRestPreparers.Contains(finalProgression.ClassName)},
            CastingSources=[..castingSources.Select(s=>new SpellcastingSource{SourceName=s.Name,SourceType=s.Kind,Ability=s.Ability,SpellSaveDc=8+levelBonus+final.Standing.Modifiers[s.Ability].Total,SpellAttackBonus=levelBonus+final.Standing.Modifiers[s.Ability].Total})],
            Attacks=attacks,Choices=choices.Definitions,Validation=validation
        };
    }

    /// <summary>A draft whose class the 2024 catalog does not offer yet: its build and choices only.</summary>
    private CharacterCalculation Unknown(CharacterDraft draft,BuildFacts build,IReadOnlyList<EquipmentItem> gear,List<string> custom,bool ownsInventory,DraftScope? scope)
    {
        var choices=Describe(new DraftCharacter(build with{Subclass=draft.Build.Subclass},gear,scope),ownsInventory,FoundValues.None);
        var errors=choices.Issues.Where(i=>i.Severity==BuildIssueSeverity.Error).ToArray();
        return new(){Id=draft.Id,Build=build with{Subclass=draft.Build.Subclass},Equipment=gear,Notes=draft.Notes,OwnsInventory=ownsInventory,CustomEquipment=custom,CurrentHp=draft.Facts?.CurrentHp??0,TemporaryHp=draft.Facts?.TemporaryHp??0,Choices=choices.Definitions,
            Validation=new(){Complete=errors.Length==0,Missing=[..errors.Select(e=>e.Message).Distinct(StringComparer.Ordinal)],Warnings=[..choices.Issues.Where(i=>i.Severity!=BuildIssueSeverity.Error).Select(i=>i.Message).Distinct(StringComparer.Ordinal)]}};
    }

    /// <summary>The items a draft without its own starts with.</summary>
    internal IReadOnlyList<EquipmentItem> StartingGear(CharacterDraft draft)
    {
        var build=Identity(draft.Build);var initial=build.ClassTracks?.FirstOrDefault(t=>t.Id==build.InitialClassTrackId)?.ClassName??build.ClassName;
        return Gear(draft,build with{ClassName=initial},catalog.Class(initial)).Gear;
    }

    /// <summary>The draft's items, or the class and background starting equipment and the items it names, with any ability bonuses.</summary>
    private (IReadOnlyList<EquipmentItem> Gear,List<string> Custom,bool OwnsInventory,BuildFacts Build) Gear(CharacterDraft draft,BuildFacts build,CatalogClass? cls)
    {
        if(draft.Inventory is {} inventory)
        {
            WorkspaceRules.Validate(new CharacterState{Id="rule-context",Name="Rule calculation",ClassName=build.ClassName,Subclass=build.Subclass,Species=build.Species,Level=build.Level,MaximumHp=1,CurrentHp=1,AbilityScores=new(DefaultScores),Equipment=inventory});
            return (inventory,[..draft.CustomEquipment],true,build);
        }
        var background=catalog.Background(build.Background);var defaults=new List<string>();
        if(draft.IncludeDefaultEquipment)
        {
            var chosen=ChoiceReading.TextOf(build.ClassOptions.Get("equipment_package"));var package=cls?.EquipmentPackages.FirstOrDefault(p=>p.Name==chosen);
            defaults.AddRange((package?.Items??cls?.StartingEquipment??[]).Where(n=>n.Length>0));
            var backgroundPackage=ChoiceReading.TextOf(build.BackgroundOptions.Get("equipment_package"));
            defaults.AddRange((background?.EquipmentPackages.FirstOrDefault(p=>p.Name==backgroundPackage)?.Items??background?.Equipment??[]).Where(n=>n.Length>0));
        }
        IEnumerable<string> custom=draft.CustomEquipment.Where(n=>n.Length>0);if(build.EquipmentMigrationPending==true)custom=custom.Except(defaults,StringComparer.Ordinal);
        var named=custom.Distinct(StringComparer.Ordinal).ToList();
        return (Named(defaults.Concat(named),draft.EquipmentBonuses),named,false,build with{EquipmentMigrationPending=false});
    }

    /// <summary>Items from their names, such as "Arrows (20)", all equipped, with the ability bonuses named for each.</summary>
    private EquipmentItem[] Named(IEnumerable<string> names,IReadOnlyDictionary<string,Dictionary<Ability,int>> bonuses)
    {
        var trusted=catalog.Equipment.Concat(catalog.WeaponRecords.Select(w=>w.Name)).ToArray();
        return names.Where(n=>n.Length>0).Distinct(StringComparer.Ordinal).Distinct(StringComparer.OrdinalIgnoreCase).Select((label,index)=>
        {
            var normalized=EquipmentLabelRules.Normalize(label,trusted);var own=bonuses.GetValueOrDefault(label);
            return new EquipmentItem{Id="legacy-item"+index,Name=normalized.Name,Quantity=normalized.Quantity,Equipped=true,ModifierBonuses=own is null?[]:Enum.GetValues<Ability>().Where(own.ContainsKey).ToDictionary(a=>a,a=>own[a])};
        }).ToArray();
    }

    private static List<FeatureConfigurationFact> Configurations(IReadOnlyList<FeatureConfiguration> configurations)
        =>[..configurations.Select(c=>new FeatureConfigurationFact{FeatureId=c.FeatureId,CurrentValue=c.CurrentValue})];

    private static Dictionary<string,IReadOnlyList<string>> DerivedFacts(ActiveFeatureSummary active)
    {
        var derived=new Dictionary<string,IReadOnlyList<string>>();
        foreach(var p in new[]{("armor_proficiencies",ProficiencySourceKind.Armor),("weapon_proficiencies",ProficiencySourceKind.Weapon),("tool_proficiencies",ProficiencySourceKind.Tool)})derived[p.Item1]=active.Proficiencies.Where(s=>s.Kind==p.Item2).Select(s=>s.Target).ToArray();
        foreach(var p in new[]{("damage_resistances","resistance"),("damage_immunities","immunity"),("damage_vulnerabilities","vulnerability"),("condition_immunities","conditionImmunity")})derived[p.Item1]=active.Defenses.Where(d=>d.Kind==p.Item2).Select(d=>d.Type).ToArray();
        return derived;
    }

    /// <summary>Which proficiencies and defenses were entered by hand and which the build gives.</summary>
    private static BuildFactOwnershipSummary Ownership(ManualBuildFacts? manual,IReadOnlyDictionary<string,IReadOnlyList<string>> derived)
        =>BuildFactOwnershipRules.Derive(ShownOwnership.Recorded(manual),derived);

    /// <summary>The build with what the standing found: the class and subclass it names, and the lists entered by hand.</summary>
    private static BuildFacts Owned(BuildFacts build,NativeBuildSummary standing)=>build with
    {
        ClassName=standing.ClassName,Subclass=standing.SubclassName,ManualLanguages=[..standing.Languages.Manual],
        ManualSkills=[..standing.ManualSources.Skills],ManualExpertise=[..standing.ManualSources.Expertise],ManualSaves=[..standing.ManualSources.Saves.Select(a=>a.ToString())]
    };

    /// <summary>What a character shows after a standing calculation, as the next calculation reads it back.</summary>
    private static ObservedProficiencies Shown(NativeBuildSummary standing)=>new()
    {
        Skills=standing.Standing.SkillProficiencies,Expertise=standing.Standing.Expertise,Saves=[..standing.Standing.SaveProficiencies.Select(a=>a.ToString())],
        PreviousSkills=[..standing.Standing.SkillProficiencies.Except(standing.ManualSources.Skills)],PreviousExpertise=[..standing.Standing.Expertise.Except(standing.ManualSources.Expertise)],
        PreviousSaves=[..standing.Standing.SaveProficiencies.Except(standing.ManualSources.Saves).Select(a=>a.ToString())],
        Languages=standing.Languages.Languages,PreviousLanguages=standing.Languages.Derived,Scores=standing.Scores.Scores
    };

    private NativeBuildSummary Derived(BuildFacts build,IReadOnlyList<EquipmentItem> equipment,ObservedProficiencies observed)
    {
        var summary=builds.Derive(build,equipment,true,observed);
        var manual=(build.ManualFacts?.WeaponProficiencies??[]).Select(name=>new ChoiceProficiencySource("manual:weapon:"+name,ProficiencySourceKind.Weapon,name));
        return summary with{ProficiencySources=summary.ProficiencySources.Concat(manual).Distinct().ToArray()};
    }

    private static List<HpGainFact> History(HitPointSummary hp)=>[..hp.History.Select(e=>new HpGainFact{CharacterLevel=e.CharacterLevel,ClassName=e.ClassName,Method=e.Method.ToString().ToLowerInvariant(),BaseGain=e.BaseGain})];

    /// <summary>The named bonuses the sheet explains beside a skill or initiative, such as Jack of All Trades.</summary>
    private static Dictionary<string,List<CalculationTerm>> Terms(NativeBuildSummary summary)
    {
        var named=new Dictionary<string,List<CalculationTerm>>(StringComparer.Ordinal);
        foreach(var pair in summary.Standing.Skills)
        {
            var extra=pair.Value.Terms.Where(term=>term.Label.StartsWith("Jack of All Trades")||term.Label.StartsWith("Thaumaturge")||term.Label.StartsWith("Magician")).ToList();
            if(extra.Count>0)named.Add("skill:"+pair.Key,extra);
        }
        var alert=summary.Standing.Initiative.Terms.Where(term=>term.Label.StartsWith("Alert")).ToList();if(alert.Count>0)named.Add("initiative",alert);
        return named;
    }

    /// <summary>Attacks with the character's final modifiers: the best eligible ability, and the change since the first pass.</summary>
    private static List<WeaponAttack> Rescored(IReadOnlyList<AttackActionMetadata> attacks,NativeBuildSummary first,NativeBuildSummary final)
    {
        var result=new List<WeaponAttack>();
        foreach(var attack in attacks)
        {
            var ability=attack.Ability;var modifiers=final.Standing.Modifiers;
            if(!Enum.IsDefined(ability)||attack.AbilityCandidates.Any(a=>!Enum.IsDefined(a)))throw new ArgumentException("An attack source has an invalid governing ability.");
            if(attack.AbilityCandidates.Count is <1 or >6)throw new ArgumentException("An attack needs a bounded list of eligible abilities.");
            if(attack.AbilityCandidates.Distinct().Count()!=attack.AbilityCandidates.Count)throw new ArgumentException("Attack candidate abilities must be unique.");
            var best=attack.AbilityCandidates.Max(a=>modifiers[a].Total);
            if(!attack.AbilityCandidates.Contains(ability)||modifiers[ability].Total!=best)ability=attack.AbilityCandidates.First(a=>modifiers[a].Total==best);
            result.Add(Attack(attack,ability,modifiers[ability].Total-first.Standing.Modifiers[ability].Total,modifiers[ability].Total));
        }
        return result;
    }

    /// <summary>An attack as the sheet shows it, moved by <paramref name="change"/> in its ability's modifier.</summary>
    private static WeaponAttack Attack(AttackActionMetadata attack,Ability ability,int change,int modifier)
    {
        var damage=attack.DamageModifier+change;
        return new(){Id=attack.Id,Name=attack.Name,Kind=attack.Kind,Ability=ability,AbilityCandidates=[..attack.AbilityCandidates],Proficient=attack.Proficient,FlatBonus=attack.FlatBonus,AttackBonus=attack.AttackBonus+change,
            DamageDice=attack.DamageDice,FixedDamage=attack.FixedDamage,DamageModifier=damage,DamageModifierAdjustment=damage-modifier,DamageType=attack.DamageType,Range=attack.Range,Notes=attack.Notes,Ammunition=attack.Ammunition};
    }

    private sealed record HpResult(BuildFacts Build,int Maximum,int Current,int Temporary);

    /// <summary>
    /// Maximum and current HP around the character a draft edits. A level-up records the HP it gained, and adds it to
    /// current HP when the player asks for that.
    /// </summary>
    private HpResult Hp(BuildFacts build,NativeBuildSummary final,ActiveFeatureSummary active,CharacterState? source,bool addGainedHp,int startHp,int startTemporary,out int hitDie,out ClassProgressionSnapshot progression)
    {
        progression=ClassProgressionRules.AtLevel(classes,final.ClassName,build.Level,final.SubclassName);hitDie=progression.HitDie;
        var hp=HitPointRules.Calculate(HitPointRules.FromBuild(build,progression.HitDie,final.Standing.Modifiers[Ability.Constitution].Total,active.OriginFeats,active.SpeciesTraits,startHp,startTemporary));
        var current=source?.CurrentHp??hp.CurrentHp;
        if(source is not null&&(addGainedHp||progression.Level>source.Level))
        {
            var sourceBuild=WorkspaceProjection.BuildOf(source);var sourceClass=ClassProgressionRules.AtLevel(classes,source.ClassName,source.Level,source.Subclass);
            var before=HitPointRules.Calculate(HitPointRules.FromBuild(sourceBuild,sourceClass.HitDie,CharacterCalculations.Ability(source,Ability.Constitution).Total,[],[],source.CurrentHp,source.TemporaryHp));
            var gained=hp.Maximum-before.Maximum;
            if(addGainedHp)current+=Math.Max(0,gained);
            if(progression.Level==source.Level+1)
            {
                var level=progression.Level;
                if(build.LevelHistory is {Count:>0} levels&&levels[^1].ToLevel==level)build=build with{LevelHistory=[..levels.Take(levels.Count-1),levels[^1] with{HpIncrease=gained}]};
                if(build.HpRolls is {Count:>0} rolls&&rolls[^1].Level==level)build=build with{HpRolls=[..rolls.Take(rolls.Count-1),rolls[^1] with{Value=gained}]};
            }
        }
        return new(build with{HpHistory=History(hp)},hp.Maximum,Math.Clamp(current,0,hp.Maximum),source?.TemporaryHp??hp.TemporaryHp);
    }

    private sealed record DefenseResult(int ArmorClass,ArmorClassDetail Detail,MovementSummary Movement);

    private DefenseResult Defenses(BuildFacts build,NativeBuildSummary final,ClassProgressionSnapshot progression,IReadOnlyList<EquipmentItem> equipment)
    {
        int Modifier(Ability ability)=>final.Standing.Modifiers[ability].Total;
        var feats=(build.Feats??[]).Select(f=>f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var armor=ArmorRules.Calculate(new(){ClassName=final.ClassName,Subclass=final.SubclassName,Level=build.Level,DexterityScore=final.Scores.Scores[Ability.Dexterity],DexterityModifier=Modifier(Ability.Dexterity),ConstitutionModifier=Modifier(Ability.Constitution),WisdomModifier=Modifier(Ability.Wisdom),CharismaModifier=Modifier(Ability.Charisma),
            DefenseStyle=FightingStyleRules.ActiveStyles(build).Contains("Defense",StringComparer.Ordinal),MediumArmorMaster=feats.Contains("Medium Armor Master"),Equipment=equipment});
        static ArmorClassCandidate Candidate(ArmorCandidate c)=>new(){Id=Regex.Replace(c.Name.ToLowerInvariant(),"[^a-z0-9]+","_").Trim('_'),Name=c.Name,Source=c.Source,BaseValue=c.Calculation.Total-(c.ShieldApplied?2:0)-c.DefenseBonus,Value=c.Calculation.Total,AllowsShield=c.AllowsShield,ShieldApplied=c.ShieldApplied,DefenseBonus=c.DefenseBonus};
        var best=Candidate(armor.Best);
        var detail=new ArmorClassDetail{Value=armor.Best.Calculation.Total,FormulaId=best.Id,Source=armor.Best.Source,SourceName=armor.Best.Name,ShieldApplied=armor.Best.ShieldApplied,DefenseBonus=armor.Best.DefenseBonus,MediumArmorDexterityCap=armor.MediumDexterityCap,Candidates=[..armor.Candidates.Select(Candidate)]};
        return new(armor.Best.Calculation.Total,detail,Movement(build,final,progression,equipment));
    }

    private MovementSummary Movement(BuildFacts build,NativeBuildSummary standing,ClassProgressionSnapshot progression,IReadOnlyList<EquipmentItem> equipment)
        =>MovementRules.Calculate(new(){ClassName=standing.ClassName,Level=build.Level,BaseSpeed=SpeciesSpeed(build.Species),UnarmoredMovementBonus=progression.Resources.Count("unarmored_movement"),
            Feats=(build.Feats??[]).Select(f=>f.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),Equipment=equipment});

    private int SpeciesSpeed(string species)=>catalog.SpeciesRecords.FirstOrDefault(s=>string.Equals(s.Name,species,StringComparison.OrdinalIgnoreCase))?.Speed??30;

    /// <summary>
    /// The resources a character has: the class's own, with any whose size depends on the character's standing, then
    /// its spell slots.
    /// </summary>
    private static List<ResourceDefinition> Pools(IReadOnlyList<CharacterResourceMetadata> pools,IReadOnlyList<DependentPool> dependent,SpellcastingProfile? casting)
    {
        var result=pools.Select(p=>new ResourceDefinition(p.Id,p.Name,p.Maximum,p.Recovery,p.SlotLevel,null,p.ShortRestRecoveryAmount)).ToList();
        foreach(var pool in dependent)
        {
            var recovery=pool.Recovery==RecoveryRest.Short?"short_or_long_rest":pool.Recovery==RecoveryRest.Long?"long_rest":"manual";
            var index=result.FindIndex(r=>r.Id==pool.Id);
            if(index<0)result.Add(new(pool.Id,pool.Name,pool.Maximum,recovery));else result[index]=result[index] with{Name=pool.Name,Maximum=pool.Maximum,Recovery=recovery};
        }
        result.RemoveAll(r=>r.SlotLevel is not null);
        if(casting is not null)
            foreach(var pair in casting.Slots.Where(p=>p.Value>0))
                result.Add(casting.CastingType=="pact"?new("pact","Pact slots",pair.Value,"short_or_long_rest",pair.Key):new("slot"+pair.Key,"Level "+pair.Key+" slots",pair.Value,"long_rest",pair.Key));
        return result;
    }
}

public sealed partial class NativeCharacterBuilder
{
    /// <summary>
    /// An uploaded rules character document as a draft. What it showed that its build does not explain (skills,
    /// Expertise, saves, languages, proficiencies and defenses) becomes hand-entered; its Herohold sidecars become play facts.
    /// </summary>
    internal CharacterDraft Import(JsonObject document)
    {
        var migrated=CanonicalCharacterRules.Migrate(document);
        var draft=CharacterDraft.FromDocument(migrated) with{Facts=WorkspaceProjection.ImportedFacts(migrated)};
        if(!SettlesOnImport(draft))return draft;
        return Import(draft,new(){Proficiencies=ObservedProficiencies.From(migrated),Ownership=ShownOwnership.From(migrated),
            GrantDetails=SpellGrantRecord.FromDocument(migrated["spell_grant_details"]),Expertise=Strings(migrated["expertise"]).ToArray()});
    }
    /// <summary>Whether an import tells hand-entered values apart: a single known class (class tracks keep what they record).</summary>
    private bool SettlesOnImport(CharacterDraft draft)
        =>draft.Build.ClassTracks is null&&draft.Build.ClassLevels is not {Count:>1}&&catalog.Class(Identity(draft.Build).ClassName) is not null;
    /// <summary>An imported draft with what its source showed told apart: values neither the build gives nor it gave before are its own, entered by hand.</summary>
    internal CharacterDraft Import(CharacterDraft draft,ShownValues shown)
    {
        if(!SettlesOnImport(draft))return draft;
        var build=Identity(draft.Build);var cls=catalog.Class(build.ClassName)!;
        var gear=Gear(draft,build,cls).Gear;var observed=shown.Proficiencies;
        var first=builds.Derive(build,gear,true,observed);
        var active=features.Derive(build,first,ClassChoiceContextFactory.FromBuild(build,first,shown.GrantDetails,shown.Expertise));
        var owned=BuildFactOwnershipRules.Derive(shown.Ownership,DerivedFacts(active));
        var manual=new ManualBuildFacts{ToolProficiencies=[..owned.Manual["tool_proficiencies"]],ArmorProficiencies=[..owned.Manual["armor_proficiencies"]],WeaponProficiencies=[..owned.Manual["weapon_proficiencies"]],
            DamageResistances=[..owned.Manual["damage_resistances"]],DamageImmunities=[..owned.Manual["damage_immunities"]],DamageVulnerabilities=[..owned.Manual["damage_vulnerabilities"]],ConditionImmunities=[..owned.Manual["condition_immunities"]]};
        var languages=active.Proficiencies.Where(s=>s.Kind==ProficiencySourceKind.Language).Select(s=>s.Target).Distinct(StringComparer.Ordinal).ToArray();
        var standing=builds.Derive(build with{ManualFacts=manual,FeatureConfigurations=Configurations(active.FeatureConfigurations)},gear,true,observed with{Languages=languages});
        return draft with{Build=draft.Build with{ManualFacts=manual,ManualLanguages=[..standing.Languages.Manual],ManualSkills=[..standing.ManualSources.Skills],ManualExpertise=[..standing.ManualSources.Expertise],ManualSaves=[..standing.ManualSources.Saves.Select(a=>a.ToString())]}};
    }
}

/// <summary>What an imported character showed beside its build: its proficiency lists, granted spells and Expertise.</summary>
internal sealed record ShownValues
{
    public ObservedProficiencies Proficiencies { get; init; } = new();
    public ShownOwnership Ownership { get; init; } = ShownOwnership.None;
    public IReadOnlyList<SpellGrantRecord> GrantDetails { get; init; } = [];
    public IReadOnlyList<string> Expertise { get; init; } = [];
}
