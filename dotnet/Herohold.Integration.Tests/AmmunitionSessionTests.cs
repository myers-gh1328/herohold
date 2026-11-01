using Herohold.Core;
using Herohold.TestSupport;
using Herohold.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;

[TestCategory("RulesData")]
[TestClass]
public sealed class AmmunitionSessionTests
{
    private string folder = "";
    [TestInitialize] public void Setup() { folder = Path.Combine(Path.GetTempPath(), "herohold-ammunition-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); }
    [TestCleanup] public void Cleanup() { SqliteConnection.ClearAllPools(); if (Directory.Exists(folder)) Directory.Delete(folder, true); }

    [TestMethod]
    public async Task CountedLongbowShotsSpendArrowsAndHalfComeBackAfterTheFight()
    {
        var store = TestStores.Open(folder);
        var sample = SampleWorkspace.Create().Character;
        await store.SaveAsync(new WorkspaceSnapshot { Character = sample }, 0);
        var session = new WorkspaceSession(store); await session.SwitchCharacterAsync(sample.Id);
        Assert.AreEqual("Arrow", session.Character!.Derived.Attacks.Single(a => a.Name == "Longbow").Ammunition);
        int Arrows() => AmmunitionRules.Carried(session.Character!, "Arrow");
        Assert.AreEqual(20, Arrows()); Assert.IsFalse(session.Character.Sheet.CountAmmunition, "Ammunition is not counted until the player asks.");

        await session.SetAmmunitionCountingAsync(true, sample.Id); Assert.IsNull(session.Error, session.Error);
        for (var shot = 0; shot < 3; shot++)
        {
            await session.RollAsync(new RollRequest { OperationId = "shot-" + shot, SourceCharacterId = sample.Id, SourceRevision = session.Snapshot!.Revision, Kind = RollKind.Attack, AttackKind = AttackKind.Weapon,
                Ability = Ability.Dexterity, Proficient = true, Mode = DiceMode.Physical, PhysicalD20 = [5], CombatContext = CombatRules.Context(session.Character!, "attack:longbow") });
            Assert.IsNull(session.Error, session.Error);
            await session.ResolveAttackAsync(new AttackResolutionRequest { OperationId = "miss-" + shot, SourceCharacterId = sample.Id, SourceRevision = session.Snapshot!.Revision, RollId = session.Character!.Play.LastRoll!.Id, Hit = false });
            Assert.IsNull(session.Error, session.Error);
        }
        Assert.AreEqual(17, Arrows());
        await session.ReloadAsync();
        Assert.IsTrue(session.Character!.Sheet.CountAmmunition); Assert.AreEqual(3, session.Character.Sheet.AmmunitionFired["Arrow"]); Assert.AreEqual(17, Arrows());

        await session.RecoverAmmunitionAsync("Arrow", sample.Id); Assert.IsNull(session.Error, session.Error);
        await session.ReloadAsync();
        Assert.AreEqual(18, Arrows(), "Half of three fired arrows, rounded down, is one."); Assert.AreEqual(0, session.Character!.Sheet.AmmunitionFired.Count);
    }
}
