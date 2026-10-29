using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;
[TestCategory("RulesData")][TestClass]public sealed class CompanionHealthUiTests
{
    [TestMethod]public async Task DamageUsesFullEnteredAmountAndCanConsumeTemporaryHpAtZeroCurrentHp()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-companion-damage-ui-"+Guid.NewGuid().ToString("N"));
        try{
            var c=AdventureRules.SetFacts(SampleWorkspace.Create().Character,new(){Companions=[new(){Id="owl",Name="Owl",MaximumHp=10,CurrentHp=2,TemporaryHp=5}]});var repo=TestStores.Open(directory);await repo.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repo);await session.SwitchCharacterAsync(c.Id);
            using var context=new BunitContext();context.Services.AddSingleton(session);var cut=context.Render<CompanionPanel>(p=>p.Add(x=>x.Character,session.Character!).Add(x=>x.Session,session));
            cut.Find("input[aria-label='HP adjustment for Owl']").Change("6");cut.FindAll("button").Single(b=>b.TextContent=="Damage").Click();cut.WaitForAssertion(()=>Assert.AreEqual(1,session.Character!.Sheet.Adventure.Companions[0].CurrentHp));Assert.AreEqual(0,session.Character!.Sheet.Adventure.Companions[0].TemporaryHp);
            c=AdventureRules.SetFacts(session.Character!,new(){Companions=[new(){Id="owl",Name="Owl",MaximumHp=10,CurrentHp=0,TemporaryHp=3,Resources=[new(){Id="ward",Name="Ward",Maximum=2,Remaining=2}]}]});await session.UpdateAdventureAsync(c.Sheet.Adventure,c.Id);cut.Render(p=>p.Add(x=>x.Character,session.Character!).Add(x=>x.Session,session));Assert.IsFalse(cut.FindAll("button").Single(b=>b.TextContent=="Damage").HasAttribute("disabled"));
            await session.UpdateAdventureAsync(session.Character!.Sheet.Adventure with{Movement=[new(){Kind="Fly",Speed=40}]},c.Id);cut.Find("button[aria-label='Spend Ward for Owl']").Click();cut.WaitForAssertion(()=>Assert.AreEqual(1,session.Character!.Sheet.Adventure.Companions[0].Resources[0].Remaining));Assert.AreEqual(40,session.Character!.Sheet.Adventure.Movement[0].Speed);
        }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
