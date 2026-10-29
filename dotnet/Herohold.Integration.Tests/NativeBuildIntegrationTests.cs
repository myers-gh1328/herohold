using Herohold.TestSupport;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Rules;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestCategory("RulesData")][TestClass] public sealed class NativeBuildIntegrationTests
{
    private static CharacterBuildCatalog Catalog()
    {
        var root=new DirectoryInfo(Environment.CurrentDirectory);while(root is not null&&!Directory.Exists(Path.Combine(root.FullName,"data/imported/5etools")))root=root.Parent;
        var path=Path.Combine(root!.FullName,"data/imported/5etools");
        return CharacterBuildCatalog.Parse(ClassProgressionCatalog.Parse(File.ReadAllText(Path.Combine(path,"classes.json"))),File.ReadAllText(Path.Combine(path,"backgrounds.json")),File.ReadAllText(Path.Combine(path,"species.json")));
    }
    [TestMethod] public void ExistingRangerSourcesRebuildScoresWithoutDoublingFeatRaisesOrEquipmentModifiers()
    {
        var raw=SampleCanonical.Create();var original=raw.ToJsonString();
        var result=Catalog().Derive(raw,SampleWorkspace.Create().Character.Equipment);
        Assert.AreEqual(16,result.Scores.Scores[Ability.Wisdom]);
        Assert.AreEqual(4,result.Standing.Modifiers[Ability.Wisdom].Total);
        Assert.AreEqual(10,result.Standing.Skills["Perception"].Total);Assert.AreEqual(10,result.Standing.Skills["Stealth"].Total);
        Assert.AreEqual(8,result.Standing.Initiative.Total);Assert.AreEqual(original,raw.ToJsonString());
    }
    [TestMethod] public void FreshPreFeatSourcesDoNotUpgradeKeenMindBecauseCanonicalAlreadyContainsItsSkill()
    {
        var raw=SampleCanonical.Create();raw["general_feats"]=new JsonArray(new JsonObject{["name"]="Keen Mind",["choices"]=new JsonObject{["skill"]="Arcana"}});
        raw["ability_score_adjustments"]=new JsonArray();raw["skill_proficiencies"]!.AsArray().Add("Arcana");raw["expertise"]=new JsonArray("Perception");
        var result=Catalog().Derive(raw,[]);
        Assert.IsTrue(result.Standing.SkillProficiencies.Contains("Arcana"));Assert.IsFalse(result.Standing.Expertise.Contains("Arcana"));
        Assert.IsFalse(result.ManualSources.Skills.Contains("Arcana"));
    }
    [TestMethod] public void UnattributedManualProficienciesSurviveWithExplicitOwnershipAndIssues()
    {
        var raw=SampleCanonical.Create();raw["skill_proficiencies"]!.AsArray().Add("Medicine");raw["expertise"]!.AsArray().Add("Medicine");raw["saving_throw_proficiencies"]!.AsArray().Add("Charisma");
        var result=Catalog().Derive(raw,[]);
        Assert.IsTrue(result.ManualSources.Skills.Contains("Medicine"));Assert.IsTrue(result.ManualSources.Expertise.Contains("Medicine"));Assert.IsTrue(result.ManualSources.Saves.Contains(Ability.Charisma));
        Assert.IsTrue(result.Issues.Count>0);Assert.AreEqual(9,result.Standing.Skills["Medicine"].Total);
    }
    [TestMethod] public void DuplicateBackgroundDraftRemainsEditableButCompletedMalformedSelectionRejects()
    {
        var raw=SampleCanonical.Create();raw["background_options"]!["secondary_ability"]="Dexterity";
        Assert.IsTrue(Catalog().Derive(raw,[],true).Issues.Count>0);
        Assert.Throws<ArgumentException>(()=>Catalog().Derive(raw,[]));
    }
    [TestMethod] public void NativeBuildProjectionPersistsManualSourcesAndNativeStandingTerms()
    {
        var raw=SampleCanonical.Create();raw["skill_proficiencies"]!.AsArray().Add("Medicine");
        var draft=TestStores.Builder.Import(raw);CollectionAssert.Contains(draft.Build.ManualSkills,"Medicine","A skill the build does not explain is kept as hand-entered.");
        var first=TestStores.Builder.Calculate(draft,SampleWorkspace.Create().Character,false);
        Assert.AreEqual(4,first.Modifiers[Ability.Wisdom]);Assert.IsTrue(first.SkillProficiencies.Contains("Medicine"));
        var again=TestStores.Builder.Calculate(draft with{Build=first.Build},SampleWorkspace.Create().Character,false);
        CollectionAssert.AreEquivalent(first.SkillBonuses.ToArray(),again.SkillBonuses.ToArray());
    }
    [TestMethod] public void DistinctFeatInstancesAndActiveAdditionalSkilledChoicesRetainTheirOwnSources()
    {
        var raw=SampleCanonical.Create();raw["ability_score_adjustments"]=new JsonArray();
        foreach(var id in new[]{"first","second"})raw["ability_score_adjustments"]!.AsArray().Add(new JsonObject{["source_type"]="feat",["source_feat"]="elemental-adept",["feat_instance_id"]=id,["adjustments"]=new JsonObject{["Wisdom"]=1}});
        raw["background"]="Entertainer";raw["species"]="Human";raw["background_options"]!["secondary_ability"]="Charisma";raw["species_options"]!["origin_feats"]=new JsonArray("Skilled");
        raw["class_options"]!["class_skills"]=new JsonArray("Stealth","Nature","Survival");raw["species_options"]!["skill_proficiencies"]=new JsonArray("Perception");
        raw["origin_feat_choices"]=new JsonObject{["tools"]=new JsonArray("Flute")};
        raw["folio_origin_feat_choices"]=new JsonObject{["Skilled"]=new JsonObject{["skills"]=new JsonArray("Medicine","History","Religion")}};
        raw["skill_proficiencies"]!.AsArray().Add("Medicine");
        var result=Catalog().Derive(raw,[]);
        Assert.AreEqual(16,result.Scores.Scores[Ability.Wisdom]);Assert.IsTrue(result.Standing.SkillProficiencies.Contains("Medicine"));Assert.IsFalse(result.ManualSources.Skills.Contains("Medicine"));
    }
    [TestMethod] public void PreviouslyOwnedFeatExpertiseDoesNotBecomeManualAfterFeatRemoval()
    {
        var draft=TestStores.Builder.Import(SampleCanonical.Create());var first=TestStores.Builder.Calculate(draft,null,false);
        var next=TestStores.Builder.Calculate(draft with{Build=first.Build with{Feats=[],AbilityScoreAdjustments=[]}},null,false);
        Assert.IsFalse(next.Expertise.Contains("Stealth"));Assert.IsFalse(next.SkillProficiencies.Contains("Acrobatics"));Assert.IsTrue(next.Expertise.Contains("Perception"));
    }
    [TestMethod] public void CompleteMissingBackgroundSelectionRejectsAndNativeLabelsArePublished()
    {
        var raw=SampleCanonical.Create();raw["background_options"]!["primary_ability"]="";
        Assert.Throws<ArgumentException>(()=>Catalog().Derive(raw,[]));
        raw=SampleCanonical.Create();raw["class"]="Bard";raw["subclass"]="";raw["hp_history"]=new JsonArray();
        var terms=TestStores.Builder.Calculate(TestStores.Builder.Import(raw),null,false).StandingTerms["skill:Arcana"];
        Assert.IsTrue(terms.Any(term=>term.Label.Contains("Jack of All Trades")));
    }
    [TestMethod] public void NativeModifierChangesPreserveOnlyActualWeaponSpecificDamageAdjustments()
    {
        CharacterCalculation Ranger(int dexterity){var raw=SampleCanonical.Create();raw["base_ability_scores"]!["Dexterity"]=dexterity;return TestStores.Builder.Calculate(TestStores.Builder.Import(raw),null,false);}
        var nimble=Ranger(20);var steady=Ranger(14);var change=nimble.Modifiers[Ability.Dexterity]-steady.Modifiers[Ability.Dexterity];Assert.IsGreaterThan(0,change);
        foreach(var attack in nimble.Attacks.Where(a=>a.Ability==Ability.Dexterity))
        {
            var before=steady.Attacks.Single(a=>a.Id==attack.Id);
            Assert.AreEqual(before.DamageModifier+change,attack.DamageModifier,attack.Name);Assert.AreEqual(before.DamageModifierAdjustment,attack.DamageModifierAdjustment,attack.Name+" keeps only its own damage adjustment.");
        }
        Assert.IsTrue(nimble.Attacks.Any(a=>a.Ability==Ability.Dexterity));
    }
    [TestMethod] public void PreviousProficiencyOwnershipCannotHideMalformedSkillsOrUnboundedLists()
    {
        var raw=SampleCanonical.Create();raw["expertise"]!.AsArray().Add("Hacking");
        raw["folio_native_build_sources"]=new JsonObject{["derivedExpertise"]=new JsonArray("Hacking")};
        Assert.Throws<ArgumentException>(()=>Catalog().Derive(raw,[]));
        raw=SampleCanonical.Create();raw["folio_native_build_sources"]=new JsonObject{["derivedSkills"]=new JsonArray(Enumerable.Repeat("Medicine",19).Select(name=>(JsonNode)JsonValue.Create(name)!).ToArray())};
        Assert.Throws<ArgumentException>(()=>Catalog().Derive(raw,[]));
    }
    [TestMethod] public void NativeDependentCapacityAndEveryCastingSourceRefreshWithoutRefundingSpentUses()
    {
        var raw=SampleCanonical.Create();raw["class"]="Bard";raw["subclass"]="";raw["hp_history"]=new JsonArray();raw["base_ability_scores"]!["Charisma"]=20;raw["background_options"]!["primary_ability"]="Charisma";
        var source=SampleWorkspace.Create().Character with{Resources=[new(){Id="bardic-inspiration",Name="Bardic Inspiration",Maximum=6,Remaining=2,SourceKind="build"}]};
        var calculation=TestStores.Builder.Calculate(TestStores.Builder.Import(raw),source,false);
        Assert.AreEqual(5,calculation.DerivedResources["bardic_inspiration_uses"].Number);
        Assert.AreEqual(16,calculation.CastingSources.Single(s=>s.SourceType=="class").SpellSaveDc);
        var typed=WorkspaceProjection.FromCalculation(calculation,source);
        Assert.AreEqual(1,typed.Resources.Single(pool=>pool.Id=="bardic-inspiration").Remaining,"Spent uses stay spent when the maximum changes.");
    }
    [TestMethod] public void StructuredWeaponAbilityCandidatesChooseHighestNativeModifierAfterScoreCaps()
    {
        var raw=SampleCanonical.Create();raw["background"]="Soldier";raw["background_options"]!["primary_ability"]="Strength";raw["background_options"]!["secondary_ability"]="Constitution";
        raw["general_feats"]=new JsonArray();raw["ability_score_adjustments"]=new JsonArray();
        raw["base_ability_scores"]!["Strength"]=20;raw["base_ability_scores"]!["Dexterity"]=22;
        var calculation=TestStores.Builder.Calculate(TestStores.Builder.Import(raw),null,false);
        Assert.IsGreaterThan(calculation.Modifiers[Ability.Strength],calculation.Modifiers[Ability.Dexterity],"Strength stops at its cap of 20.");
        var finesse=calculation.Attacks.Where(a=>a.AbilityCandidates.SequenceEqual([Ability.Strength,Ability.Dexterity])).ToArray();
        Assert.IsNotEmpty(finesse);Assert.IsTrue(finesse.All(a=>a.Ability==Ability.Dexterity));
    }
    [TestMethod] public void UnresolvedKnownRogueExpertiseIsNotPromotedToManualOwnership()
    {
        var raw=SampleCanonical.Create();raw["class"]="Rogue";raw["subclass"]="";raw["general_feats"]=new JsonArray();raw["ability_score_adjustments"]=new JsonArray();
        raw["class_options"]!["expertise"]=new JsonArray("Arcana","History");raw["expertise"]=new JsonArray("Arcana","History");
        var draft=Catalog().Derive(raw,[],true);
        Assert.IsTrue(draft.Issues.Count>0);Assert.AreEqual(0,draft.ManualSources.Expertise.Count);
        Assert.IsFalse(draft.Standing.Expertise.Contains("Arcana"));
    }
    [TestMethod] public void TransientSkillExpertOrphanStaysEditableWithoutBecomingManual()
    {
        var raw=SampleCanonical.Create();raw["class"]="Rogue";raw["subclass"]="";
        raw["class_options"]!["class_skills"]=new JsonArray("Athletics","Nature","Survival");
        raw["general_feats"]![0]!["choices"]!["skill_proficiency"]="Medicine";
        raw["general_feats"]![0]!["choices"]!["expertise"]="Arcana";
        raw["expertise"]=new JsonArray("Arcana");
        var original=raw.ToJsonString();
        var draft=Catalog().Derive(raw,[],true);
        Assert.IsTrue(draft.Issues.Any(issue=>issue.Contains("Skill Expert")));
        Assert.IsFalse(draft.Standing.Expertise.Contains("Arcana"));Assert.IsFalse(draft.ManualSources.Expertise.Contains("Arcana"));
        Assert.AreEqual(original,raw.ToJsonString());
        Assert.Throws<ArgumentException>(()=>Catalog().Derive(raw,[]));
    }
    [TestMethod] public void AcceptedClassAndSubclassAliasesAreCanonicalBeforeDownstreamStages()
    {
        var raw=SampleCanonical.Create();raw["class"]="sorcerer";raw["subclass"]="draconic";
        raw["general_feats"]=new JsonArray();raw["ability_score_adjustments"]=new JsonArray();
        raw["class_levels"]=new JsonObject{["Sorcerer"]=5};raw["hp_history"]=new JsonArray();
        var calculation=TestStores.Builder.Calculate(TestStores.Builder.Import(raw),null,false);
        Assert.AreEqual("Sorcerer",calculation.Build.ClassName);
        Assert.AreEqual("Draconic Sorcery",calculation.Build.Subclass);
        var hp=HitPointRules.Calculate(CanonicalFixtures.HitPoints(BuilderDocuments.Document(calculation),6,2));
        Assert.AreEqual(37,hp.Maximum);
    }
    [TestMethod] public void ActualRepeatableElementalAdeptCatalogInstancesPreserveSeparateScoreSources()
    {
        var root=new DirectoryInfo(Environment.CurrentDirectory);while(root is not null&&!Directory.Exists(Path.Combine(root.FullName,"data/imported/5etools")))root=root.Parent;
        var feats=JsonNode.Parse(File.ReadAllText(Path.Combine(root!.FullName,"data/imported/5etools/feats.json")))!.AsArray();
        var definition=feats.OfType<JsonObject>().Single(feat=>feat["name"]!.GetValue<string>()=="Elemental Adept");
        Assert.IsTrue(definition["repeatable"]!.GetValue<bool>());
        var ability=definition["ability_increase"]!["from"]![0]!.GetValue<string>();
        var amount=definition["ability_increase"]!["amount"]!.GetValue<int>();
        var raw=SampleCanonical.Create();raw["general_feats"]=new JsonArray();raw["ability_score_adjustments"]=new JsonArray();
        foreach(var damage in new[]{"Fire","Cold"})
        {
            var instance="elemental-"+damage;
            raw["general_feats"]!.AsArray().Add(new JsonObject{["instance_id"]=instance,["feat_id"]=definition["id"]!.DeepClone(),["name"]="Elemental Adept",["repeatable"]=true,["choices"]=new JsonObject{["ability"]=ability,["damage_type"]=damage}});
            raw["ability_score_adjustments"]!.AsArray().Add(new JsonObject{["source_type"]="feat",["source_feat"]=definition["id"]!.DeepClone(),["feat_instance_id"]=instance,["source_name"]="Elemental Adept",["adjustments"]=new JsonObject{[ability]=amount},["maximum"]=definition["ability_increase"]!["maximum"]!.DeepClone()});
        }
        var original=raw.ToJsonString();var summary=Catalog().Derive(raw,[]);
        Assert.AreEqual(raw["base_ability_scores"]![ability]!.GetValue<int>()+2*amount,summary.Scores.Scores[Enum.Parse<Ability>(ability)]);
        Assert.AreEqual(1,summary.Standing.UnmodeledStandingFeats.Count(name=>name=="Elemental Adept"));
        Assert.AreEqual(original,raw.ToJsonString());
        var settled=TestStores.Builder.Calculate(TestStores.Builder.Import(raw),null,false).Build;
        Assert.AreEqual(2,settled.Feats!.Count);
        Assert.AreEqual(2,settled.AbilityScoreAdjustments!.Count);
    }
    [TestMethod] public void NativeBuildExposesPerDefinitionProficiencyOwnershipAndDetachedChoiceContext()
    {
        var raw=SampleCanonical.Create();var summary=Catalog().Derive(raw,[]);
        Assert.IsTrue(summary.ProficiencySources.Any(source=>source.SourceId=="class_options:ranger_deft_explorer_expertise"&&source.Kind==ProficiencySourceKind.Expertise&&source.Target=="Perception"));
        Assert.IsTrue(summary.ProficiencySources.Any(source=>source.SourceId=="feat:sample-skill-expert"&&source.Kind==ProficiencySourceKind.Expertise&&source.Target=="Stealth"));
        Assert.IsTrue(summary.ProficiencySources.Any(source=>source.SourceId=="class:Ranger"&&source.Kind==ProficiencySourceKind.Save&&source.Target=="Strength"));
        var context=CanonicalFixtures.ChoiceContext(raw,summary);
        context.ClassOptions["ranger_languages"].Items!.Clear();Assert.AreEqual(2,raw["class_options"]!["ranger_languages"]!.AsArray().Count);
        Assert.AreEqual(summary.ProficiencySources.Count,context.ProficiencySources.Count);
    }
    [TestMethod] public void ChoiceContextDoesNotAuthorizeCantripsFromInactiveParentsOrForgedPactLabels()
    {
        var raw=SampleCanonical.Create();raw["class_options"]!["fighting_style"]="Druidic Warrior";raw["class_options"]!["druidic_warrior_cantrips"]=new JsonArray("Shillelagh");
        var build=Catalog().Derive(raw,[]);
        Assert.IsTrue(CanonicalFixtures.ChoiceContext(raw,build).CantripSources.Any(source=>source.SpellName=="Shillelagh"));
        raw["class_options"]!["fighting_style"]="Archery";
        Assert.IsFalse(CanonicalFixtures.ChoiceContext(raw,build).CantripSources.Any(source=>source.SpellName=="Shillelagh"));
        raw["class"]="Warlock";raw["subclass"]="";raw["class_options"]!["extra_cantrips"]=new JsonArray("Sacred Flame");
        raw["spell_grant_details"]=new JsonArray(new JsonObject{["name"]="Fire Bolt",["source"]=new JsonObject{["type"]="feat",["source_name"]="Pact of the Tome"}});
        build=Catalog().Derive(raw,[]);
        var context=CanonicalFixtures.ChoiceContext(raw,build);
        Assert.IsFalse(context.CantripSources.Any(source=>source.SpellName=="Sacred Flame"));
        Assert.IsFalse(context.CantripSources.Any(source=>source.SpellName=="Fire Bolt"&&source.CountsAsClasses.Contains("Warlock")));
        raw["class_options"]!["eldritch_invocations"]=new JsonArray("Pact of the Tome");raw["spell_grant_details"]![0]!["source"]!["type"]="invocation";
        Assert.IsTrue(CanonicalFixtures.ChoiceContext(raw,build).CantripSources.Any(source=>source.SpellName=="Fire Bolt"&&source.CountsAsClasses.Contains("Warlock")));
    }
}
