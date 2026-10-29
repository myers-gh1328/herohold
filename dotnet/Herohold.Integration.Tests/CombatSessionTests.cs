using Herohold.TestSupport;
using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestCategory("RulesData")]
[TestClass]
public sealed class CombatSessionTests
{
    [TestMethod]
    public async Task CombatAllowanceTimersLayoutRetryUndoAndCanonicalRoundTripPersist()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-combat-session-"+Guid.NewGuid().ToString("N"));
        try
        {
            var session=new WorkspaceSession(TestStores.Open(directory));await session.InitializeAsync();
            async Task Act(CombatTransitionRequest r){await session.TransitionCombatAsync(r with{SourceCharacterId=session.Character!.Id,SourceRevision=session.Snapshot!.Revision});Assert.IsNull(session.Error,session.Error);}
            await Act(new(){Kind=CombatTransitionKind.Enter,NewEncounter=true,EncounterId="encounter",OwnTurnNow=true,TableBoundaryConfirmed=true,BoundaryId="first-turn"});
            await Act(new(){Kind=CombatTransitionKind.SpendAction,ActionId="dodge"});
            await Act(new(){Kind=CombatTransitionKind.AddEffect,Effect=new(){Id="timer",Name="Timed effect",Source="Table"},Timer=new(){EffectId="timer",Duration=CombatDuration.Rounds,OriginalUnit="round",OriginalAmount=1,Remaining=1}});
            await Act(new(){Kind=CombatTransitionKind.ConfigureLayout,Layout=session.Character!.Combat.Layout with{HiddenSections=["skills"],PinnedSpells=["hunters-mark"]}});
            await session.ReloadAsync();Assert.IsFalse(session.Character!.Combat.ActionAvailable);Assert.AreEqual(1,session.Character.Combat.Timers.Count);CollectionAssert.Contains(session.Character.Combat.Layout.HiddenSections,"skills");
            var request=new CombatTransitionRequest{SourceCharacterId=session.Character.Id,SourceRevision=session.Snapshot!.Revision,OperationId="advance-round",Kind=CombatTransitionKind.AdvanceRound,Round=2,AdvanceConfirmed=true};
            await session.TransitionCombatAsync(request);Assert.IsNull(session.Error,session.Error);var revision=session.Snapshot!.Revision;await session.TransitionCombatAsync(request);Assert.AreEqual(revision,session.Snapshot!.Revision);Assert.IsFalse(session.Character!.Effects.Any(x=>x.Id=="timer"));
            await session.UndoAsync(new(){SourceCharacterId=session.Character.Id,SourceRevision=session.Snapshot.Revision});Assert.IsNull(session.Error,session.Error);Assert.AreEqual(1,session.Character!.Combat.Round);Assert.IsTrue(session.Character.Effects.Any(x=>x.Id=="timer"));
            var canonical=WorkspaceProjection.ToCanonical(session.Character);var restored=TestStores.Restored(canonical);
            Assert.IsTrue(restored.Combat.Enabled);Assert.IsFalse(restored.Combat.ActionAvailable);Assert.AreEqual("timer",restored.Combat.Timers.Single().EffectId);CollectionAssert.Contains(restored.Combat.Layout.HiddenSections,"skills");
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
