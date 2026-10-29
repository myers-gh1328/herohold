using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;

[TestClass]
public sealed class CombatUsabilityTests
{
    [TestMethod]
    public void CombatStatusExposesDirectHealthActionsAndExplainsOwnTurnWithoutChangingState()
    {
        using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;
        var c=SampleWorkspace.Create().Character with{Effects=[new(){Id="bless",Name="Bless",RemainingRounds=1}]};
        var actions=new List<string>();var cut=context.Render<CombatStatus>(p=>p.Add(x=>x.Character,c).Add(x=>x.OnDamage,()=>actions.Add("damage")).Add(x=>x.OnHeal,()=>actions.Add("heal")).Add(x=>x.OnTemporaryHp,()=>actions.Add("temp")).Add(x=>x.OnAdvanceTurn,()=>actions.Add("turn")));
        cut.Find("[data-testid='combat-damage']").Click();cut.Find("[data-testid='combat-heal']").Click();cut.Find("[data-testid='combat-temp']").Click();cut.Find("[data-testid='next-turn']").Click();
        CollectionAssert.AreEqual(new[]{"damage","heal","temp","turn"},actions);StringAssert.Contains(cut.Markup,"Advance my turn");StringAssert.Contains(cut.Markup,"once-per-turn");StringAssert.Contains(cut.Markup,"Bless");Assert.AreEqual(32,c.CurrentHp);
        cut.Render(p=>p.Add(x=>x.Busy,true));Assert.IsTrue(cut.FindAll("[data-testid='combat-damage'],[data-testid='combat-heal'],[data-testid='combat-temp'],[data-testid='combat-initiative'],[data-testid='next-turn']").All(x=>x.HasAttribute("disabled")));
    }
    [TestMethod]
    public void EveryCounterHasImmediateCompactControlsIncludingLaterPools()
    {
        using var context=new BunitContext();var c=SampleWorkspace.Create().Character;
        (string Id,int Delta,string SourceId)? action=null;var cut=context.Render<ResourceCounters>(p=>p.Add(x=>x.Character,c).Add(x=>x.OnAdjust,v=>action=v));
        Assert.AreEqual(0,cut.FindAll("h2,details").Count);Assert.AreEqual(c.Resources.Count,cut.FindAll(".resource-counter").Count);
        var last=c.Resources.Last();cut.Find($"button[aria-label='Use one {last.Name}']").Click();Assert.AreEqual((last.Id,-1,c.Id),action!.Value);
        foreach(var resource in c.Resources)Assert.AreEqual(resource.Remaining.ToString(),cut.Find($"[data-testid='resource-{resource.Id}']").TextContent);
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task HealthDialogDamagePreviewAndConfirmationUseTemporaryHpFirstAndPersistOneUndo()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-health-ui-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repository=TestStores.Open(directory);var c=SampleWorkspace.Create().Character with{Id="health-ui",TemporaryHp=5};await repository.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);
            using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;var cut=context.Render<HealthDialog>(p=>p.Add(x=>x.Character,session.Character!).Add(x=>x.Session,session));
            await cut.InvokeAsync(()=>cut.Instance.OpenAsync(HealthChangeKind.Damage));cut.Find("#health-amount").Input("9");
            Assert.IsTrue(cut.Find("#health-amount").HasAttribute("autofocus"),"Opening an HP change should focus the amount to enter.");
            StringAssert.Contains(cut.Find("[data-testid='health-preview']").TextContent,"28 / 44");Assert.AreEqual(32,session.Character!.CurrentHp);
            cut.Find("[data-testid='confirm-health']").Click();cut.WaitForAssertion(()=>Assert.AreEqual(28,session.Character!.CurrentHp));Assert.AreEqual(0,session.Character!.TemporaryHp);Assert.AreEqual(1,session.Character.Play.UndoHistory.Count);
            await session.ReloadAsync();Assert.AreEqual(28,session.Character!.CurrentHp);
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow(HealthChangeKind.Healing,100,44,5)]
    [DataRow(HealthChangeKind.TemporaryHp,2,32,2)]
    [DataRow(HealthChangeKind.TemporaryHp,0,32,0)]
    public async Task PrefilledHealthChangesRequireExplicitConfirmation(HealthChangeKind kind,int amount,int expectedHp,int expectedTemporary)
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-health-prefill-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repository=TestStores.Open(directory);var c=SampleWorkspace.Create().Character with{Id="health-prefill",TemporaryHp=5};await repository.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);
            using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;var cut=context.Render<HealthDialog>(p=>p.Add(x=>x.Character,session.Character!).Add(x=>x.Session,session));
            await cut.InvokeAsync(()=>cut.Instance.OpenAsync(kind,amount));
            Assert.AreEqual(amount.ToString(),cut.Find("#health-amount").GetAttribute("value"));Assert.AreEqual(32,session.Character!.CurrentHp);Assert.AreEqual(5,session.Character.TemporaryHp);Assert.AreEqual(0,session.Character.Play.UndoHistory.Count);
            cut.Find("[data-testid='confirm-health']").Click();cut.WaitForAssertion(()=>Assert.AreEqual(1,session.Character!.Play.UndoHistory.Count));Assert.AreEqual(expectedHp,session.Character!.CurrentHp);Assert.AreEqual(expectedTemporary,session.Character.TemporaryHp);
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task HealthDialogCannotApplyAnOpenCharactersDamageAfterSwitchingCharacters()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-health-source-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repository=TestStores.Open(directory);var first=SampleWorkspace.Create().Character with{Id="health-first"};var second=first with{Id="health-second"};await repository.SaveAsync(new(){Character=first},0);await repository.SaveAsync(new(){Character=second},0);var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(first.Id);
            using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;var cut=context.Render<HealthDialog>(p=>p.Add(x=>x.Character,session.Character!).Add(x=>x.Session,session));await cut.InvokeAsync(()=>cut.Instance.OpenAsync(HealthChangeKind.Damage,9));
            await session.SwitchCharacterAsync(second.Id);cut.Find("[data-testid='confirm-health']").Click();cut.WaitForAssertion(()=>Assert.IsNotNull(session.Error));Assert.AreEqual(second.Id,session.Character!.Id);Assert.AreEqual(32,session.Character.CurrentHp);Assert.AreEqual(0,session.Character.Play.UndoHistory.Count);
            await session.SwitchCharacterAsync(first.Id);Assert.AreEqual(32,session.Character!.CurrentHp);
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
