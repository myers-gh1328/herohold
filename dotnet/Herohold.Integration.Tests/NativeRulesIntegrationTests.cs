using Herohold.TestSupport;
using System.Text.Json.Nodes;
using Herohold.Core;
using Microsoft.Extensions.DependencyInjection;
using Herohold.Rules;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Integration.Tests;

[TestCategory("RulesData")][TestClass] public sealed class NativeRulesIntegrationTests
{
    private static string Root()
    {
        var root=new DirectoryInfo(Environment.CurrentDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"data/imported/5etools/classes.json")))root=root.Parent;
        return root!.FullName;
    }
    /// <summary>A Wizard whose Constitution gives the modifier asked for, with 1 current and 7 temporary HP.</summary>
    private static JsonObject Wizard(int level,int constitution=0)
    {
        var raw=JsonNode.Parse("""{"character_id":"native-wizard","name":"Native wizard","class":"Wizard","level":3,"hit_points":{"maximum":1,"max":1,"current":1,"temporary":7},"spells":{}}""")!.AsObject();
        raw["level"]=level;raw["base_ability_scores"]=new JsonObject(Enum.GetNames<Ability>().Select(a=>KeyValuePair.Create<string,JsonNode?>(a,a=="Constitution"?Math.Max(1,10+2*constitution):10)));
        return raw;
    }
    private static CharacterState Saved(int level,int constitution=0)=>TestStores.Calculated(Wizard(level,constitution));
    [TestMethod] public void NativeHpCorrectionAndClassSlotsReachTypedStateAndCanonicalExport()
    {
        var c=Saved(3,-5);
        Assert.AreEqual(3,c.MaximumHp,"Each level gives at least 1 HP.");Assert.AreEqual(1,c.CurrentHp);Assert.AreEqual(7,c.TemporaryHp);
        Assert.AreEqual(4,c.Resources.Single(r=>r.Id=="slot1").Maximum);
        Assert.AreEqual(2,c.Resources.Single(r=>r.Id=="slot2").Maximum);
        Assert.AreEqual(6,c.Play.Preparation.PreparedLimit);
        Assert.AreEqual(3,WorkspaceProjection.ToCanonical(c)["hit_points"]!["maximum"]!.GetValue<int>());
    }
    [TestMethod] public void ConstitutionItemModifierAppliesOnceAndRecalculationNeverHeals()
    {
        var item=new EquipmentItem{Id="con-charm",Name="Con charm",Equipped=true,ModifierBonuses=new(){{Ability.Constitution,1}}};
        var source=Saved(3) with{CurrentHp=4,TemporaryHp=9,Equipment=[item]};Assert.AreEqual(14,Saved(3).MaximumHp);
        var equipped=TestStores.Workflow.Recalculate(CharacterDraft.Of(source),source);
        Assert.AreEqual(17,equipped.MaximumHp);Assert.AreEqual(4,equipped.CurrentHp);Assert.AreEqual(9,equipped.TemporaryHp);
        var removed=equipped with{Equipment=[item with{Equipped=false}]};
        var final=TestStores.Workflow.Recalculate(CharacterDraft.Of(removed),removed);
        Assert.AreEqual(14,final.MaximumHp);Assert.AreEqual(4,final.CurrentHp);
    }
    [TestMethod] public void GainedHpUsesCorrectedNativeSourceMaximumAndRequiresOptIn()
    {
        var source=Saved(2,-5) with{CurrentHp=1};Assert.AreEqual(2,source.MaximumHp);
        var incoming=TestStores.Builder.Import(Wizard(3,-5));
        Assert.AreEqual(1,TestStores.Builder.Calculate(incoming,source,false).CurrentHp);
        Assert.AreEqual(2,TestStores.Builder.Calculate(incoming,source,true).CurrentHp);
    }
    [TestMethod] public void NativeGainedHpUpdatesAuditHistoryWithoutChangingUnmodifiedRolledDie()
    {
        var source=Saved(2,-5) with{CurrentHp=1};
        var raw=Wizard(3,-5);
        raw["level_history"]=new JsonArray(new JsonObject{["class"]="Wizard",["to_level"]=3,["hp_increase"]=0});
        raw["hp_rolls"]=new JsonArray(new JsonObject{["level"]=3,["value"]=0,["mode"]="rolled"});
        raw["hp_history"]=new JsonArray(new JsonObject{["character_level"]=3,["class_name"]="Wizard",["method"]="rolled",["base_gain"]=2});
        var build=TestStores.Builder.Calculate(TestStores.Builder.Import(raw),source,false).Build;
        Assert.AreEqual(1,build.LevelHistory![0].HpIncrease);
        Assert.AreEqual(1,build.HpRolls![0].Value);
        Assert.AreEqual(2,build.HpHistory![2].BaseGain);
    }
    [TestMethod] public async Task SessionUsesNativeHpForDraftCommitPersistenceAndExport()
    {
        var path=Path.Combine(Path.GetTempPath(),"herohold-native-"+Guid.NewGuid().ToString("N"));
        try
        {
            // The upload claims 99 maximum HP; the rules give a Constitution 1 Fighter 10 - 5 = 5, and keep its current 3.
            var raw=NativeCharacterBuilderTests.Fighter();raw["base_ability_scores"]=new JsonObject();
            foreach(var ability in Enum.GetValues<Ability>())raw["base_ability_scores"]![ability.ToString()]=ability==Ability.Constitution?1:10;
            raw["hit_points"]=new JsonObject{["maximum"]=99,["max"]=99,["current"]=3,["temporary"]=0};
            var repo=TestStores.Open(path);var session=new WorkspaceSession(repo,TestStores.Workflow);
            await session.InitializeAsync();await session.ReviewUploadAsync("minimum.json",System.Text.Encoding.UTF8.GetBytes(raw.ToJsonString()));
            Assert.IsNull(session.Error);Assert.AreEqual(5,session.Workflow!.Candidate.MaximumHp);
            await session.CommitWorkflowAsync();Assert.IsNull(session.Error);
            Assert.AreEqual(5,session.Character!.MaximumHp);Assert.AreEqual(3,session.Character.CurrentHp);
            var persisted=await repo.LoadAsync(session.Character.Id);Assert.AreEqual(5,persisted!.Character.MaximumHp);Assert.AreEqual(3,persisted.Character.CurrentHp);
            Assert.AreEqual(5,WorkspaceProjection.ToCanonical(persisted.Character)["hit_points"]!["maximum"]!.GetValue<int>());
        }
        finally{if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [TestMethod] public async Task ActualJsonExportBytesPreserveNativeMinimumHpCorrection()
    {
        var path=Path.Combine(Path.GetTempPath(),"herohold-native-export-"+Guid.NewGuid().ToString("N"));
        try
        {
            var raw=Wizard(3,-5);raw["base_ability_scores"]=new JsonObject();
            foreach(var ability in Enum.GetValues<Ability>())raw["base_ability_scores"]![ability.ToString()]=ability==Ability.Constitution?1:10;
            var c=TestStores.Calculated(raw);
            Assert.AreEqual(3,c.MaximumHp);
            var repo=TestStores.Open(path);await repo.SaveAsync(new(){Character=c},0);
            var session=new WorkspaceSession(repo,TestStores.Workflow);await session.InitializeAsync();await session.SwitchCharacterAsync(c.Id);
            var artifact=await session.ExportAsync("json");Assert.IsNull(session.Error);Assert.IsNotNull(artifact);
            var exported=JsonNode.Parse(artifact.Bytes)!;
            Assert.AreEqual(3,exported["hit_points"]!["maximum"]!.GetValue<int>());
            Assert.AreEqual(c.CurrentHp,exported["hit_points"]!["current"]!.GetValue<int>());
            Assert.AreEqual(c.TemporaryHp,exported["hit_points"]!["temporary"]!.GetValue<int>());
        }
        finally{if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [TestMethod] public async Task ActualInventoryBridgeChangesConstitutionHpOnceWithoutHealing()
    {
        var path=Path.Combine(Path.GetTempPath(),"herohold-native-item-"+Guid.NewGuid().ToString("N"));
        try
        {
            var session=new WorkspaceSession(TestStores.Open(path),TestStores.Workflow);await session.InitializeAsync();
            await session.AddEquipmentAsync(new(){Id="con-charm",Name="Con charm",Equipped=true,ModifierBonuses=new(){{Ability.Constitution,1}}});
            Assert.IsNull(session.Error);Assert.AreEqual(49,session.Character!.MaximumHp);Assert.AreEqual(32,session.Character.CurrentHp);
            await session.RemoveEquipmentAsync("con-charm");
            Assert.IsNull(session.Error);Assert.AreEqual(44,session.Character!.MaximumHp);Assert.AreEqual(32,session.Character.CurrentHp);
        }
        finally{if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [TestMethod] public void ImportObservedHpSurvivesLegacyClampAndTypedSourceAlwaysWins()
    {
        var draft=TestStores.Builder.Import(Wizard(3,-5));
        draft=draft with{Facts=draft.Facts! with{CurrentHp=3,TemporaryHp=8}};
        var imported=TestStores.Builder.Calculate(draft,null,false);
        Assert.AreEqual(3,imported.CurrentHp);Assert.AreEqual(8,imported.TemporaryHp);
        var source=Saved(3,-5) with{CurrentHp=1,TemporaryHp=2};
        var withSource=TestStores.Builder.Calculate(draft,source,false);
        Assert.AreEqual(1,withSource.CurrentHp);Assert.AreEqual(2,withSource.TemporaryHp);
    }
}
