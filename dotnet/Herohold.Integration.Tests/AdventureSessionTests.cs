using Herohold.TestSupport;
using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestCategory("RulesData")]
[TestClass]
public sealed class AdventureSessionTests
{
    [TestMethod]
    public async Task TravelCompanionsAndItemChargesSurviveCanonicalExportAndStaleEditorsCannotWrite()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-adventure-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repo=TestStores.Open(directory);var session=new WorkspaceSession(repo);await session.InitializeAsync();var id=session.Character!.Id;
            await session.AddEquipmentAsync(new(){Id="charge-test",Name="Wand",UnitWeightLb=1.5m,Charges=new(){Maximum=3,Remaining=2,RecoveryNote="At dawn"},UseEffects=[new(){Name="Light",RemainingRounds=3}]});
            await session.UpdateAdventureAsync(new(){Movement=[new(){Kind="Fly",Speed=40,Source="Spell"}],Companions=[new(){Id="owl",Name="Owl",MaximumHp=4,CurrentHp=4,Actions=[new(){Name="Help",Rules="Use actual familiar rules"}],Resources=[new(){Id="ward",Name="Ward",Maximum=1,Remaining=1}]}]},id);
            await session.UseEquipmentAsync("charge-test",id);await session.ReloadAsync();Assert.IsNull(session.Error);Assert.AreEqual(1,session.Character!.Equipment.Single(i=>i.Id=="charge-test").Charges!.Remaining);
            var raw=WorkspaceProjection.ToCanonical(session.Character);var restored=TestStores.Restored(raw);Assert.AreEqual("Owl",restored.Sheet.Adventure.Companions.Single().Name);Assert.AreEqual("Help",restored.Sheet.Adventure.Companions.Single().Actions.Single().Name);Assert.AreEqual(40,restored.Sheet.Adventure.Movement.Single().Speed);Assert.AreEqual(1.5m,restored.Equipment.Single(i=>i.Id=="charge-test").UnitWeightLb);
            var revision=session.Snapshot!.Revision;await session.UpdateAdventureAsync(new(),"wrong-character");Assert.IsNotNull(session.Error);Assert.AreEqual(revision,session.Snapshot.Revision);Assert.AreEqual(1,session.Character.Sheet.Adventure.Companions.Count);
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
