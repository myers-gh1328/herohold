using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Core.Tests;

[TestClass]
public sealed class RestTests
{
    private static WorkspaceSnapshot Workspace() => new()
    {
        Revision = 1,
        Character = SpellTests.Caster() with
        {
            TemporaryHp = 5,
            Resources = [new() { Id = "slot1", Name = "Slots", Remaining = 1, Maximum = 4 }, new() { Id = "hitdice", Name = "Hit Dice", Remaining = 3, Maximum = 5 }, new() { Id = "pact", Name = "Pact", Remaining = 0, Maximum = 2, Recovery = RecoveryRest.Short, SlotLevel = 2 }, new() { Id = "manual", Name = "Manual", Remaining = 1, Maximum = 3, Recovery = RecoveryRest.Manual }],
            Play = new() { PreparedSpellIds = ["cure-wounds", "fog-cloud", "hail-of-thorns"], Preparation = new() { CanChangeOnLongRest = true, PreparedLimit = 3, ReplacementLimit = 1, EligibleSpellIds = ["cure-wounds", "fog-cloud", "hail-of-thorns", "jump", "goodberry"] } }
        }
    };

    [TestMethod]
    public void ShortRestStagesExplicitHitDiceAndRecoversOnlyEligiblePools()
    {
        var w = Workspace();
        var request = new RestRequest { OperationId = "rest-short", SourceRevision = 1, Kind = RestKind.Short, Mode = DiceMode.Physical, HitDiceToSpend = 1, PhysicalHitDice = [4], EndConcentration = false };
        var preview = RestRules.Preview(w, request, _ => throw new Exception("No hidden RNG"));
        Assert.AreEqual(38, preview.CurrentHp);
        Assert.AreEqual(32, w.Character.CurrentHp);
        Assert.AreEqual(2, preview.Resources.Single(x => x.Id == "hitdice").Remaining);
        Assert.AreEqual(1, preview.Resources.Single(x => x.Id == "slot1").Remaining);
        Assert.AreEqual(2, preview.Resources.Single(x => x.Id == "pact").Remaining);
        Assert.AreEqual(1, preview.Resources.Single(x => x.Id == "manual").Remaining);
        var rested = PlayRules.Rest(w, request, _ => throw new Exception("No hidden RNG"));
        Assert.AreEqual(38, rested.CurrentHp);
        Assert.AreEqual(5, rested.TemporaryHp);
        Assert.AreEqual(38, PlayRules.Rest(w with { Revision = 2, Character = rested }, request).CurrentHp);
        Assert.Throws<ArgumentException>(() => RestRules.Preview(w, request with { PhysicalHitDice = [11] }));
    }

    [TestMethod]
    public void LongRestKeepsSpellsUnlessExplicitlyOptedInAndEnforcesClassPolicy()
    {
        var w = Workspace();
        var request = new RestRequest { OperationId = "rest-long", SourceRevision = 1, Kind = RestKind.Long, NewPreparedSpellIds = ["nonsense"] };
        var rested = PlayRules.Rest(w, request);
        CollectionAssert.AreEqual(w.Character.Play.PreparedSpellIds, rested.Play.PreparedSpellIds);
        Assert.AreEqual(44, rested.CurrentHp);
        Assert.AreEqual(0, rested.TemporaryHp);
        Assert.AreEqual(5, rested.Resources.Single(x => x.Id == "hitdice").Remaining);
        Assert.AreEqual(4, rested.Resources.Single(x => x.Id == "slot1").Remaining);
        Assert.AreEqual(1, rested.Resources.Single(x => x.Id == "manual").Remaining);
        var changed = PlayRules.Rest(w, request with { ChangeSpells = true, NewPreparedSpellIds = ["jump", "fog-cloud", "hail-of-thorns"] });
        CollectionAssert.AreEqual(new[] { "jump", "fog-cloud", "hail-of-thorns" }, changed.Play.PreparedSpellIds);
        Assert.Throws<ArgumentException>(() => PlayRules.Rest(w, request with { ChangeSpells = true, NewPreparedSpellIds = ["jump", "goodberry", "hail-of-thorns"] }));
        var knownCaster = w with { Character = w.Character with { Play = w.Character.Play with { Preparation = new() { CanChangeOnLongRest = false } } } };
        Assert.Throws<ArgumentException>(() => PlayRules.Rest(knownCaster, request with { ChangeSpells = true, NewPreparedSpellIds = ["jump"] }));
        var restored = PlayRules.Undo(w with { Revision = 2, Character = changed }, new() { OperationId = "undo-rest", SourceRevision = 2 });
        Assert.AreEqual(32, restored.CurrentHp);
        CollectionAssert.AreEqual(w.Character.Play.PreparedSpellIds, restored.Play.PreparedSpellIds);
    }

    [TestMethod]
    public void ClassFeaturesCanRecoverOneUseOnShortRestAndAllOnLongRest()
    {
        var w = Workspace();
        w = w with { Character = w.Character with { Resources = [new() { Id = "second-wind", Name = "Second Wind", Remaining = 0, Maximum = 4, Recovery = RecoveryRest.Short, ShortRestRecoveryAmount = 1 }] } };
        var shortRest = RestRules.Preview(w, new() { OperationId = "fighter-short", SourceRevision = 1, Kind = RestKind.Short });
        Assert.AreEqual(1, shortRest.Resources[0].Remaining);
        var longRest = RestRules.Preview(w, new() { OperationId = "fighter-long", SourceRevision = 1, Kind = RestKind.Long });
        Assert.AreEqual(4, longRest.Resources[0].Remaining);
    }

    [TestMethod]
    public void ExhaustionIsBoundedAndLongRestRecoversOneLevelWithUndo()
    {
        var w = Workspace();
        w = w with { Character = WorkspaceRules.SetExhaustion(w.Character, 4) };
        Assert.AreEqual(4, w.Character.Exhaustion);
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkspaceRules.SetExhaustion(w.Character, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkspaceRules.SetExhaustion(w.Character, 7));
        var shortRest = PlayRules.Rest(w, new() { OperationId = "exhaustion-short", SourceRevision = 1, Kind = RestKind.Short });
        Assert.AreEqual(4, shortRest.Exhaustion);
        var longRest = PlayRules.Rest(w, new() { OperationId = "exhaustion-long", SourceRevision = 1, Kind = RestKind.Long });
        Assert.AreEqual(3, longRest.Exhaustion);
        var undone = PlayRules.Undo(w with { Revision = 2, Character = longRest }, new() { OperationId = "exhaustion-undo", SourceRevision = 2 });
        Assert.AreEqual(4, undone.Exhaustion);
        var noExhaustion = PlayRules.Rest(w with { Character = w.Character with { Exhaustion = 0 } }, new() { OperationId = "zero-exhaustion", SourceRevision = 1, Kind = RestKind.Long });
        Assert.AreEqual(0, noExhaustion.Exhaustion);
    }
}
