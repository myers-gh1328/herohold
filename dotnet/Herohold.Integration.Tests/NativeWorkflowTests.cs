using Herohold.TestSupport;
using Herohold.Core;
using Herohold.Rules;
using Herohold.Documents;
using Herohold.Web.Services;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestCategory("RulesData")][TestClass] public sealed class NativeWorkflowTests
{
    private sealed class UnusedPdf:IPdfService
    {
        public Task<PdfInspection> InspectAsync(byte[] bytes,CancellationToken ct=default)=>throw new AssertFailedException("No PDF operation was requested.");
        public Task<byte[]> ExportAsync(CharacterState c,JsonObject raw,CancellationToken ct=default)=>throw new AssertFailedException("No PDF export was requested.");
    }
    internal static CharacterRules Workflow()
    {
        var catalog=NativeCatalogIntegrationTests.Snapshot();var classes=ClassProgressionCatalog.From(catalog.ClassRecords);var builder=NativeCharacterBuilderTests.Builder();var edits=new CharacterEditRules(catalog);
        return new(builder,edits,new(catalog,classes,NativeCharacterBuilderTests.Feats(),edits,builder),catalog,new UnusedPdf());
    }
    private static ChoiceValue Value(string json)=>System.Text.Json.JsonSerializer.Deserialize<ChoiceValue>(json)!;
    /// <summary>A fixture character as a saved character.</summary>
    private static CharacterState Saved(CharacterRules rules,JsonObject raw){var draft=rules.ImportJson(raw);Assert.IsTrue(draft.Validation.Complete,string.Join("; ",draft.Validation.Missing));return rules.Adopt(draft.Draft);}
    [TestMethod] public void CreateEditAndCalculateUseTheNativeWorkflowAndDoNotChangeTheInput()
    {
        var workflow=Workflow();var draft=workflow.Create();Assert.IsFalse(draft.Validation.Complete);
        var source=NativeCharacterBuilderTests.Fighter();var before=source.ToJsonString();var result=workflow.Edit(workflow.ImportJson(source.DeepClone().AsObject()).Draft,["notes"],ChoiceValue.Of("Native edit"));
        Assert.IsTrue(result.Validation.Complete);Assert.AreEqual("Native edit",result.Draft.Notes);Assert.AreEqual(before,source.ToJsonString());
        var calculated=workflow.Calculate(result.Draft);Assert.AreEqual(10,calculated.Summary.MaximumHp);
    }
    [TestMethod] public async Task ARealSessionCreatesAndLevelsACharacterWithoutPython()
    {
        var folder=Path.Combine(Path.GetTempPath(),"herohold-native-workflow-"+Guid.NewGuid().ToString("N"));
        try
        {
            var session=new WorkspaceSession(TestStores.Open(folder),Workflow());await session.InitializeAsync();await session.StartCreateAsync();var source=NativeCharacterBuilderTests.Fighter();
            foreach(var key in new[]{"name","class","species","background","origin_languages","class_options","background_options","weapon_masteries"}){await session.EditDraftAsync([key],JsonWorkflowViews.Value(source[key]));Assert.IsNull(session.Error,key+": "+session.Error);}await session.CommitWorkflowAsync();Assert.IsNull(session.Error);Assert.AreEqual(1,session.Character!.Level);Assert.AreEqual(10,session.Character.CurrentHp);
            var id=session.Character.Id;await session.StartLevelUpAsync();Assert.IsNull(session.Error);Assert.AreEqual(6,session.Workflow!.LevelUp!.HpBaseGain);Assert.AreEqual(6,session.Workflow.LevelUp.HpIncrease);await session.CommitWorkflowAsync();Assert.IsNull(session.Error);Assert.AreEqual(id,session.Character.Id);Assert.AreEqual(2,session.Character.Level);Assert.AreEqual(16,session.Character.MaximumHp);Assert.AreEqual(10,session.Character.CurrentHp);
            // Gained HP is added to current HP only when the player asks for it.
            await session.StartLevelUpAsync();var subclass=session.Workflow!.Choices.Single(c=>c.Path.SequenceEqual(["subclass"]));await session.ChooseWorkflowAsync(subclass.Id,ChoiceValue.Of("Champion"));Assert.IsNull(session.Error);
            await session.CommitWorkflowAsync(new(){Mode="fixed",AddGainedHp=true});Assert.IsNull(session.Error);Assert.AreEqual(3,session.Character.Level);Assert.AreEqual(22,session.Character.MaximumHp);Assert.AreEqual(16,session.Character.CurrentHp);
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [TestMethod] public async Task NativeWorkflowRejectsUnknownFormatsAndHonorsCancellation()
    {
        var workflow=Workflow();var c=Saved(workflow,NativeCharacterBuilderTests.Fighter());
        await Assert.ThrowsAsync<ArgumentException>(()=>workflow.ExportAsync(c,"run.command"));
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();await Assert.ThrowsAsync<OperationCanceledException>(()=>workflow.ExportAsync(c,"json",cancellation.Token));
        Assert.Throws<CharacterWorkflowException>(()=>workflow.GenerateAbilityScores(workflow.Create().Draft,"user-code"));
    }
    [TestMethod] public void ExistingFeatRepairUsesStableIdentityAndReachesNativeValidation()
    {
        var workflow=Workflow();var c=NativeCharacterBuilderTests.Fighter();c["level"]=4;c["general_feats"]=new JsonArray(new JsonObject{["instance_id"]="resilient-test",["name"]="Resilient",["feat_id"]="resilient_xphb",["level_taken"]=4,["choices"]=new JsonObject{["ability"]="Wisdom"}});
        var initial=workflow.ImportJson(c.DeepClone().AsObject());
        Assert.IsTrue(initial.Choices.Any(d=>d.Id=="existing_feat:resilient-test"));
        var repaired=workflow.RepairFeat(initial.Draft,"resilient-test",Value("""{"mode":"feat","feat_id":"resilient_xphb","feat_ability_choice":"Charisma","feat_choices":{}}"""));
        Assert.AreEqual("Charisma",repaired.Draft.Build.Feats![0].Choices["ability"].Text);
        Assert.AreEqual("Wisdom",c["general_feats"]![0]!["choices"]!["ability"]!.GetValue<string>());
        Assert.Throws<CharacterWorkflowException>(()=>workflow.RepairFeat(initial.Draft,"resilient-test",Value("""{"feat_id":"tough_xphb"}""")));
        c["general_feats"]![0]!["choices"]!["ability"]="Luck";var broken=workflow.ImportJson(c.DeepClone().AsObject());Assert.IsFalse(broken.Validation.Complete);Assert.IsTrue(broken.Choices.Any(d=>d.Id=="existing_feat:resilient-test"));
        c["general_feats"]![0]!["choices"]!["ability"]="Strength";var invalid=workflow.ImportJson(c.DeepClone().AsObject());Assert.IsFalse(invalid.Validation.Complete,"A Fighter cannot take Resilient for an already proficient Strength save.");
    }

    [TestMethod] public async Task JsonExportUsesFinalStandingNumbersPreservesHpAndRejectsMalformedSnapshots()
    {
        var workflow=Workflow();var raw=NativeCharacterBuilderTests.Fighter();raw["folio_inventory"]=new JsonArray(new JsonObject{["id"]="ring",["name"]="Test Ring",["equipped"]=true,["quantity"]=1,["modifierBonuses"]=new JsonObject{["Wisdom"]=1}});
        var before=raw.ToJsonString();var state=Saved(workflow,raw.DeepClone().AsObject()) with{CurrentHp=3,TemporaryHp=2};Assert.AreEqual(10,state.MaximumHp);
        var roundtrip=JsonNode.Parse(await workflow.ExportAsync(state,"json"))!;Assert.AreEqual(1,roundtrip["ability_modifiers"]!["Wisdom"]!.GetValue<int>());Assert.AreEqual(3,roundtrip["hit_points"]!["current"]!.GetValue<int>());Assert.AreEqual(2,roundtrip["hit_points"]!["temporary"]!.GetValue<int>());Assert.AreEqual(before,raw.ToJsonString());
        var stats=NativeExportProjection.Create(state);var canonical=roundtrip.AsObject();
        var invalid=stats.DeepClone().AsObject();invalid["ability_modifiers"]!["Wisdom"]=999999;Assert.Throws<CharacterWorkflowException>(()=>NativeExportOverlay.Apply(canonical,invalid));
        invalid=stats.DeepClone().AsObject();invalid["extra_field"]=true;Assert.Throws<CharacterWorkflowException>(()=>NativeExportOverlay.Apply(canonical,invalid));
        await Assert.ThrowsAsync<ArgumentException>(()=>workflow.ExportAsync(state with{CurrentHp=11},"json"));
    }

    [TestMethod] public void NativeLevelUpIncludesOriginFeatsAndPreservesOwnedStyleAndOriginRestrictions()
    {
        var workflow=Workflow();var raw=NativeCharacterBuilderTests.Fighter();raw["level"]=3;raw["subclass"]="Champion";var source=Saved(workflow,raw);
        var start=workflow.StartLevelUp(source,4);var asi=start.PendingChoices.Single(c=>c.Type=="asi_or_feat");
        var options=asi.FeatOptions;Assert.IsTrue(options.Single(o=>o.Value.Text=="defense_xphb").Disabled,"The class already owns Defense.");Assert.IsTrue(options.Single(o=>o.Value.Text=="savage_attacker_xphb").Disabled,"Soldier already owns Savage Attacker.");Assert.IsFalse(options.Single(o=>o.Value.Text=="lucky_xphb").Disabled);
        var chosen=workflow.ChooseLevelUp(source,start.Review.Draft,asi.Id,Value("""{"mode":"feat","feat_id":"lucky_xphb","feat_choices":{}}"""));
        chosen=workflow.ChooseLevelUp(source,chosen.Review.Draft,"fighter_4_weapon_mastery",ChoiceValue.ListOf([ChoiceValue.Of("Dagger")]));Assert.IsTrue(chosen.PreviewValidation.Complete,string.Join("; ",chosen.PreviewValidation.Missing));
        var state=workflow.CommitLevelUp(source,chosen.Review.Draft,new());Assert.AreEqual(4,state.Level);Assert.AreEqual(2,state.Resources.Single(r=>r.Id=="luck").Maximum);Assert.AreEqual(0,state.Build!.AbilityScoreAdjustments?.Count??0,"An Origin feat must not add an empty score adjustment.");
    }

    [TestMethod] public void AbilityGenerationIsExplicitAndAssignmentsSurviveRecalculation()
    {
        var workflow=Workflow();var source=NativeCharacterBuilderTests.Fighter();var original=source.ToJsonString();var draft=workflow.ImportJson(source.DeepClone().AsObject()).Draft;
        var generated=workflow.GenerateAbilityScores(draft,"Standard array");Assert.AreEqual(15,generated.Draft.Build.BaseAbilityScores![Ability.Strength]);Assert.AreEqual(original,source.ToJsonString());
        var swapped=workflow.AssignAbilityScore(generated.Draft,Ability.Wisdom,0);Assert.AreEqual(15,swapped.Draft.Build.BaseAbilityScores![Ability.Wisdom]);Assert.AreEqual(10,swapped.Draft.Build.BaseAbilityScores![Ability.Strength]);
        var rolled=workflow.GenerateAbilityScores(draft,"Roll 4d6");var roll=string.Join(",",rolled.Draft.Build.RolledAbilityScores!);var recalculated=workflow.Calculate(rolled.Draft);Assert.AreEqual(roll,string.Join(",",recalculated.Draft.Build.RolledAbilityScores!));
        Assert.Throws<CharacterWorkflowException>(()=>workflow.GenerateAbilityScores(draft,"user-code"));
    }

}
