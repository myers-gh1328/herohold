using Herohold.TestSupport;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestClass]
public sealed class MulticlassCompositionTests
{
    [TestMethod]
    public void LegacySingleTrackAdaptsWithoutMutatingAndExplicitUnknownHistoryRemainsBlocked()
    {
        var source=NativeCharacterBuilderTests.Fighter();var before=source.ToJsonString();var one=ClassTrackRules.Read(BuildFacts.FromCanonical(source));Assert.IsTrue(one.CanCalculate);Assert.AreEqual(1,one.Tracks.Count);Assert.AreEqual(before,source.ToJsonString());
        source["level"]=2;source["class_levels"]=new JsonObject{["Fighter"]=1,["Wizard"]=1};Assert.IsFalse(ClassTrackRules.Read(BuildFacts.FromCanonical(source)).CanCalculate);
    }
    internal static JsonObject FighterWizard()
    {
        var source=NativeCharacterBuilderTests.Fighter();source["level"]=2;source["base_ability_scores"]!["Intelligence"]=13;source["base_ability_scores"]!["Strength"]=13;
        source["class_levels"]=new JsonObject{["Fighter"]=1,["Wizard"]=1};source["initial_class_track_id"]="class-fighter";
        source["folio_class_tracks"]=new JsonArray(Track("Fighter",1,source),Track("Wizard",1,NativeCharacterBuilderTests.Wizard()));
        source["hp_history"]=new JsonArray(new JsonObject{["character_level"]=1,["class_name"]="Fighter",["method"]="maximum",["base_gain"]=10},new JsonObject{["character_level"]=2,["class_name"]="Wizard",["method"]="fixed",["base_gain"]=4});return source;
    }
    internal static JsonObject Track(string name,int level,JsonObject choice)=>new(){["id"]="class-"+name.ToLowerInvariant(),["class_name"]=name,["rules_source"]="XPHB",["level"]=level,["subclass"]=choice["subclass"]?.DeepClone()??JsonValue.Create(""),["choices"]=new JsonObject{["class_options"]=choice["class_options"]?.DeepClone()??new JsonObject(),["spells"]=choice["spells"]?.DeepClone()??new JsonObject(),["weapon_masteries"]=choice["weapon_masteries"]?.DeepClone()??new JsonArray()}};
    private static NativeLevelUpRules LevelRules(Herohold.Web.Services.NativeCharacterBuilder builder)
    {
        var root=new DirectoryInfo(Environment.CurrentDirectory);while(root is not null&&!File.Exists(Path.Combine(root.FullName,"data/imported/5etools/classes.json")))root=root.Parent;
        var catalog=NativeCatalogIntegrationTests.Snapshot();var classes=ClassProgressionCatalog.Parse(File.ReadAllText(Path.Combine(root!.FullName,"data/imported/5etools/classes.json")));
        return new(catalog,classes,NativeCharacterBuilderTests.Feats(),new(catalog),builder);
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public void AddingWizardUsesNewClassChoicesAndOneNonmaximumHpContributionThenCanContinueFighter()
    {
        var builder=NativeCharacterBuilderTests.Builder();var fighter=NativeCharacterBuilderTests.Fighter();fighter["base_ability_scores"]!["Strength"]=13;fighter["base_ability_scores"]!["Intelligence"]=13;var source=builder.Recalculate(fighter);var before=source.ToJsonString();var rules=LevelRules(builder);var review=rules.Start(source,2,"Wizard");Assert.IsFalse(review.CanFinalize);Assert.IsTrue(review.Draft.AddsNewClass);Assert.AreEqual(4,review.HpBaseGain);Assert.IsFalse(review.PendingChoices.Any(d=>d.Path.SequenceEqual(new[]{"class_options","class_skills"})));
        var wizard=NativeCharacterBuilderTests.Wizard();
        for(var pass=0;pass<8&&!review.CanFinalize;pass++)foreach(var pending in review.PendingChoices.Where(d=>d.Status!=BuildChoiceStatus.Resolved).ToArray())
        {
            JsonNode? value=wizard;foreach(var key in pending.Path)value=(value as JsonObject)?[key];if(value is not null)review=rules.Choose(source,review.Draft,pending.Id,value.DeepClone());
        }
        Assert.IsTrue(review.CanFinalize,string.Join(";",review.Issues.Select(i=>i.Message)));Assert.AreEqual(before,source.ToJsonString());var final=rules.Commit(source,review.Draft).Character;Assert.AreEqual(14,final["hit_points"]!["maximum"]!.GetValue<int>());Assert.AreEqual(1,final["class_levels"]!["Wizard"]!.GetValue<int>());
        var fighter2=rules.Start(final,3,"Fighter");Assert.IsTrue(fighter2.CanFinalize,string.Join(";",fighter2.Issues.Select(i=>i.Message)));Assert.AreEqual(20,rules.Commit(final,fighter2.Draft).Character["hit_points"]!["maximum"]!.GetValue<int>());
    }


    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow("Barbarian")][DataRow("Bard")][DataRow("Cleric")][DataRow("Druid")][DataRow("Monk")][DataRow("Paladin")][DataRow("Ranger")][DataRow("Rogue")][DataRow("Sorcerer")][DataRow("Warlock")][DataRow("Wizard")]
    public void EveryAdditionalClassResolvesItsOwnEntryChoicesAndCanAdvanceItsNextClassLevel(string name)
    {
        var builder=NativeCharacterBuilderTests.Builder();var fighter=NativeCharacterBuilderTests.Fighter();foreach(var a in Enum.GetNames<Ability>())fighter["base_ability_scores"]![a]=13;var source=builder.Recalculate(fighter);var rules=LevelRules(builder);var review=rules.Start(source,2,name);
        for(var step=0;!review.CanFinalize&&step<60;step++)
        {
            var pending=review.PendingChoices.FirstOrDefault(d=>d.Status!=BuildChoiceStatus.Resolved);Assert.IsNotNull(pending,name+": "+string.Join(";",review.Issues.Select(i=>i.Message)));var options=pending.Options.Where(o=>!o.Disabled).ToArray();Assert.IsTrue(options.Length>0,name+" "+pending.Id);var value=pending.Mode==BuildChoiceMode.Multi?(JsonNode)new JsonArray(options.Take(pending.Choose).Select(o=>CanonicalFixtures.Node(o.Value)).ToArray()):CanonicalFixtures.Node(options[0].Value);review=rules.Choose(source,review.Draft,pending.Id,value);
        }
        Assert.IsTrue(review.CanFinalize,name+": "+string.Join(";",review.Issues.Select(i=>i.Message)));var final=rules.Commit(source,review.Draft).Character;Assert.AreEqual(1,final["class_levels"]![name]!.GetValue<int>());Assert.AreEqual(10+1+HitPointRules.FixedHitDieGain(MulticlassRules.HitDie(name))+1,final["hit_points"]!["maximum"]!.GetValue<int>());
        var next=rules.Start(final,3,name);Assert.AreEqual(2,next.Draft.TargetClassLevel);Assert.AreEqual(3,next.Draft.TargetLevel);
    }


    internal static JsonObject Complete(string name,int level)
    {
        var builder=NativeCharacterBuilderTests.Builder();var source=NativeCharacterBuilderTests.Fighter();source["class"]=name;source["level"]=level;source["class_options"]=new JsonObject();source["weapon_masteries"]=new JsonArray();foreach(var a in Enum.GetNames<Ability>())source["base_ability_scores"]![a]=13;
        var edits=new CharacterEditRules(NativeCatalogIntegrationTests.Snapshot());
        for(var step=0;step<100;step++)
        {
            source=builder.Recalculate(source);var report=builder.Describe(source);var choice=report.Definitions.FirstOrDefault(d=>d.Status!=BuildChoiceStatus.Resolved);if(choice is null){Assert.AreEqual(0,report.Issues.Count,string.Join(";",report.Issues.Select(i=>i.Message)));return source;}
            var available=choice.Options.Where(o=>!o.Disabled).ToArray();Assert.IsTrue(available.Length>=choice.Choose,name+": "+choice.Id);var value=choice.Mode==BuildChoiceMode.Multi?(JsonNode)new JsonArray(available.Take(choice.Choose-choice.Details.LockedValues.Count).Select(o=>CanonicalFixtures.Node(o.Value)).ToArray()):CanonicalFixtures.Node(available[0].Value);var patch=new JsonObject();var cursor=patch;foreach(var key in choice.Path.SkipLast(1)){var next=new JsonObject();cursor[key]=next;cursor=next;}cursor[choice.Path.Last()]=value;source=edits.Apply(source,patch);
        }
        Assert.Fail("Single class fixture did not resolve: "+name+"; "+string.Join(";",builder.Describe(source).Definitions.Where(d=>d.Status!=BuildChoiceStatus.Resolved).Select(d=>d.Id+":"+string.Join(",",d.Issues.Select(i=>i.Message)))));return source;
    }
    internal static JsonObject Combine(JsonObject initial,JsonObject additional)
    {
        var name=initial["class"]!.GetValue<string>();var other=additional["class"]!.GetValue<string>();var firstLevel=initial["level"]!.GetValue<int>();var secondLevel=additional["level"]!.GetValue<int>();var secondary=additional.DeepClone().AsObject();var skillCount=MulticlassRules.EntrySkills(other);var options=secondary["class_options"]!.AsObject();if(skillCount==0)options.Remove("class_skills");else if(options["class_skills"] is JsonArray skills)options["class_skills"]=new JsonArray(skills.Take(skillCount).Select(n=>n!.DeepClone()).ToArray());if(other=="Bard"&&options["tool_proficiencies"] is JsonArray tools)options["tool_proficiencies"]=new JsonArray(tools.Take(1).Select(n=>n!.DeepClone()).ToArray());if(other=="Monk")options.Remove("tool_proficiencies");var result=initial.DeepClone().AsObject();result["level"]=firstLevel+secondLevel;result["initial_class_track_id"]="class-"+name.ToLowerInvariant();result["class_levels"]=new JsonObject{[name]=firstLevel,[other]=secondLevel};result["folio_class_tracks"]=new JsonArray(Track(name,firstLevel,initial),Track(other,secondLevel,secondary));var history=result["hp_history"]!.AsArray();for(var i=1;i<=secondLevel;i++)history.Add(new JsonObject{["character_level"]=firstLevel+i,["class_name"]=other,["method"]="fixed",["base_gain"]=HitPointRules.FixedHitDieGain(MulticlassRules.HitDie(other))});return result;
    }
    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow(1,3)][DataRow(3,4)]
    public void RangerWizardSharedSlotsRoundUpButEachClassKeepsItsOwnSpellLevelAccess(int rangerLevel,int expectedFirstLevelSlots)
    {
        var raw=Combine(Complete("Ranger",rangerLevel),Complete("Wizard",1));var result=NativeCharacterBuilderTests.Builder().ResultFor(raw);Assert.IsTrue(result["validation"]!["complete"]!.GetValue<bool>(),result["validation"]!.ToJsonString());var state=TestStores.Character(result);
        Assert.AreEqual(expectedFirstLevelSlots,state.Resources.Single(p=>p.Id=="slot1").Maximum);Assert.AreEqual(rangerLevel==1?0:2,state.Resources.SingleOrDefault(p=>p.Id=="slot2")?.Maximum??0);foreach(var source in state.Play.SpellSources.Where(s=>s.SourceKind=="class")){Assert.AreEqual(1,source.Preparation.MaximumSpellLevel);Assert.IsFalse(source.Preparation.EligibleSpellIds.Select(id=>state.Play.Spells.Single(s=>s.Id==id)).Any(s=>s.Level>1));}
        Assert.IsTrue(state.Play.SpellSources.Any(s=>s.Id=="class_feature:Favored Enemy"));Assert.IsTrue(state.Resources.Any(p=>p.Id=="hunters"));
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public void WizardWarlockKeepsNinthLevelSharedSlotsAndSecondLevelPactSlotsWithSeparateAbilities()
    {
        var raw=Combine(Complete("Wizard",17),Complete("Warlock",3));var result=NativeCharacterBuilderTests.Builder().ResultFor(raw);Assert.IsTrue(result["validation"]!["complete"]!.GetValue<bool>(),result["validation"]!.ToJsonString());var state=TestStores.Character(result);Assert.AreEqual(1,state.Resources.Single(p=>p.Id=="slot9").Maximum);var pact=state.Resources.Single(p=>p.Id=="pact");Assert.AreEqual(2,pact.SlotLevel);Assert.AreEqual(2,pact.Maximum);Assert.AreEqual(RecoveryRest.Short,pact.Recovery);Assert.AreEqual("class-warlock",pact.SourceId);Assert.AreEqual(Ability.Intelligence,state.Play.SpellSources.Single(s=>s.Id=="class-wizard").Ability);Assert.AreEqual(Ability.Charisma,state.Play.SpellSources.Single(s=>s.Id=="class-warlock").Ability);Assert.AreEqual(2,state.Play.SpellSources.Single(s=>s.Id=="class-warlock").Preparation.MaximumSpellLevel);var first=NativeCharacterBuilderTests.Builder().Recalculate(raw);Assert.IsTrue(JsonNode.DeepEquals(first,NativeCharacterBuilderTests.Builder().Recalculate(first)),"Additional-class armor must retain derived ownership across repeated calculation.");
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public void FighterRangerExtraAttackDoesNotStackAndRepeatedCalculationDoesNotCompound()
    {
        var raw=Combine(Complete("Fighter",5),Complete("Ranger",5));var builder=NativeCharacterBuilderTests.Builder();var result=builder.ResultFor(raw);Assert.IsTrue(result["validation"]!["complete"]!.GetValue<bool>(),result["validation"]!.ToJsonString());Assert.AreEqual(2,result["playMetadata"]!["attackActionCount"]!.GetValue<int>());var one=builder.Recalculate(raw);var two=builder.Recalculate(one);Assert.IsTrue(JsonNode.DeepEquals(one,two),"Repeated composition must preserve source ownership and numerical values: "+string.Join(";",one.Where(p=>!JsonNode.DeepEquals(p.Value,two[p.Key])).Select(p=>p.Key+" FIRST="+p.Value?.ToJsonString()+" SECOND="+two[p.Key]?.ToJsonString())));
    }


    [TestCategory("RulesData")]
    [TestMethod]
    public void RealNativeSourceRestExportAndRecalculationRetainChangedWizardPreparationAndOwnedIds()
    {
        var builder=NativeCharacterBuilderTests.Builder();var state=TestStores.Calculated(FighterWizard());state=state with{CurrentHp=state.MaximumHp};var wizard=state.Play.SpellSources.Single(s=>s.Id=="class-wizard");var selected=wizard.SpellbookIds.First(id=>!wizard.PreparedSpellIds.Contains(id));var next=PlayRules.Rest(new(){Character=state},new(){Kind=RestKind.Long,SourceChanges=[new(){SpellSourceId=wizard.Id,ChangeSpells=true,NewPreparedSpellIds=[selected]}]});
        var raw=Herohold.Web.Services.WorkspaceProjection.ToCanonical(next);var recalculated=TestStores.Calculated(raw,next);CollectionAssert.AreEqual(new[]{selected},recalculated.Play.SpellSources.Single(s=>s.Id==wizard.Id).PreparedSpellIds);Assert.AreEqual(next.MaximumHp,recalculated.CurrentHp);Assert.AreEqual(2,recalculated.Play.HitDiePools.Count);
    }


    [TestCategory("RulesData")]
    [TestMethod]
    public void WizardMonkRetainsAllSixDisciplinedSurvivorSavesWithoutAddingMonkStartingTraining()
    {
        var raw=Combine(Complete("Wizard",1),Complete("Monk",14));var result=NativeCharacterBuilderTests.Builder().ResultFor(raw);Assert.IsTrue(result["validation"]!["complete"]!.GetValue<bool>(),result["validation"]!.ToJsonString());var state=TestStores.Character(result);CollectionAssert.AreEquivalent(Enum.GetValues<Ability>(),state.SaveProficiencies.ToArray());
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public void IndependentClassDexAsisAreSharedGlobalAdjustmentsAndBothApplyExactlyOnce()
    {
        var raw=Combine(Complete("Fighter",4),Complete("Ranger",4));var baseDex=raw["base_ability_scores"]!["Dexterity"]!.GetValue<int>()+1;raw["ability_score_adjustments"]=new JsonArray(new JsonObject{["source_id"]="fighter-asi-4",["source_type"]="class_level",["source_class"]="Fighter",["source_level"]=4,["adjustments"]=new JsonObject{["Dexterity"]=2}},new JsonObject{["source_id"]="ranger-asi-4",["source_type"]="class_level",["source_class"]="Ranger",["source_level"]=4,["adjustments"]=new JsonObject{["Dexterity"]=2}});var result=NativeCharacterBuilderTests.Builder().ResultFor(raw);Assert.IsTrue(result["validation"]!["complete"]!.GetValue<bool>(),result["validation"]!.ToJsonString());Assert.AreEqual(baseDex+4,result["character"]!["ability_scores"]!["Dexterity"]!.GetValue<int>());var next=NativeCharacterBuilderTests.Builder().Recalculate(result["character"]!.AsObject());Assert.AreEqual(baseDex+4,next["ability_scores"]!["Dexterity"]!.GetValue<int>());
    }

    [TestCategory("RulesData")]
    [TestMethod]
    public void FighterWizardComposesClassFeaturesHpAndSourceSpellAccessWithoutWizardInitialSaves()
    {
        var source=FighterWizard();var before=source.ToJsonString();var result=NativeCharacterBuilderTests.Builder().ResultFor(source);
        Assert.IsTrue(result["validation"]!["complete"]!.GetValue<bool>(),result["validation"]!.ToJsonString());Assert.AreEqual(before,source.ToJsonString());var state=TestStores.Character(result);
        Assert.AreEqual(14,state.MaximumHp);Assert.IsTrue(state.Features.Contains("Second Wind"));Assert.IsTrue(state.Features.Contains("Spellcasting"));CollectionAssert.AreEquivalent(new[]{Ability.Strength,Ability.Constitution},state.SaveProficiencies);Assert.AreEqual(1,state.Play.SpellSources.Count(s=>s.SourceKind=="class"));Assert.AreEqual(2,state.Play.HitDiePools.Count);
    }
}
