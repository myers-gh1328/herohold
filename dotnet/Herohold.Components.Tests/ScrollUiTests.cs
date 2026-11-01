using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;
[TestClass]public sealed class ScrollUiTests
{
    private string directory="";
    [TestInitialize]public void Setup()=>directory=Path.Combine(Path.GetTempPath(),"herohold-scroll-ui",Guid.NewGuid().ToString("N"));
    [TestCleanup]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    private sealed class Spells:IScrollSpells
    {
        public ScrollSpell? Find(string name)=>name switch
        {
            "Conjure Animals"=>new(new(){Id="conjure-animals",Name="Conjure Animals",Level=3,CastingTime="Action",Range="60 feet",Concentration=true,Description="Spirits of nature appear."},["Druid","Ranger"]),
            "Speak with Dead"=>new(new(){Id="speak-with-dead",Name="Speak with Dead",Level=3,CastingTime="Action",Range="10 feet",Description="You grant the semblance of life."},["Bard","Cleric","Wizard"]),
            _=>null
        };
    }
    private async Task<(WorkspaceSession,BunitContext,IRenderedComponent<ScrollDialog>)> Open(string spell)
    {
        var c=SampleWorkspace.Create().Character with{Id="fictional-reader",Play=SampleWorkspace.Create().Character.Play with{SpellcastingAbility=Ability.Wisdom},
            Resources=[new(){Id="slot1",Name="Level 1 slots",SlotLevel=1,Maximum=4,Remaining=4}],
            Equipment=[new(){Id="scroll",Name="Spell Scroll (Level 3)",Quantity=1,Consumable=true,ScrollSpell=spell}]};
        var repository=HandMadeStore.At(directory);await repository.SaveAsync(new(){Character=c},0);
        var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);
        var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session);context.Services.AddSingleton<IScrollSpells>(new Spells());
        var dialog=context.Render<ScrollDialog>(p=>p.Add(x=>x.Session,session).Add(x=>x.PreferredDiceMode,DiceMode.Physical));
        await dialog.InvokeAsync(()=>dialog.Instance.OpenAsync(session.Character!.Equipment.Single()));
        return (session,context,dialog);
    }

    [TestMethod]public async Task AHigherLevelScrollAsksForTheCheckAndCastsOnASuccess()
    {
        var (session,context,dialog)=await Open("Conjure Animals");using var _=context;
        StringAssert.Contains(dialog.Find("[data-testid='scroll-summary']").TextContent,"Conjure Animals · Level 3");
        StringAssert.Contains(dialog.Find("[data-testid='scroll-numbers']").TextContent,"Spell save DC 15 · attack bonus +7");
        StringAssert.Contains(dialog.Find("[data-testid='scroll-check']").TextContent,"DC 13 Wisdom check");
        Assert.IsTrue(dialog.Find("[data-testid='confirm-scroll']").HasAttribute("disabled"),"The d20 comes first.");
        dialog.Find("#scroll-check-d20").Input("19");dialog.Find("[data-testid='confirm-scroll']").Click();
        dialog.WaitForAssertion(()=>StringAssert.Contains(dialog.Find("[data-testid='scroll-result']").TextContent,"You cast Conjure Animals"));
        Assert.AreEqual(0,session.Character!.Equipment.Single().Quantity);Assert.IsTrue(session.Character.Effects.Any(e=>e.Name=="Conjure Animals"&&e.Concentration));
    }

    [TestMethod]public async Task AScrollOffYourListCannotBeRead()
    {
        var (session,context,dialog)=await Open("Speak with Dead");using var _=context;
        StringAssert.Contains(dialog.Find("[data-testid='scroll-unavailable']").TextContent,"not on your spell list");
        Assert.IsTrue(dialog.Find("[data-testid='confirm-scroll']").HasAttribute("disabled"));Assert.AreEqual(1,session.Character!.Equipment.Single().Quantity);
    }
}
