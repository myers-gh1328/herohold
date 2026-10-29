using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;
[TestClass]public sealed class MulticlassRestUiTests
{
    [TestMethod]public async Task MixedDicePickerStagesSelectedSizeAndSourceChangesAreOptIn()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-class-rest-ui-"+Guid.NewGuid().ToString("N"));
        try{
            var c=SampleWorkspace.Create().Character;c=c with{Play=c.Play with{HitDiePools=[new(){Id="hd6",Sides=6,Maximum=1,Remaining=1},new(){Id="hd10",Sides=10,Maximum=1,Remaining=1}],SpellSources=[new(){Id="wizard",Name="Wizard",Ability=Ability.Intelligence,Preparation=new(){MaximumSpellLevel=1,CanChangeOnLongRest=true,PreparedLimit=2}},new(){Id="ranger",Name="Ranger",Ability=Ability.Wisdom,Preparation=new(){MaximumSpellLevel=1,CanChangeOnLongRest=true,PreparedLimit=2,ReplacementLimit=1}}]}};
            var repo=HandMadeStore.At(directory);await repo.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repo);await session.SwitchCharacterAsync(c.Id);
            using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session);var cut=context.Render<GameplayDialogs>(p=>p.Add(x=>x.Character,session.Character!).Add(x=>x.Session,session).Add(x=>x.PreferredDiceMode,DiceMode.Physical));
            await cut.InvokeAsync(()=>cut.Instance.OpenRestAsync(RestKind.Short));cut.Find("#rest-hit-die-pool").Change("hd6");Assert.AreEqual("6",cut.Find("#rest-physical-hit-die").GetAttribute("max"));cut.Find("#rest-physical-hit-die").Change("6");cut.Find("[data-testid='stage-hit-die']").Click();StringAssert.Contains(cut.Find("#rest-dialog").TextContent,"d6: 6");Assert.AreEqual("d6 · 1 / 1 available",cut.Find("#rest-hit-die-pool option[value=hd6]").TextContent);Assert.IsFalse(cut.Find("#rest-dialog").TextContent.Contains("@pool"));
            await cut.InvokeAsync(()=>cut.Instance.OpenRestAsync(RestKind.Long));Assert.AreEqual(2,cut.FindAll("[data-testid='source-change-spells']").Count);Assert.IsTrue(cut.FindAll("[data-testid='source-change-spells']").All(x=>!x.HasAttribute("checked")));Assert.AreEqual(0,cut.FindAll("[data-testid='source-spell-picker']").Count);
        }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
