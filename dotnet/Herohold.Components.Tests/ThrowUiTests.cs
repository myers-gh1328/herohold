using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Pages;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;
[TestClass]public sealed class ThrowUiTests
{
    private string directory="";
    [TestInitialize]public void Setup()=>directory=Path.Combine(Path.GetTempPath(),"herohold-throw-ui",Guid.NewGuid().ToString("N"));
    [TestCleanup]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}

    [TestMethod]public async Task AThrownItemSitsWithTheAttacksAndThrowingSpendsOneAndRollsItsDamage()
    {
        var c=SampleWorkspace.Create().Character with{Id="fictional-thrower",Equipment=[new(){Id="fire",Name="Alchemist's Fire",Quantity=2,Consumable=true,ThrownRangeFt=20,ThrownDamage=new(1,4),ThrownDamageType="Fire"}]};
        var repository=HandMadeStore.At(directory);await repository.SaveAsync(new(){Character=c},0);
        var session=new WorkspaceSession(repository);await session.SwitchCharacterAsync(c.Id);
        using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session);
        var home=context.Render<Home>();
        var dc=ThrownItemRules.SaveDc(session.Character!).Total;
        StringAssert.Contains(home.Find("#play-attacks").TextContent,$"Alchemist's Fire ×2");StringAssert.Contains(home.Find("#play-attacks").TextContent,$"DC {dc} Dex save · 20 ft · 1d4 Fire");
        home.Find("[data-testid='throw-fire']").Click();
        home.WaitForAssertion(()=>StringAssert.Contains(home.Find("[data-testid='throw-summary']").TextContent,$"DC {dc} Dexterity save"));
        home.Find("[data-testid='confirm-throw']").Click();
        home.WaitForAssertion(()=>Assert.AreEqual(1,home.FindAll("[data-testid='throw-result']").Count));
        Assert.AreEqual(1,session.Character!.Equipment.Single().Quantity);
        home.Find("[data-testid='roll-throw-damage']").Click();
        StringAssert.Matches(home.Find("[data-testid='throw-damage']").TextContent,new System.Text.RegularExpressions.Regex(@"^1d4 Fire: [1-4] = [1-4] Fire damage\.$"));
    }
}
