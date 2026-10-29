using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Core.Tests;

[TestClass]
public sealed class PlayTests
{
    private static WorkspaceSnapshot Workspace(CharacterState? c = null, long revision = 1) => new() { Revision = revision, Character = c ?? Fixtures.Ranger() };

    [TestMethod]
    public void ManualCounterActionsAreIdempotentAndUndoPreservesNotesAndInvalidatesResults()
    {
        var original = Workspace();
        var request = new ResourceAdjustmentRequest { OperationId = "spend-luck", SourceRevision = 1, ResourceId = "luck", Delta = -1 };
        var spent = PlayRules.AdjustResource(original, request);
        Assert.AreEqual(2, spent.Resources[0].Remaining);
        var duplicate = PlayRules.AdjustResource(Workspace(spent, 2), request);
        Assert.AreEqual(2, duplicate.Resources[0].Remaining);
        Assert.AreEqual(1, duplicate.Play.UndoHistory.Count);
        var noted = WorkspaceRules.AddNote(spent, "Keep this", "A later narrative change.");
        var restored = PlayRules.Undo(Workspace(noted, 3), new() { OperationId = "undo-spend", SourceRevision = 3 });
        Assert.AreEqual(3, restored.Resources[0].Remaining);
        Assert.AreEqual(1, restored.Notes.Count);
        Assert.IsNull(restored.Play.LastRoll);
        Assert.IsNull(restored.Play.LastCast);
        Assert.AreEqual(0, restored.Play.UndoHistory.Count);
        Assert.Throws<WorkspaceConflictException>(() => PlayRules.AdjustHp(Workspace(spent, 2), new() { OperationId = "stale-hp", SourceRevision = 1, Delta = -1 }));
    }

    [TestMethod]
    public void DigitalRollLuckyAndOnHitEffectsHaveIndependentOnceOnlyCosts()
    {
        var c = Fixtures.Ranger() with
        {
            Resources = [new() { Id = "luck", Name = "Luck", Remaining = 3, Maximum = 3 }, new() { Id = "dread", Name = "Dreadful Strike", Remaining = 4, Maximum = 4 }, new() { Id = "hunters", Name = "Mark", Remaining = 2, Maximum = 3 }],
            Effects = [new() { Id = "mark", Name = "Hunter’s Mark", Source = "spell:hunters-mark", Concentration = true }],
            Play = new() { HunterTarget = "Sentinel" }
        };
        var w = Workspace(c);
        var rolled = PlayRules.Roll(w, new() { OperationId = "attack", SourceRevision = 1, Kind = RollKind.Attack, AttackKind=AttackKind.Weapon, Ability = Ability.Dexterity, StandingFlatBonus = 2, FlatBonus = 2, BonusDice = new(1, 4) }, new Queue<int>([7, 3]).DequeueForDie());
        Assert.AreEqual(21, rolled.Play.LastRoll!.Total);
        var lucky = PlayRules.UseLucky(Workspace(rolled, 2), new() { OperationId = "lucky", SourceRevision = 2, RollId = "attack" }, _ => 18);
        Assert.AreEqual(32, lucky.Play.LastRoll!.Total);
        Assert.AreEqual(2, lucky.Resources.Single(x => x.Id == "luck").Remaining);
        Assert.Throws<ArgumentException>(() => PlayRules.UseLucky(Workspace(lucky, 3), new() { OperationId = "lucky-again", SourceRevision = 3, RollId = "attack" }));
        var hit = PlayRules.ResolveAttack(Workspace(lucky, 3), new() { OperationId = "hit", SourceRevision = 3, RollId = "attack", Hit = true, BaseDamage = new(1, 8), DamageModifier = 4, HuntersMark = true, DreadfulStrike = true }, new Queue<int>([5, 3, 2, 6]).DequeueForDie());
        Assert.AreEqual(3, hit.Play.LastRoll!.Damage.Count);
        Assert.AreEqual(9, hit.Play.LastRoll.Damage[0].Total);
        Assert.AreEqual(3, hit.Play.LastRoll.Damage[1].Total);
        Assert.AreEqual(8, hit.Play.LastRoll.Damage[2].Total);
        Assert.AreEqual(3, hit.Resources.Single(x => x.Id == "dread").Remaining);
        Assert.AreEqual(2, hit.Resources.Single(x => x.Id == "hunters").Remaining);
        Assert.IsTrue(hit.Play.DreadUsedThisTurn);
        Assert.Throws<ArgumentException>(() => PlayRules.ResolveAttack(Workspace(hit, 4), new() { OperationId = "hit-again", SourceRevision = 4, RollId = "attack", Hit = true, DreadfulStrike = true }));
        Assert.IsFalse(WorkspaceRules.AdvanceTurn(hit).Play.DreadUsedThisTurn);
        var undo = PlayRules.Undo(Workspace(lucky, 3), new() { OperationId = "undo-lucky", SourceRevision = 3 });
        Assert.AreEqual(3, undo.Resources.Single(x => x.Id == "luck").Remaining);
        Assert.IsNull(undo.Play.LastRoll);
    }

    [TestMethod]
    public void PhysicalDiceAreExplicitAndLuckyCancelsDisadvantageWithoutHiddenRng()
    {
        var w = Workspace();
        var rolled = PlayRules.Roll(w, new() { OperationId = "save", SourceRevision = 1, Kind = RollKind.Save, Ability = Ability.Dexterity, Mode = DiceMode.Physical, Advantage = RollAdvantage.Disadvantage, PhysicalD20 = [7, 18] }, _ => throw new Exception("Must not roll secretly"));
        Assert.AreEqual(14, rolled.Play.LastRoll!.Total);
        var lucky = PlayRules.UseLucky(Workspace(rolled, 2), new() { OperationId = "cancel-dis", SourceRevision = 2, RollId = "save" }, _ => throw new Exception("Must not roll secretly"));
        Assert.AreEqual(14, lucky.Play.LastRoll!.Total);
        Assert.AreEqual(7, lucky.Play.LastRoll.ChosenD20);
        Assert.AreEqual(RollAdvantage.Normal, lucky.Play.LastRoll.Advantage);
        Assert.AreEqual(2, lucky.Resources[0].Remaining);
        Assert.Throws<ArgumentException>(() => PlayRules.Roll(w, new() { OperationId = "invalid-die", SourceRevision = 1, Mode = DiceMode.Physical, PhysicalD20 = [21] }));
    }
    [TestMethod]
    public void PersistentRetryAndUndoHistoryStayBoundedAndOldRequestsRemainStale()
    {
        var w = Workspace();
        for (var i = 0; i < 125; i++)
        {
            var c = PlayRules.AdjustResource(w, new() { OperationId = "counter-" + i, SourceRevision = w.Revision, ResourceId = "luck", Delta = i % 2 == 0 ? -1 : 1 });
            w = w with { Revision = w.Revision + 1, Character = c };
        }
        Assert.AreEqual(100, w.Character.Play.CompletedOperationIds.Count);
        Assert.AreEqual(20, w.Character.Play.UndoHistory.Count);
        Assert.Throws<WorkspaceConflictException>(() => PlayRules.AdjustResource(w, new() { OperationId = "counter-0", SourceRevision = 1, ResourceId = "luck", Delta = -1 }));
        WorkspaceRules.Validate(w.Character);
    }

    [TestMethod]
    public void ConfirmedResultsCloseLuckyAndMissesNeverSpendOnHitResources()
    {
        var w = Workspace();
        var save = PlayRules.Roll(w, new() { OperationId = "saved-check", SourceRevision = 1, Kind = RollKind.Save, Ability = Ability.Dexterity }, _ => 10);
        var confirmed = PlayRules.ResolveRoll(Workspace(save, 2), new() { OperationId = "confirm-save", SourceRevision = 2, RollId = "saved-check" });
        Assert.Throws<ArgumentException>(() => PlayRules.UseLucky(Workspace(confirmed, 3), new() { OperationId = "too-late-lucky", SourceRevision = 3, RollId = "saved-check" }));
        var adv = PlayRules.Roll(w, new() { OperationId = "advantage-check", SourceRevision = 1, Kind = RollKind.Save, Ability = Ability.Dexterity, Advantage = RollAdvantage.Advantage }, _ => 10);
        Assert.Throws<ArgumentException>(() => PlayRules.UseLucky(Workspace(adv, 2), new() { OperationId = "third-die", SourceRevision = 2, RollId = "advantage-check" }));
        Assert.AreEqual(3, adv.Resources[0].Remaining);
        var attack = PlayRules.Roll(w, new() { OperationId = "miss-check", SourceRevision = 1, Kind = RollKind.Attack,AttackKind=AttackKind.Weapon }, _ => 10);
        var miss = PlayRules.ResolveAttack(Workspace(attack, 2), new() { OperationId = "miss", SourceRevision = 2, RollId = "miss-check", Hit = false, DreadfulStrike = true, HuntersMark = true });
        Assert.AreEqual(0, miss.Play.LastRoll!.Damage.Count);
        Assert.AreEqual(3, miss.Resources[0].Remaining);
        Assert.IsFalse(miss.Play.DreadUsedThisTurn);
    }

    [TestMethod]
    public void UndoPreservesUnrelatedLaterConditionsHpExhaustionAndAdvancedTurn()
    {
        var original = Fixtures.Ranger() with { Effects = [new() { Id = "timed-mark", Name = "Hunter’s Mark", Source = "spell:hunters-mark", Concentration = true, RemainingRounds = 1 }], Play = new() { HunterTarget = "Sentinel" } };
        var spent = PlayRules.AdjustResource(Workspace(original), new() { OperationId = "selective-spend", SourceRevision = 1, ResourceId = "luck", Delta = -1 });
        var later = WorkspaceRules.AddEffect(spent, new() { Id = "later-condition", Name = "Poisoned", Source = "Trap" });
        later = WorkspaceRules.AdvanceTurn(later);
        later = WorkspaceRules.SetExhaustion(later, 3);
        later = WorkspaceRules.AdjustHp(later, -5);
        later = WorkspaceRules.AddNote(later, "Later note", "Keep all unrelated changes.");
        var undone = PlayRules.Undo(Workspace(later, 6), new() { OperationId = "selective-undo", SourceRevision = 6 });
        Assert.AreEqual(3, undone.Resources[0].Remaining);
        Assert.AreEqual(2, undone.Turn);
        Assert.AreEqual(27, undone.CurrentHp);
        Assert.AreEqual(3, undone.Exhaustion);
        Assert.AreEqual("Poisoned", undone.Effects.Single().Name);
        Assert.AreEqual("", undone.Play.HunterTarget);
        Assert.AreEqual(1, undone.Notes.Count);
    }

    [TestMethod]
    public void UndoRejectsAnOverlappingLaterResourceChangeWithoutOverwritingIt()
    {
        var spent = PlayRules.AdjustResource(Workspace(), new() { OperationId = "overlap-spend", SourceRevision = 1, ResourceId = "luck", Delta = -1 });
        var later = WorkspaceRules.AdjustResource(spent, "luck", -1);
        Assert.Throws<WorkspaceConflictException>(() => PlayRules.Undo(Workspace(later, 3), new() { OperationId = "overlap-undo", SourceRevision = 3 }));
        Assert.AreEqual(1, later.Resources[0].Remaining);
    }

    [TestMethod]
    public void ImportedRecordedRollsRejectImpossibleDiceShapesAndOverflowingTerms()
    {
        var impossible = new RollOutcome { Id = "impossible", Name = "Imported roll", Kind = RollKind.Save, Mode = DiceMode.Digital, Advantage = RollAdvantage.Normal,
            D20 = [20, 1], ChosenD20 = 20, Total = 20 };
        var c = Fixtures.Ranger() with { Play = new() { LastRoll = impossible } };
        Assert.Throws<ArgumentException>(() => WorkspaceRules.Validate(c));
        var overflowing = impossible with { D20 = [20], StandingBonus = new(0, [new("Bad term", int.MaxValue), new("Bad term", int.MaxValue)]) };
        c = c with { Play = c.Play with { LastRoll = overflowing } };
        Assert.Throws<ArgumentException>(() => WorkspaceRules.Validate(c));
    }

    [TestMethod]
    public void RefundingDreadfulStrikeAfterAdvancingTurnNeverRewindsTheTurn()
    {
        var c = Fixtures.Ranger() with { Resources = [new() { Id = "dread", Name = "Dreadful Strike", Remaining = 4, Maximum = 4 }] };
        var roll = PlayRules.Roll(Workspace(c), new() { OperationId = "turn-attack", SourceRevision = 1, Kind = RollKind.Attack,AttackKind=AttackKind.Weapon }, _ => 10);
        var hit = PlayRules.ResolveAttack(Workspace(roll, 2), new() { OperationId = "turn-strike", SourceRevision = 2, RollId = "turn-attack", Hit = true, DreadfulStrike = true }, _ => 4);
        var next = WorkspaceRules.AdvanceTurn(hit);
        next = WorkspaceRules.AddEffect(next, new() { Id = "turn-condition", Name = "Poisoned" });
        var refunded = PlayRules.Undo(Workspace(next, 4), new() { OperationId = "turn-refund", SourceRevision = 4 });
        Assert.AreEqual(2, refunded.Turn);
        Assert.IsFalse(refunded.Play.DreadUsedThisTurn);
        Assert.AreEqual(4, refunded.Resources[0].Remaining);
        Assert.AreEqual("Poisoned", refunded.Effects.Single().Name);
        Assert.IsNull(refunded.Play.LastRoll);
    }

    [TestMethod]
    public void LegacyBeforeOnlyUndoEntriesRefuseUnsafeRollback()
    {
        var c = Fixtures.Ranger() with { Play = new() { UndoHistory = [new() { OperationId = "legacy-action", Description = "Old counter action", Before = new() { CurrentHp = 32, Turn = 1, Resources = Fixtures.Ranger().Resources } }] } };
        Assert.Throws<WorkspaceConflictException>(() => PlayRules.Undo(Workspace(c), new() { OperationId = "legacy-undo", SourceRevision = 1 }));
        Assert.AreEqual(32, c.CurrentHp);
        Assert.AreEqual(3, c.Resources[0].Remaining);
        Assert.AreEqual(1, c.Play.UndoHistory.Count);
    }

    [TestMethod]
    public void PlayActionsCannotCrossCharacterIdentityAtTheSameRevision()
    {
        var first = Workspace(Fixtures.Ranger() with { Id = "first-character" });
        var second = Workspace(Fixtures.Ranger() with { Id = "second-character" });
        var request = new ResourceAdjustmentRequest { OperationId = "cross-character", SourceCharacterId = first.Character.Id, SourceRevision = 1, ResourceId = "luck", Delta = -1 };
        Assert.Throws<WorkspaceConflictException>(() => PlayRules.AdjustResource(second, request));
        Assert.AreEqual(3, second.Character.Resources[0].Remaining);
        var collision = second with { Character = second.Character with { Play = second.Character.Play with { CompletedOperationIds = ["cross-character"] } } };
        Assert.Throws<WorkspaceConflictException>(() => PlayRules.AdjustResource(collision, request));
        Assert.Throws<WorkspaceConflictException>(() => RestRules.Preview(second, new() { OperationId = "cross-rest", SourceCharacterId = first.Character.Id, SourceRevision = 1, Kind = RestKind.Long }));
    }

}

internal static class QueueDiceExtension
{
    internal static Func<int, int> DequeueForDie(this Queue<int> values) => _ => values.Dequeue();
}
