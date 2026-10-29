using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Pages;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Components.Tests;

[TestClass]
public sealed class NativeItemFactsTests
{
    private string directory="";
    [TestInitialize]public void Setup()=>directory=Path.Combine(Path.GetTempPath(),"herohold-item-facts-"+Guid.NewGuid().ToString("N"));
    [TestCleanup]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    private static EquipmentItem Item()=>new(){Id="source-flask",Name="Warding flask",Consumable=true,Quantity=2,Equipped=true,Defenses=[new(){Type="Fire",Kind="resistance"}],PassiveEffects=[new(){Id="passive-fact",Name="Water breathing",Note="From the flask"}],UseEffects=[new(){Id="used-fact",Name="Warding mist",RemainingRounds=2}]};
    private async Task<WorkspaceSession> Session()
    {
        var c=SampleWorkspace.Create().Character with{Id="item-facts",Build=null,Equipment=[Item()]};var repository=HandMadeStore.At(directory);await repository.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);return session;
    }
    private static BunitContext Context(WorkspaceSession session){var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session);return context;}
    [TestMethod]
    public async Task EditingAnItemPreservesItsExistingSourceFactsAndUsingItAppliesItsOwnEffectOnce()
    {
        var session=await Session();using var context=Context(session);var editor=context.Render<InventoryDefenseDialogs>(p=>p.Add(x=>x.Session,session));await editor.InvokeAsync(()=>editor.Instance.OpenItemAsync(session.Character!.Equipment.Single()));editor.Find("#item-name").Input("Renamed warding flask");
        await editor.FindAll("button").Single(b=>b.TextContent.Trim()=="Save item").ClickAsync(new());var item=session.Character!.Equipment.Single();Assert.AreEqual(1,item.Defenses.Count);Assert.AreEqual(1,item.PassiveEffects.Count);Assert.AreEqual(1,item.UseEffects.Count);
        var home=context.Render<Home>();await home.Find("[data-testid='nav-equipment']").ClickAsync(new());await home.Find("[data-testid='consume-source-flask']").ClickAsync(new());Assert.AreEqual(2,session.Character!.Equipment.Single().Quantity,"Opening item confirmation must not spend it.");await home.Find("[data-testid='confirm-item-use']").ClickAsync(new());
        home.WaitForAssertion(()=>Assert.AreEqual(1,session.Character!.Equipment.Single().Quantity));Assert.AreEqual(1,session.Character!.Effects.Count(e=>e.Name=="Warding mist"));StringAssert.Contains(session.Character.Effects.Single(e=>e.Name=="Warding mist").Source,"item:source-flask");await session.ReloadAsync();Assert.AreEqual(1,session.Character!.Equipment.Single().UseEffects.Count);
    }
    [TestMethod]
    public async Task PassiveItemFactsAreVisibleBySourceAndDisappearOnlyWhenTheirEquipmentBecomesInactive()
    {
        var session=await Session();using var context=Context(session);var home=context.Render<Home>();StringAssert.Contains(home.Find("[data-testid='conditions-panel']").TextContent,"Water breathing");StringAssert.Contains(home.Find("[data-testid='defenses-panel']").TextContent,"Fire");Assert.AreEqual(0,home.FindAll("button[aria-label='End Water breathing']").Count);
        await session.SetEquipmentAsync("source-flask",false,false);home.WaitForAssertion(()=>Assert.IsFalse(home.Find("[data-testid='conditions-panel']").TextContent.Contains("Water breathing")));Assert.IsFalse(home.Find("[data-testid='defenses-panel']").TextContent.Contains("Fire"));
    }
    [TestMethod]
    public async Task ItemEditorStagesGenericDefensesAndOnUseEffectsOnlyWhenTheItemIsSaved()
    {
        var session=await Session();using var context=Context(session);var editor=context.Render<InventoryDefenseDialogs>(p=>p.Add(x=>x.Session,session));await editor.InvokeAsync(()=>editor.Instance.OpenItemAsync(session.Character!.Equipment.Single()));var revision=session.Snapshot!.Revision;
        editor.Find("#item-fact-defense-type").Change("Cold");await editor.Find("[data-testid='stage-item-defense']").ClickAsync(new());editor.Find("#item-fact-effect-name").Input("Freedom of movement");editor.Find("#item-fact-effect-kind").Change("use");await editor.Find("[data-testid='stage-item-effect']").ClickAsync(new());Assert.AreEqual(revision,session.Snapshot.Revision);Assert.AreEqual(1,session.Character!.Equipment.Single().Defenses.Count);
        await editor.FindAll("button").Single(b=>b.TextContent.Trim()=="Save item").ClickAsync(new());Assert.IsNull(session.Error);CollectionAssert.AreEquivalent(new[]{"Fire","Cold"},session.Character!.Equipment.Single().Defenses.Select(x=>x.Type).ToArray());Assert.AreEqual(2,session.Character.Equipment.Single().UseEffects.Count);Assert.AreEqual(1,session.Character.Equipment.Single().PassiveEffects.Count);
    }
    [TestMethod]
    public void PassiveEffectsCannotStartConcentrationAndSwitchingFromOnUseClearsTheChoice()
    {
        using var context=new BunitContext();
        var staged=new EquipmentItem{Id="test",Name="Test item"};
        var editor=context.Render<ItemFactsEditor>(p=>p.Add(x=>x.Value,staged).Add(x=>x.ValueChanged,value=>staged=value));
        Assert.IsTrue(editor.Find("input[type=checkbox]").HasAttribute("disabled"));
        editor.Find("#item-fact-effect-kind").Change("use");
        Assert.IsFalse(editor.Find("input[type=checkbox]").HasAttribute("disabled"));
        editor.Find("input[type=checkbox]").Change(true);
        editor.Find("#item-fact-effect-kind").Change("passive");
        editor.Find("#item-fact-effect-name").Input("Water breathing");
        editor.Find("[data-testid='stage-item-effect']").Click();
        Assert.IsFalse(staged.PassiveEffects.Single().Concentration);
        editor.Find("#item-fact-effect-kind").Change("use");
        editor.Find("input[type=checkbox]").Change(true);
        editor.Find("#item-fact-effect-name").Input("Focused ward");
        editor.Find("[data-testid='stage-item-effect']").Click();
        Assert.IsTrue(staged.UseEffects.Single().Concentration);
    }

}
