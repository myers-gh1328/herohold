using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]
public sealed class HitDieCounterTests
{
    internal static CharacterState Character(bool mixed=true)
    {
        var c=Fixtures.Ranger();var pools=new List<HitDiePool>{new(){Id="hd6",Sides=6,Maximum=1,Remaining=1,ClassTrackIds=["wizard"]}};if(mixed)pools.Add(new(){Id="hd10",Sides=10,Maximum=2,Remaining=1,ClassTrackIds=["ranger"]});
        return c with{Play=c.Play with{HitDiePools=pools},Resources=c.Resources.Where(r=>r.Id!="hitdice").Append(new(){Id="hitdice",Name="Hit Dice",Maximum=pools.Sum(p=>p.Maximum),Remaining=pools.Sum(p=>p.Remaining)}).ToList()};
    }
    [TestMethod]
    public void SingleExplicitPoolScalarAdjustmentUpdatesAuthoritativePoolAndUndoTogether()
    {
        var c=Character(false);var w=new WorkspaceSnapshot{Character=c};var next=PlayRules.AdjustResource(w,new(){ResourceId="hitdice",Delta=-1});Assert.AreEqual(0,next.Play.HitDiePools.Single().Remaining);Assert.AreEqual(0,next.Resources.Single(r=>r.Id=="hitdice").Remaining);var undo=PlayRules.Undo(w with{Character=next},new());Assert.AreEqual(1,undo.Play.HitDiePools.Single().Remaining);Assert.AreEqual(1,undo.Resources.Single(r=>r.Id=="hitdice").Remaining);Assert.AreEqual(1,c.Play.HitDiePools.Single().Remaining);Assert.AreEqual(0,WorkspaceRules.AdjustResource(c,"hitdice",-1).Play.HitDiePools.Single().Remaining);
    }
    [TestMethod]
    public void MixedPoolsRejectAmbiguousScalarAndBoundsWithoutMutation()
    {
        var c=Character();var w=new WorkspaceSnapshot{Character=c};Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.AdjustResource(w,new(){ResourceId="hitdice",Delta=-1}));Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>PlayRules.AdjustHitDiePool(w,new(){PoolId="hd6",Delta=-2}));Assert.AreEqual(2,c.Resources.Single(r=>r.Id=="hitdice").Remaining);Assert.ThrowsExactly<ArgumentException>(()=>WorkspaceRules.AdjustResource(c,"hitdice",-1));
    }
    [TestMethod]
    public void PerPoolAdjustmentIsSourceGuardedIdempotentAndUndoPreservesAnotherPoolCorrection()
    {
        var c=Character();var w=new WorkspaceSnapshot{Character=c};var request=new HitDiePoolAdjustmentRequest{PoolId="hd6",Delta=-1};var next=PlayRules.AdjustHitDiePool(w,request);Assert.AreEqual(0,next.Play.HitDiePools[0].Remaining);Assert.AreEqual(1,next.Resources.Single(r=>r.Id=="hitdice").Remaining);var duplicate=PlayRules.AdjustHitDiePool(w with{Character=next},request);Assert.AreEqual(1,duplicate.Play.UndoHistory.Count);
        Assert.ThrowsExactly<WorkspaceConflictException>(()=>PlayRules.AdjustHitDiePool(w,new(){SourceCharacterId="another-character",PoolId="hd6",Delta=-1}));
        var later=next with{Play=next.Play with{HitDiePools=next.Play.HitDiePools.Select(p=>p.Id=="hd10"?p with{Remaining=0}:p).ToList()},Resources=next.Resources.Select(r=>r.Id=="hitdice"?r with{Remaining=0}:r).ToList()};var undone=PlayRules.Undo(w with{Character=later},new());Assert.AreEqual(1,undone.Play.HitDiePools[0].Remaining);Assert.AreEqual(0,undone.Play.HitDiePools[1].Remaining);Assert.AreEqual(1,undone.Resources.Single(r=>r.Id=="hitdice").Remaining);
    }
    [TestMethod]
    public void CountedDownPoolCannotBeSpentAgainByShortRest()
    {
        var c=Character();var adjusted=PlayRules.AdjustHitDiePool(new(){Character=c},new(){PoolId="hd6",Delta=-1});Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.Rest(new(){Character=adjusted},new(){Kind=RestKind.Short,Mode=DiceMode.Physical,HitDieSpends=[new(){PoolId="hd6",Count=1,PhysicalDice=[3]}]},_=>throw new AssertFailedException("Physical dice must not use RNG.")));
    }
}
