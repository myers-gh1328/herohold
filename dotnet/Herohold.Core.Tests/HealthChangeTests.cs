using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;

[TestClass]
public sealed class HealthChangeTests
{
    [TestMethod]
    [DataRow(HealthChangeKind.Damage,3,32,5,32,2)]
    [DataRow(HealthChangeKind.Damage,9,32,5,28,0)]
    [DataRow(HealthChangeKind.Damage,100,32,5,0,0)]
    [DataRow(HealthChangeKind.Healing,7,32,5,39,5)]
    [DataRow(HealthChangeKind.Healing,100,32,5,44,5)]
    [DataRow(HealthChangeKind.TemporaryHp,7,32,5,32,7)]
    [DataRow(HealthChangeKind.TemporaryHp,2,32,5,32,2)]
    [DataRow(HealthChangeKind.TemporaryHp,0,32,5,32,0)]
    public void ExplicitHealthAmountsRespectDamageHealingAndNonStackingTemporaryHp(HealthChangeKind kind,int amount,int hp,int temporary,int expectedHp,int expectedTemporary)
    {
        var original=Fixtures.Ranger() with{CurrentHp=hp,TemporaryHp=temporary};
        var request=new HealthChangeRequest{OperationId="health-change",SourceCharacterId=original.Id,SourceRevision=4,Kind=kind,Amount=amount};
        var result=PlayRules.ChangeHealth(new(){Character=original,Revision=4},request);
        Assert.AreEqual(expectedHp,result.CurrentHp);Assert.AreEqual(expectedTemporary,result.TemporaryHp);
        Assert.AreEqual(hp,original.CurrentHp);Assert.AreEqual(temporary,original.TemporaryHp);Assert.AreEqual(1,result.Play.UndoHistory.Count);
        var retry=PlayRules.ChangeHealth(new(){Character=result,Revision=5},request);Assert.AreEqual(expectedHp,retry.CurrentHp);Assert.AreEqual(expectedTemporary,retry.TemporaryHp);Assert.AreEqual(1,retry.Play.UndoHistory.Count);
        var undo=PlayRules.Undo(new(){Character=result,Revision=5},new(){OperationId="undo-health",SourceCharacterId=result.Id,SourceRevision=5});Assert.AreEqual(hp,undo.CurrentHp);Assert.AreEqual(temporary,undo.TemporaryHp);
    }
    [TestMethod]
    public void DamageAtZeroHpClearsConcentrationAndUndoRestoresItWithBothHpPools()
    {
        var original=Fixtures.Ranger() with{CurrentHp=2,TemporaryHp=3,Effects=[new(){Id="mark",Name="Hunter’s Mark",Source="spell:hunters-mark",Concentration=true}],Play=new(){HunterTarget="Quarry"}};
        var result=PlayRules.ChangeHealth(new(){Character=original,Revision=1},new(){OperationId="knocked-out",SourceCharacterId=original.Id,SourceRevision=1,Kind=HealthChangeKind.Damage,Amount=6});
        Assert.AreEqual(0,result.CurrentHp);Assert.AreEqual(0,result.TemporaryHp);Assert.AreEqual(0,result.Effects.Count);Assert.AreEqual("",result.Play.HunterTarget);
        var undo=PlayRules.Undo(new(){Character=result,Revision=2},new(){OperationId="undo-damage",SourceCharacterId=result.Id,SourceRevision=2});
        Assert.AreEqual(2,undo.CurrentHp);Assert.AreEqual(3,undo.TemporaryHp);Assert.AreEqual("Quarry",undo.Play.HunterTarget);Assert.IsTrue(undo.Effects.Single().Concentration);
    }
    [TestMethod]
    public void InvalidAmountsOrStaleCharacterActionsCannotChangeHealth()
    {
        var c=Fixtures.Ranger();var w=new WorkspaceSnapshot{Character=c,Revision=7};var r=new HealthChangeRequest{OperationId="health-guard",SourceCharacterId=c.Id,SourceRevision=7,Kind=HealthChangeKind.Damage,Amount=1};
        foreach(var amount in new[]{0,-1,100001})Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>PlayRules.ChangeHealth(w,r with{Amount=amount}));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>PlayRules.ChangeHealth(w,r with{Kind=(HealthChangeKind)999}));
        Assert.ThrowsExactly<WorkspaceConflictException>(()=>PlayRules.ChangeHealth(w,r with{SourceRevision=6}));
        Assert.ThrowsExactly<WorkspaceConflictException>(()=>PlayRules.ChangeHealth(w,r with{SourceCharacterId="another-character"}));
        Assert.AreEqual(32,c.CurrentHp);
    }
    [TestMethod]
    public void AdvancingOwnTurnExplainsAnUndoableGuardedExpiryAndResetsTurnUse()
    {
        var c=Fixtures.Ranger() with{Effects=[new(){Id="bless",Name="Bless",RemainingRounds=1},new(){Id="long",Name="Long effect",RemainingRounds=3}],Play=new(){DreadUsedThisTurn=true}};
        var request=new AdvanceOwnTurnRequest{OperationId="my-next-turn",SourceCharacterId=c.Id,SourceRevision=3};
        var advanced=PlayRules.AdvanceOwnTurn(new(){Character=c,Revision=3},request);Assert.AreEqual(c.Turn+1,advanced.Turn);Assert.IsFalse(advanced.Play.DreadUsedThisTurn);Assert.AreEqual(1,advanced.Effects.Count);Assert.AreEqual(2,advanced.Effects.Single().RemainingRounds);
        var retry=PlayRules.AdvanceOwnTurn(new(){Character=advanced,Revision=4},request);Assert.AreEqual(advanced.Turn,retry.Turn);
        var undone=PlayRules.Undo(new(){Character=advanced,Revision=4},new(){OperationId="undo-my-turn",SourceCharacterId=c.Id,SourceRevision=4});Assert.AreEqual(c.Turn,undone.Turn);Assert.IsTrue(undone.Play.DreadUsedThisTurn);Assert.AreEqual(2,undone.Effects.Count);
        Assert.ThrowsExactly<WorkspaceConflictException>(()=>PlayRules.AdvanceOwnTurn(new(){Character=c with{Id="other-character"},Revision=3},request));
    }

}
