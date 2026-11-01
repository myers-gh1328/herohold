using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Pages;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;
[TestClass]public sealed class AmmunitionUiTests
{
    private string directory="";
    [TestInitialize]public void Setup()=>directory=Path.Combine(Path.GetTempPath(),"herohold-ammunition-ui",Guid.NewGuid().ToString("N"));
    [TestCleanup]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}

    [TestMethod]public async Task CountingShowsArrowsLeftBlocksTheBowAtZeroAndRecoversFiredArrows()
    {
        var c=SampleWorkspace.Create().Character with{Id="fictional-archer",Equipment=[new(){Id="bow",Name="Longbow",Equipped=true},new(){Id="arrows",Name="Arrow",Quantity=1,Consumable=true}],
            Derived=new(){Attacks=[new(){Id="weapon:longbow",Name="Longbow",Ability=Ability.Dexterity,Proficient=true,DamageDice=new(1,8),DamageType="Piercing",Ammunition="Arrow"}]}};
        var repository=HandMadeStore.At(directory);await repository.SaveAsync(new(){Character=c},0);
        var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);
        using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session);
        var panel=context.Render<AmmunitionPanel>(p=>p.Add(x=>x.Character,session.Character!).Add(x=>x.Session,session));
        StringAssert.Contains(panel.Markup,"Not counted");
        var home=context.Render<Home>();Assert.AreEqual(0,home.FindAll("[data-testid='ammunition-left-longbow']").Count,"Nothing is shown while ammunition is not counted.");

        panel.Find("[data-testid='count-ammunition']").Change(true);
        Assert.IsTrue(session.Character!.Sheet.CountAmmunition);
        panel.Render(p=>p.Add(x=>x.Character,session.Character!));
        StringAssert.Contains(panel.Find("[data-testid='ammunition-arrow']").TextContent,"1 carried");
        home.Render();
        Assert.AreEqual("Arrows 1",home.Find("[data-testid='ammunition-left-longbow']").TextContent);Assert.IsFalse(home.Find("[data-testid='roll-longbow']").HasAttribute("disabled"));

        await session.RollAsync(new(){OperationId="last-arrow",SourceCharacterId=c.Id,SourceRevision=session.Snapshot!.Revision,Kind=RollKind.Attack,AttackKind=AttackKind.Weapon,Mode=DiceMode.Physical,PhysicalD20=[9],CombatContext=CombatRules.Context(session.Character!,"attack:longbow")});
        Assert.IsNull(session.Error,session.Error);
        home.Render();
        Assert.AreEqual("Arrows 0",home.Find("[data-testid='ammunition-left-longbow']").TextContent);Assert.IsTrue(home.Find("[data-testid='roll-longbow']").HasAttribute("disabled"),"No arrows, no bow attack.");
        panel.Render(p=>p.Add(x=>x.Character,session.Character!));
        StringAssert.Contains(panel.Find("[data-testid='ammunition-arrow']").TextContent,"1 fired");
        panel.Find("[data-testid='recover-arrow']").Click();
        Assert.AreEqual(0,session.Character!.Sheet.AmmunitionFired.Count);Assert.AreEqual(0,AmmunitionRules.Carried(session.Character,"Arrow"),"Half of one arrow, rounded down, is none.");
    }
}
