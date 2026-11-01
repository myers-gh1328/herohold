using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;
[TestClass]public sealed class PotionUiTests
{
    private string directory="";
    [TestInitialize]public void Setup()=>directory=Path.Combine(Path.GetTempPath(),"herohold-potion-ui",Guid.NewGuid().ToString("N"));
    [TestCleanup]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    private static readonly EquipmentItem Potion=new(){Id="potion",Name="Potion of Healing",Quantity=2,Consumable=true,UseAction=ItemUseAction.BonusAction,HealingDice=new(2,4),HealingBonus=2};

    private async Task<(WorkspaceSession Session,BunitContext Context,IRenderedComponent<ItemUseDialog> Dialog)> Open(DiceMode mode)
    {
        var c=SampleWorkspace.Create().Character with{Id="fictional-potion-ui",CurrentHp=10,Equipment=[Potion]};
        var repository=HandMadeStore.At(directory);await repository.SaveAsync(new(){Character=c},0);
        var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);
        var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session);
        var dialog=context.Render<ItemUseDialog>(p=>p.Add(x=>x.Session,session).Add(x=>x.PreferredDiceMode,mode));
        await dialog.InvokeAsync(()=>dialog.Instance.OpenAsync(session.Character!.Equipment.Single()));
        return (session,context,dialog);
    }

    [TestMethod]public async Task PhysicalDiceAskForTheTotalAndRefuseOneTheDiceCannotGive()
    {
        var (session,context,dialog)=await Open(DiceMode.Physical);using var _=context;
        StringAssert.Contains(dialog.Find("[data-testid='item-use-healing']").TextContent,"Heals 2d4 + 2");
        Assert.IsTrue(dialog.Find("[data-testid='confirm-item-use']").HasAttribute("disabled"),"Nothing to confirm until the total is entered.");
        dialog.Find("#item-use-healing-total").Input("11");Assert.IsTrue(dialog.Find("[data-testid='confirm-item-use']").HasAttribute("disabled"),"2d4 + 2 cannot make 11.");
        dialog.Find("#item-use-healing-total").Input("7");dialog.Find("[data-testid='confirm-item-use']").Click();
        dialog.WaitForAssertion(()=>StringAssert.Contains(dialog.Find("[data-testid='item-use-result']").TextContent,"You regain 7 HP"));
        Assert.AreEqual(17,session.Character!.CurrentHp);Assert.AreEqual(1,session.Character.Equipment.Single().Quantity);
        await session.ReloadAsync();Assert.AreEqual(17,session.Character!.CurrentHp);
    }

    [TestMethod]public async Task DigitalDiceRollTheHealingAndShowTheDice()
    {
        var (session,context,dialog)=await Open(DiceMode.Digital);using var _=context;
        Assert.AreEqual(0,dialog.FindAll("#item-use-healing-total").Count,"Digital dice are rolled by the app.");
        dialog.Find("[data-testid='confirm-item-use']").Click();
        dialog.WaitForAssertion(()=>StringAssert.Contains(dialog.Find("[data-testid='item-use-result']").TextContent,"Rolled 2d4 + 2:"));
        var hp=session.Character!.CurrentHp;Assert.IsTrue(hp is >=14 and <=20,$"10 HP plus 4 to 10 healing, got {hp}.");
        StringAssert.Contains(dialog.Find("[data-testid='item-use-result']").TextContent,$"You regain {hp-10} HP");Assert.AreEqual(1,session.Character.Equipment.Single().Quantity);
    }
}
