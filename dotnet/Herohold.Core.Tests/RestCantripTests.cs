using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]
public sealed class RestCantripTests
{
    private static CharacterState Wizard()=>Fixtures.Ranger() with{ClassName="Wizard",Resources=[new(){Id="slot1",Name="Level1",Maximum=4,Remaining=1,SlotLevel=1}],Play=JsonSerializer.Deserialize<PlayState>("""
    {"knownSpellIds":["fire-bolt","mage-hand","warlock-gift"],"spells":[{"id":"fire-bolt","name":"Fire Bolt","level":0},{"id":"mage-hand","name":"Mage Hand","level":0},{"id":"ray-of-frost","name":"Ray of Frost","level":0},{"id":"warlock-gift","name":"Warlock gift","level":0}],"preparation":{"canChangeCantripsOnLongRest":true,"cantripReplacementLimit":1,"ownedCantripSpellIds":["fire-bolt","mage-hand"],"eligibleCantripSpellIds":["fire-bolt","mage-hand","ray-of-frost"]}}
    """,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!};
    private static RestRequest Request(bool change=true,RestKind kind=RestKind.Long)=>JsonSerializer.Deserialize<RestRequest>($$"""
    {"operationId":"wizard-cantrip-rest","sourceRevision":1,"kind":{{(int)kind}},"changeCantrips":{{change.ToString().ToLowerInvariant()}},"newClassCantripSpellIds":["ray-of-frost","mage-hand"]}
    """,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
    [TestMethod][DataRow("short")][DataRow("not-permitted")][DataRow("foreign-source")][DataRow("wrong-count")][DataRow("duplicate")]
    public void InvalidCantripReplacementNeverPartiallyCompletesRest(string reason)
    {
        var c=Wizard();var r=Request();
        if(reason=="short")r=r with{Kind=RestKind.Short};
        if(reason=="not-permitted")c=c with{Play=c.Play with{Preparation=c.Play.Preparation with{CanChangeCantripsOnLongRest=false}}};
        if(reason=="foreign-source")r=r with{NewClassCantripSpellIds=["ray-of-frost","warlock-gift"]};
        if(reason=="wrong-count")r=r with{NewClassCantripSpellIds=["ray-of-frost"]};
        if(reason=="duplicate")r=r with{NewClassCantripSpellIds=["ray-of-frost","ray-of-frost"]};
        Assert.Throws<ArgumentException>(()=>PlayRules.Rest(new(){Character=c,Revision=1},r));
        Assert.AreEqual(1,c.Resources.Single().Remaining);CollectionAssert.AreEquivalent(new[]{"fire-bolt","mage-hand","warlock-gift"},c.Play.KnownSpellIds);
    }
    [TestMethod]
    public void WizardCantripReplacementIsAnExplicitLongRestChoiceAndPreservesOtherSources()
    {
        var c=Wizard();var w=new WorkspaceSnapshot{Character=c,Revision=1};var unchanged=PlayRules.Rest(w,Request(false));
        CollectionAssert.AreEquivalent(c.Play.KnownSpellIds,unchanged.Play.KnownSpellIds);
        var after=PlayRules.Rest(w,Request());Assert.IsFalse(after.Play.KnownSpellIds.Contains("fire-bolt"));Assert.IsTrue(after.Play.KnownSpellIds.Contains("ray-of-frost"));Assert.IsTrue(after.Play.KnownSpellIds.Contains("warlock-gift"));
        CollectionAssert.AreEquivalent(new[]{"fire-bolt","mage-hand","warlock-gift"},c.Play.KnownSpellIds);
        var undone=PlayRules.Undo(new(){Character=after,Revision=2},new(){OperationId="undo-cantrip",SourceRevision=2});CollectionAssert.AreEquivalent(c.Play.KnownSpellIds,undone.Play.KnownSpellIds);
    }
}
