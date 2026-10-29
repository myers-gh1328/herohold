using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Web.Components.Pages;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Components.Tests;
[TestCategory("RulesData")]
[TestClass]
public sealed class HitDieCounterUiTests
{
    [TestMethod]
    public async Task MountedPoolCounterChangesAuthoritativeDieAndSurvivesReloadExportBeforeRest()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-hitdie-counter-"+Guid.NewGuid().ToString("N"));
        try
        {
            var sample=SampleWorkspace.Create().Character;var c=sample with{Id="hitdie-counter-character",Build=null,Level=3,Resources=sample.Resources.Select(r=>r.Id=="hitdice"?r with{Maximum=3,Remaining=2}:r).ToList(),Play=sample.Play with{HitDiePools=[new(){Id="hd6",Sides=6,Maximum=1,Remaining=1,ClassTrackIds=["wizard"]},new(){Id="hd10",Sides=10,Maximum=2,Remaining=1,ClassTrackIds=["ranger"]}]}};
            var repo=HandMadeStore.At(directory);await repo.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repo);await session.SwitchCharacterAsync(c.Id);using var context=new BunitContext();context.JSInterop.Mode=JSRuntimeMode.Loose;context.Services.AddSingleton(session);var cut=context.Render<Home>();var revision=session.Snapshot!.Revision;
            Assert.IsNotNull(cut.Find("[data-testid='resource-hd6']"));Assert.IsNotNull(cut.Find("[data-testid='resource-hd10']"));Assert.AreEqual(0,cut.FindAll("button[aria-label='Use one Hit Dice']").Count,"Mixed pools cannot offer an ambiguous aggregate debit.");await cut.Find("button[aria-label='Use one d6 Hit Dice']").ClickAsync(new());cut.WaitForAssertion(()=>Assert.AreEqual(0,session.Character!.Play.HitDiePools.Single(p=>p.Id=="hd6").Remaining));Assert.AreEqual(revision+1,session.Snapshot.Revision);Assert.AreEqual(1,session.Character!.Resources.Single(p=>p.Id=="hitdice").Remaining);
            await session.ReloadAsync();Assert.AreEqual(0,session.Character!.Play.HitDiePools.Single(p=>p.Id=="hd6").Remaining);var exported=TestStores.Restored(WorkspaceProjection.ToCanonical(session.Character));Assert.AreEqual(0,exported.Play.HitDiePools.Single(p=>p.Id=="hd6").Remaining);Assert.AreEqual(1,exported.Resources.Single(p=>p.Id=="hitdice").Remaining);
            await session.RestAsync(new(){SourceCharacterId=c.Id,SourceRevision=session.Snapshot!.Revision,Kind=RestKind.Short,Mode=DiceMode.Physical,HitDieSpends=[new(){PoolId="hd6",Count=1,PhysicalDice=[3]}]});Assert.IsNotNull(session.Error);Assert.AreEqual(revision+1,session.Snapshot.Revision);
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
