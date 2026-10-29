using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;

[TestClass]
public sealed class CombatModeTests
{
    private static CharacterState Started()=>CombatRules.Transition(Fixtures.Ranger() with{Equipment=[new(){Id="longbow",Name="Longbow",Equipped=true,Quantity=1}],Resources=[new(){Id="dread",Name="Dreadful Strike",Remaining=3,Maximum=3}]},new(){Kind=CombatTransitionKind.Enter,NewEncounter=true,EncounterId="e",OwnTurnNow=true,TableBoundaryConfirmed=true,BoundaryId="t1"});
    [TestMethod]
    public void KnownSpellAttackReceiptConsumesFiniteCountWithoutRequiringModeledDamageOrAnotherAction()
    {
        var c=Started();c=c with{Combat=c.Combat with{ActionAvailable=false},Play=c.Play with{Spells=[new(){Id="eldritch-blast",Name="Eldritch Blast",CastingAbility=Ability.Charisma,AttackPattern=SpellAttackPattern.EldritchBlast,AttackRollKind="ranged"}],LastCast=new(){Id="cast",AttacksRemaining=2,Preview=new(){SpellId="eldritch-blast",Modeled=false,AttackRollKind="ranged",AttackRollCount=2}}}};
        var request=new RollRequest{Kind=RollKind.SpellAttack,AttackKind=AttackKind.Spell,Ability=Ability.Charisma,CombatContext=CombatRules.Context(c,"spell:eldritch-blast")};
        c=CombatRules.PrepareRoll(c,request);Assert.AreEqual(1,c.Play.LastCast!.AttacksRemaining);Assert.IsFalse(c.Combat.ActionAvailable);
        c=CombatRules.PrepareRoll(c,request);Assert.AreEqual(0,c.Play.LastCast!.AttacksRemaining);Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.PrepareRoll(c,request));
    }
    [TestMethod]
    public void UndoOriginalCombatCastAllowsSameReceiptEffectRefinementButPreservesLaterDifferentReceipt()
    {
        var c=Started();c=c with{Resources=[new(){Id="slot1",Name="Level 1 slots",Remaining=1,Maximum=1,SlotLevel=1}],Play=c.Play with{PreparedSpellIds=["cure-wounds"]}};
        var cast=PlayRules.Cast(new(){Character=c,Revision=1},new(){SourceCharacterId=c.Id,SourceRevision=1,SpellId="cure-wounds",SlotLevel=1,PaymentResourceId="slot1",Mode=DiceMode.Physical,CombatContext=CombatRules.Context(c,"spell:cure-wounds")});
        var refined=PlayRules.ResolveCastEffect(new(){Character=cast,Revision=2},new(){SourceCharacterId=c.Id,SourceRevision=2,CastId=cast.Play.LastCast!.Id,PhysicalDice=[4,5]});
        var undone=PlayRules.Undo(new(){Character=refined,Revision=3},new(){SourceCharacterId=c.Id,SourceRevision=3});
        Assert.AreEqual(1,undone.Resources.Single().Remaining);Assert.IsTrue(undone.Combat.ActionAvailable);Assert.IsNull(undone.Play.LastCast);
        var later=refined with{Play=refined.Play with{LastCast=refined.Play.LastCast! with{Id="later-cast"}}};
        Assert.ThrowsExactly<WorkspaceConflictException>(()=>PlayRules.Undo(new(){Character=later,Revision=3},new(){SourceCharacterId=c.Id,SourceRevision=3}));
    }
    [TestMethod]
    public void OlderWorkspaceTurnApiCannotBypassPausedEncounterBoundaries()
    {
        var paused=CombatRules.Transition(Started(),new(){Kind=CombatTransitionKind.Leave});
        Assert.ThrowsExactly<ArgumentException>(()=>WorkspaceRules.AdvanceTurn(paused));
    }
    [TestMethod]
    public void OwnStartClosesPreviousExternalTurnExactlyOnceForExpiryAndSave()
    {
        var c=Started();
        foreach(var pair in new[]{("expiry",CombatDuration.TargetNextEnd),("save",CombatDuration.RepeatedSave)})c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.AddEffect,Effect=new(){Id=pair.Item1,Name=pair.Item1},Timer=new(){EffectId=pair.Item1,TargetId="goblin",Duration=pair.Item2,SavePrompt="DC 12 Wisdom"}});
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.EndTurn,AdvanceConfirmed=true});
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.ExternalTurn,ActorId="goblin",BoundaryId="goblin-turn",TableBoundaryConfirmed=true});
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.StartTurn,BoundaryId="t2",TableBoundaryConfirmed=true});
        Assert.IsFalse(c.Effects.Any(e=>e.Id=="expiry"));Assert.IsTrue(c.Combat.Timers.Single(t=>t.EffectId=="save").AwaitingSave);
    }
    [TestMethod]
    public void DeathSaveIsOncePerOwnTurnAndNotBetweenTurns()
    {
        var c=Started() with{CurrentHp=0};
        c=CombatRules.FinalizeDeathSave(CombatRules.PrepareDeathSave(c));
        Assert.AreEqual(c.Combat.TurnId,c.Combat.DeathSaveRecordedTurnId);
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.PrepareDeathSave(c));
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.PrepareDeathSave(c with{Combat=c.Combat with{Phase=CombatPhase.BetweenTurns}}));
    }
    [TestMethod]
    public void UndoLuckyPreservesPaidAttackReceiptAndSpentAttackOpportunity()
    {
        var c=Started();c=c with{Resources=[..c.Resources,new(){Id="luck",Name="Luck",Remaining=1,Maximum=1}]};
        var rolled=PlayRules.Roll(new(){Character=c,Revision=1},new(){SourceCharacterId=c.Id,SourceRevision=1,Kind=RollKind.Attack,AttackKind=AttackKind.Weapon,Mode=DiceMode.Physical,PhysicalD20=[12],CombatContext=CombatRules.Context(c,"attack:longbow")});
        var lucky=PlayRules.UseLucky(new(){Character=rolled,Revision=2},new(){SourceCharacterId=c.Id,SourceRevision=2,RollId=rolled.Play.LastRoll!.Id,PhysicalD20=18});
        var undone=PlayRules.Undo(new(){Character=lucky,Revision=3},new(){SourceCharacterId=c.Id,SourceRevision=3});
        Assert.IsNotNull(undone.Play.LastRoll);Assert.AreEqual(12,undone.Play.LastRoll.ChosenD20);Assert.AreEqual(rolled.Play.LastRoll.Id,undone.Play.LastRoll.Id);Assert.IsFalse(undone.Combat.ActionAvailable);Assert.AreEqual(1,undone.Combat.AttacksRemaining);
    }
    [TestMethod]
    public void PausedCombatAndPendingReceiptsCannotBeBypassedByAnotherRoll()
    {
        var c=Started();var r=new RollRequest{Kind=RollKind.Attack,AttackKind=AttackKind.Weapon,CombatContext=CombatRules.Context(c,"attack:longbow")};
        var paused=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.Leave});Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.PrepareRoll(paused,r));
        var pending=c with{Play=c.Play with{LastRoll=new(){Id="old",Resolved=false}}};
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.PrepareRoll(pending,new(){Kind=RollKind.Skill,Skill="Perception"}));
    }
    [TestMethod]
    public void PrearmedMarkReceiptCannotMoveToANewQuarryAfterTheRoll()
    {
        var c=Started();c=c with{Effects=[new(){Id="mark",Source="spell:hunters-mark",Name="Hunter’s Mark",Concentration=true}],Play=c.Play with{HunterTarget="Original",LastRoll=new(){Id="attack"}}};
        var receipt=CombatRules.FinalizeRoll(c,new(){Kind=RollKind.Attack,CombatContext=CombatRules.Context(c,"attack:longbow",["hunters-mark"])});
        CombatRules.ValidateHit(receipt,new(){RollId="attack",HuntersMark=true,Target="Original"});
        var moved=receipt with{Play=receipt.Play with{HunterTarget="Other"}};
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.ValidateHit(moved,new(){RollId="attack",HuntersMark=true,Target="Other"}));
    }
    [TestMethod]
    public void NextEndTimerSurvivesCurrentTurnAndExpiresAtFollowingOwnEnd()
    {
        var c=Started();c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.AddEffect,Effect=new(){Id="next-end",Name="Until next end"},Timer=new(){EffectId="next-end",CreatorId=c.Id,Duration=CombatDuration.CreatorNextEnd}});
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.EndTurn,AdvanceConfirmed=true});Assert.AreEqual(1,c.Effects.Count);
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.StartTurn,BoundaryId="t2",TableBoundaryConfirmed=true});
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.EndTurn,AdvanceConfirmed=true});Assert.AreEqual(0,c.Effects.Count);
    }
    [TestMethod]
    public void LeaveCannotBypassRestOrLegacyAdvanceAndFreshEncounterResetsOnlyNewTurnBudgets()
    {
        var c=Started();c=c with{Combat=c.Combat with{SlotCastThisTurn=true,MovementSpent=20,ReactionAvailable=false}};
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.Leave});
        Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.AdvanceOwnTurn(new(){Character=c},new()));
        Assert.ThrowsExactly<ArgumentException>(()=>RestRules.Preview(new(){Character=c},new()));
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.Resume});c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.EndEncounter});
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.Enter,NewEncounter=true,EncounterId="second",OwnTurnNow=true,TableBoundaryConfirmed=true,BoundaryId="second-first"});
        Assert.IsFalse(c.Combat.SlotCastThisTurn);Assert.AreEqual(0,c.Combat.MovementSpent);Assert.IsTrue(c.Combat.ReactionAvailable);
    }
    [TestMethod]
    public void RetargetingMarkSpendsBonusAndCannotBeRepeatedOrUsedOutsideOwnTurn()
    {
        var c=Started();var request=new HunterMarkRetargetRequest{CombatContext=CombatRules.Context(c,"spell:hunters-mark-retarget")};
        var after=CombatRules.PrepareRetarget(c,request);Assert.IsFalse(after.Combat.BonusActionAvailable);
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.PrepareRetarget(after,request));
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.PrepareRetarget(c with{Combat=c.Combat with{Phase=CombatPhase.BetweenTurns}},request));
    }
    [TestMethod]
    public void EffectiveItemIncapacitationLocksActionsAndSpellAttacksCannotReplayCast()
    {
        var c=Started();var disabled=c with{Equipment=[new(){Id="cursed",Quantity=1,Equipped=true,PassiveEffects=[new(){Id="paralysis",Name="Paralyzed"}]}]};
        Assert.IsTrue(CombatRules.Incapacitated(disabled));
        c=c with{Play=c.Play with{LastCast=new(){Preview=new(){SpellId="cure-wounds"}}}};
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.PrepareRoll(c,new(){Kind=RollKind.SpellAttack,CombatContext=CombatRules.Context(c,"spell:cure-wounds")}));
    }
    [TestMethod]
    [DataRow("hunters-mark")][DataRow("grant-mark-alias")]
    public void SourceDurationRetainsHoursAndExpiresOnlyAfterConfirmedGameTime(string spellId)
    {
        var definition=SpellCatalog.Find(Started(),"hunters-mark") with{Id=spellId};
        var c=Started() with{Effects=[new(){Id="mark",Name="Hunter’s Mark",Source="spell:hunters-mark",Concentration=true}],Play=new(){HunterTarget="Goblin",Spells=[definition]}};
        c=CombatRules.FinalizeCast(c,new(){SpellId=spellId,SlotLevel=1});
        Assert.AreEqual("hour",c.Combat.Timers.Single().OriginalUnit);Assert.AreEqual(3600,c.Combat.Timers.Single().Remaining);
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.AdvanceGameTime,Amount=3600});
        Assert.AreEqual(0,c.Effects.Count);Assert.AreEqual("",c.Play.HunterTarget);
    }
    [TestMethod]
    public void StartTurnCanCommitOneExplicitTableBoundaryAtomically()
    {
        var c=CombatRules.Transition(Started(),new(){Kind=CombatTransitionKind.EndTurn,AdvanceConfirmed=true});
        var after=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.StartTurn,TableBoundaryConfirmed=true,BoundaryId="new-turn"});
        Assert.AreEqual("new-turn",after.Combat.TurnId);Assert.IsTrue(after.Combat.ActionAvailable);
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.Transition(after,new(){Kind=CombatTransitionKind.StartTurn,TableBoundaryConfirmed=true,BoundaryId="another"}));
    }
    [TestMethod]
    public void EligibilityLocksSpentAttackAndCastingWithSpecificReasons()
    {
        var c=Started();c=c with{Resources=[..c.Resources,new(){Id="slot1",SlotLevel=1,Remaining=1,Maximum=1}],Play=c.Play with{PreparedSpellIds=["cure-wounds"]},Combat=c.Combat with{ActionAvailable=false}};
        var attack=CombatRules.AttackOption(c,"longbow",AttackKind.Weapon);Assert.IsFalse(attack.Allowed);StringAssert.Contains(attack.Reason,"spent");
        var cast=CombatRules.CastOption(c,SpellCatalog.Find(c,"cure-wounds"));Assert.IsFalse(cast.Allowed);StringAssert.Contains(cast.Reason,"spent");
    }
    [TestMethod]
    public void OwnedActionSurgeAllowsRestrictedActionButNeverMagic()
    {
        var c=Started() with{ClassName="Fighter",Level=5,Resources=[new(){Id="action-surge",Name="Action Surge",Remaining=1,Maximum=1}]};
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.SpendAction,ActionId="dodge"});
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.SpendAction,ActionId="action-surge"});
        Assert.IsFalse(c.Combat.ActionAvailable);Assert.AreEqual(1,c.Combat.AdditionalActions.Single().Remaining);
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.PrepareCast(c,new(){SpellId="cure-wounds",CombatContext=CombatRules.Context(c,"spell:cure-wounds")}));
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.SpendAction,ActionId="attack"});Assert.AreEqual(2,c.Combat.AttacksRemaining);Assert.AreEqual(0,c.Resources.Single().Remaining);
    }
    [TestMethod]
    public void PendingRollMustBeResolvedOrExplicitlyAbandonedBeforeTimeMoves()
    {
        var c=Started();c=c with{Play=c.Play with{LastRoll=new(){Id="pending",Resolved=false}}};
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.Transition(c,new(){Kind=CombatTransitionKind.EndTurn,AdvanceConfirmed=true}));
        var after=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.EndTurn,AdvanceConfirmed=true,AbandonPending=true});
        Assert.IsTrue(after.Play.LastRoll!.Resolved);Assert.AreEqual(CombatPhase.BetweenTurns,after.Combat.Phase);
    }
    [TestMethod]
    public void CombatCommitsRetrySafelyUndoAndRejectAnotherCharacterOrRevision()
    {
        var c=Started();var r=new CombatTransitionRequest{SourceCharacterId=c.Id,SourceRevision=4,OperationId="spend-dodge",Kind=CombatTransitionKind.SpendAction,ActionId="dodge"};
        var after=PlayRules.TransitionCombat(new(){Character=c,Revision=4},r);
        Assert.IsFalse(after.Combat.ActionAvailable);Assert.AreEqual(1,after.Play.UndoHistory.Count);
        var retry=PlayRules.TransitionCombat(new(){Character=after,Revision=5},r);Assert.AreEqual(1,retry.Play.UndoHistory.Count);
        var undo=PlayRules.Undo(new(){Character=after,Revision=5},new(){SourceCharacterId=c.Id,SourceRevision=5});Assert.IsTrue(undo.Combat.ActionAvailable);
        Assert.ThrowsExactly<WorkspaceConflictException>(()=>PlayRules.TransitionCombat(new(){Character=c,Revision=5},r));
        Assert.ThrowsExactly<WorkspaceConflictException>(()=>PlayRules.TransitionCombat(new(){Character=c with{Id="other"},Revision=4},r));
    }
    [TestMethod]
    public void IllegalAttackApiCannotConsumeRandomValuesOrMutateBudget()
    {
        var c=Started();var calls=0;
        Assert.ThrowsExactly<WorkspaceConflictException>(()=>PlayRules.Roll(new(){Character=c,Revision=2},new(){SourceCharacterId=c.Id,SourceRevision=2,Kind=RollKind.Attack,AttackKind=AttackKind.Weapon},s=>{calls++;return 10;}));
        Assert.AreEqual(0,calls);Assert.IsTrue(c.Combat.ActionAvailable);
    }
    [TestMethod]
    public void IncapacitationDoesNotWarnAboutUnavailableActionOrBonus()
    {
        var c=Started() with{CurrentHp=0};
        Assert.AreEqual(0,CombatRules.Preview(c,new(){Kind=CombatTransitionKind.EndTurn}).UnusedOpportunities.Count);
        Assert.IsTrue(CombatRules.Options(c).All(x=>!x.Allowed));
        var after=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.EndTurn});Assert.AreEqual(CombatPhase.BetweenTurns,after.Combat.Phase);
    }
    [TestMethod]
    public void AttackCostsOneOpportunityAndRejectsStaleOrUnarmedOnHitEffects()
    {
        var c=Started();
        Assert.ThrowsExactly<WorkspaceConflictException>(()=>CombatRules.PrepareRoll(c,new(){Kind=RollKind.Attack}));
        var r=new RollRequest{Kind=RollKind.Attack,AttackKind=AttackKind.Weapon,CombatContext=CombatRules.Context(c,"attack:longbow",["dreadful-strike"])};
        var after=CombatRules.PrepareRoll(c,r);
        Assert.IsFalse(after.Combat.ActionAvailable);Assert.AreEqual(1,after.Combat.AttacksRemaining);
        Assert.AreEqual(c.Resources.Single(x=>x.Id=="dread").Remaining,after.Resources.Single(x=>x.Id=="dread").Remaining);
        Assert.ThrowsExactly<WorkspaceConflictException>(()=>CombatRules.PrepareRoll(after,r with{CombatContext=r.CombatContext! with{TurnId="old"}}));
        var receipt=CombatRules.FinalizeRoll(after with{Play=after.Play with{LastRoll=new(){Id="rolled"}}},r);
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.ValidateHit(receipt,new(){RollId="rolled",HuntersMark=true}));
        CombatRules.ValidateHit(receipt,new(){RollId="rolled",DreadfulStrike=true});
    }
    [TestMethod]
    public void CastingTracksActualActionCostAndOneSlotPerCreatureTurn()
    {
        var c=Started();
        var first=new CastRequest{SpellId="hunters-mark",CombatContext=CombatRules.Context(c,"spell:hunters-mark"),PaymentResourceId="slot1"};
        var after=CombatRules.PrepareCast(c,first);
        Assert.IsFalse(after.Combat.BonusActionAvailable);Assert.IsTrue(after.Combat.SlotCastThisTurn);
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.PrepareCast(after,new(){SpellId="cure-wounds",PaymentResourceId="slot1",CombatContext=CombatRules.Context(after,"spell:cure-wounds")}));
        Assert.IsTrue(c.Combat.BonusActionAvailable);
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.PrepareCast(c,first with{Ritual=true}));
    }
    [TestMethod]
    public void LeaveResumeAndRoundsNeverRefreshSpentAllowances()
    {
        var c=Started();
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.SpendAction,ActionId="dodge"});
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.SpendAction,ActionId="reaction",TableBoundaryConfirmed=true});
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.Leave});
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.Resume});
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.AdvanceRound,Round=2,AdvanceConfirmed=true});
        Assert.IsFalse(c.Combat.ActionAvailable);Assert.IsFalse(c.Combat.ReactionAvailable);
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.Transition(c,new(){Kind=CombatTransitionKind.StartTurn}));
    }
    [TestMethod]
    public void EndTurnRequiresOnlyUsableActionWarningAndNewConfirmedBoundary()
    {
        var c=Started();
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.Transition(c,new(){Kind=CombatTransitionKind.EndTurn}));
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.EndTurn,AdvanceConfirmed=true});
        Assert.ThrowsExactly<ArgumentException>(()=>CombatRules.Transition(c,new(){Kind=CombatTransitionKind.ConfirmOwnTurn,BoundaryId="t1",TableBoundaryConfirmed=true}));
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.ConfirmOwnTurn,BoundaryId="t2",TableBoundaryConfirmed=true});
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.StartTurn});
        Assert.AreEqual("t2",c.Combat.TurnId);Assert.AreEqual(2,c.Combat.OwnTurnNumber);Assert.IsTrue(c.Combat.ReactionAvailable);
    }
    [TestMethod]
    public void RoundTimersExpireOnRoundOnlyAndLegacyTimersStayManual()
    {
        var c=Started() with{Effects=[new(){Id="legacy",Name="Legacy",RemainingRounds=1}]};
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.AddEffect,Effect=new(){Id="counted",Name="Counted"},Timer=new(){EffectId="counted",Duration=CombatDuration.Rounds,OriginalAmount=1,Remaining=1}});
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.EndTurn,AdvanceConfirmed=true});
        Assert.AreEqual(2,c.Effects.Count);
        c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.AdvanceRound,Round=2,AdvanceConfirmed=true});
        Assert.AreEqual("legacy",c.Effects.Single().Id);Assert.AreEqual(1,c.Effects.Single().RemainingRounds);
    }
    [TestMethod]
    public void EnterRequiresAnExplicitBoundaryAndStartsOneOwnTurn()
    {
        var before=Fixtures.Ranger();
        var after=CombatRules.Transition(before,new(){Kind=CombatTransitionKind.Enter,NewEncounter=true,EncounterId="encounter-1",OwnTurnNow=true,TableBoundaryConfirmed=true,BoundaryId="turn-1"});
        Assert.IsTrue(after.Combat.Enabled);
        Assert.IsTrue(after.Combat.EncounterActive);
        Assert.AreEqual(CombatPhase.OwnTurn,after.Combat.Phase);
        Assert.IsTrue(after.Combat.ActionAvailable);
        Assert.AreEqual("turn-1",after.Combat.TurnId);
        Assert.IsFalse(before.Combat.Enabled);
    }
}
