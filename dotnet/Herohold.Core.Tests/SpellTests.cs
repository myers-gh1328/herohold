using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Core.Tests;

[TestClass]
public sealed class SpellTests
{
    internal static CharacterState Caster() => Fixtures.Ranger() with
    {
        Resources = [new() { Id = "slot1", Name = "First level", Remaining = 4, Maximum = 4 }, new() { Id = "slot2", Name = "Second level", Remaining = 2, Maximum = 2 }, new() { Id = "hunters", Name = "Hunter’s Mark", Remaining = 3, Maximum = 3 }],
        Play = new() { PreparedSpellIds = ["cure-wounds", "fog-cloud", "hail-of-thorns"], GrantedSpellIds = ["hunters-mark", "disguise-self"] }
    };

    [TestMethod]
    public void StructuredPreviewsApplyEachSpellSpecificHigherSlotBenefit()
    {
        var c = Caster();
        var hail = SpellRules.Preview(c, "hail-of-thorns", 2);
        Assert.AreEqual(new DiceFormula(2, 10), hail.Dice);
        Assert.AreEqual("Piercing", hail.DamageType);
        Assert.AreEqual(Ability.Dexterity, hail.SaveAbility);
        Assert.AreEqual(15, hail.SaveDc);
        Assert.AreEqual(0, hail.Modifier);
        var cure = SpellRules.Preview(c, "cure-wounds", 2);
        Assert.AreEqual(new DiceFormula(4, 8), cure.Dice);
        Assert.AreEqual(4, cure.Modifier);
        Assert.AreEqual(40, SpellRules.Preview(c, "fog-cloud", 2).RadiusFt);
        Assert.AreEqual(new DiceFormula(1, 6), SpellRules.Preview(c, "hunters-mark", 3).Dice);
        Assert.AreEqual("8 hour", SpellRules.Preview(c, "hunters-mark", 3).DurationText);
        StringAssert.Contains(SpellRules.Preview(c, "disguise-self", 2).Note, "No higher-slot benefit");
        Assert.AreEqual(2, c.Resources.Single(x => x.Id == "slot2").Remaining);
    }

    [TestMethod]
    public void CastPaysChosenSlotOnceAndEffectRollUsesTheSameScaledMetadata()
    {
        var w = new WorkspaceSnapshot { Revision = 1, Character = Caster() };
        var request = new CastRequest { OperationId = "cast-hail", SourceRevision = 1, SpellId = "hail-of-thorns", SlotLevel = 2, PaymentResourceId = "slot2", QualifyingHit = true };
        var cast = PlayRules.Cast(w, request);
        Assert.AreEqual(1, cast.Resources.Single(x => x.Id == "slot2").Remaining);
        Assert.AreEqual(4, cast.Resources.Single(x => x.Id == "slot1").Remaining);
        Assert.AreEqual(new DiceFormula(2, 10), cast.Play.LastCast!.Preview.Dice);
        Assert.AreEqual(1, PlayRules.Cast(w with { Revision = 2, Character = cast }, request).Resources.Single(x => x.Id == "slot2").Remaining);
        var queue = new Queue<int>([4, 7]);
        var resolved = PlayRules.ResolveCastEffect(w with { Revision = 2, Character = cast }, new() { OperationId = "effect-hail", SourceRevision = 2, CastId = cast.Play.LastCast.Id }, _ => queue.Dequeue());
        Assert.AreEqual(11, resolved.Play.LastCast!.EffectTotal);
        Assert.AreEqual(32, resolved.CurrentHp);
        Assert.Throws<ArgumentException>(() => PlayRules.ResolveCastEffect(w with { Revision = 3, Character = resolved }, new() { OperationId = "again-effect", SourceRevision = 3, CastId = cast.Play.LastCast.Id }));
        var restored = PlayRules.Undo(w with { Revision = 3, Character = resolved }, new() { OperationId = "undo-hail", SourceRevision = 3 });
        Assert.AreEqual(2, restored.Resources.Single(x => x.Id == "slot2").Remaining);
        Assert.IsNull(restored.Play.LastCast);
    }

    [TestMethod]
    public void CastRejectsMissingHitAndReplacesConcentrationOnlyAfterConfirmation()
    {
        var w = new WorkspaceSnapshot { Revision = 1, Character = Caster() };
        Assert.Throws<ArgumentException>(() => PlayRules.Cast(w, new() { OperationId = "missing-hit", SourceRevision = 1, SpellId = "hail-of-thorns" }));
        Assert.Throws<ArgumentException>(() => PlayRules.Cast(w, new() { OperationId = "missing-conc", SourceRevision = 1, SpellId = "fog-cloud" }));
        var marked = PlayRules.Cast(w, new() { OperationId = "mark", SourceRevision = 1, SpellId = "hunters-mark", PaymentResourceId = "hunters", Target = "Sentinel", ConfirmConcentration = true });
        Assert.AreEqual("Sentinel", marked.Play.HunterTarget);
        Assert.AreEqual(2, marked.Resources.Single(x => x.Id == "hunters").Remaining);
        var fog = PlayRules.Cast(w with { Revision = 2, Character = marked }, new() { OperationId = "fog", SourceRevision = 2, SpellId = "fog-cloud", PaymentResourceId = "slot2", SlotLevel = 2, ConfirmConcentration = true });
        Assert.AreEqual(1, fog.Effects.Count(x => x.Concentration));
        Assert.AreEqual("Fog Cloud", fog.Effects.Single(x => x.Concentration).Name);
        Assert.AreEqual("", fog.Play.HunterTarget);
        var undone = PlayRules.Undo(w with { Revision = 3, Character = fog }, new() { OperationId = "undo-fog", SourceRevision = 3 });
        Assert.AreEqual("Sentinel", undone.Play.HunterTarget);
        Assert.AreEqual("Hunter’s Mark", undone.Effects.Single(x => x.Concentration).Name);
    }

    [TestMethod]
    public void UnknownHigherSlotScalingNeverInventsAResolvedBaseDamageRoll()
    {
        var c = Caster() with { Play = Caster().Play with
        {
            PreparedSpellIds = ["custom-flame"],
            Spells = [new() { Id = "custom-flame", Name = "Custom Flame", Level = 1, Modeled = true, EffectKind = SpellEffectKind.Damage, Dice = new(1, 6), Scaling = ScalingStatus.Unmodeled }]
        } };
        var higher = SpellRules.Preview(c, "custom-flame", 2);
        Assert.IsNull(higher.Dice);
        StringAssert.Contains(higher.Note, "unmodeled");
        var w = new WorkspaceSnapshot { Revision = 1, Character = c };
        var cast = PlayRules.Cast(w, new() { OperationId = "manual-cast", SourceRevision = 1, SpellId = "custom-flame", SlotLevel = 2, PaymentResourceId = "slot2" });
        Assert.AreEqual(1, cast.Resources.Single(x => x.Id == "slot2").Remaining);
        Assert.Throws<ArgumentException>(() => PlayRules.ResolveCastEffect(w with { Revision = 2, Character = cast }, new() { OperationId = "manual-effect", SourceRevision = 2, CastId = "manual-cast" }));
    }

    [TestMethod]
    public void PhysicalSpellEffectsUseEnteredDiceAndHealingModifierOnce()
    {
        var w = new WorkspaceSnapshot { Revision = 1, Character = Caster() };
        var cast = PlayRules.Cast(w, new() { OperationId = "physical-cure", SourceRevision = 1, SpellId = "cure-wounds", SlotLevel = 2, PaymentResourceId = "slot2", Mode = DiceMode.Physical });
        var resolved = PlayRules.ResolveCastEffect(w with { Revision = 2, Character = cast }, new() { OperationId = "physical-heal", SourceRevision = 2, CastId = "physical-cure", PhysicalDice = [1, 2, 3, 4] }, _ => throw new Exception("No hidden RNG"));
        Assert.AreEqual(14, resolved.Play.LastCast!.EffectTotal);
        Assert.AreEqual(32, resolved.CurrentHp);
        Assert.Throws<ArgumentException>(() => PlayRules.ResolveCastEffect(w with { Revision = 2, Character = cast }, new() { OperationId = "bad-physical-heal", SourceRevision = 2, CastId = "physical-cure", PhysicalDice = [1, 2, 3, 9] }));
    }

    [TestMethod]
    public void RangerFoeSlayerChangesMarkedHitDieAtLevelTwentyOnly()
    {
        var ranger = Caster() with { Level = 20 };
        Assert.AreEqual(new DiceFormula(1, 10), SpellRules.Preview(ranger, "hunters-mark", 1).Dice);
        Assert.AreEqual(new DiceFormula(1, 6), SpellRules.Preview(ranger with { Level = 19 }, "hunters-mark", 1).Dice);
        Assert.AreEqual(new DiceFormula(1, 6), SpellRules.Preview(ranger with { ClassName = "Fighter" }, "hunters-mark", 1).Dice);
        ranger = ranger with { Effects = [new() { Id = "mark", Name = "Hunter’s Mark", Source = "spell:hunters-mark", Concentration = true }], Play = ranger.Play with { HunterTarget = "Quarry" } };
        var w = new WorkspaceSnapshot { Revision = 1, Character = ranger };
        var attack = PlayRules.Roll(w, new() { OperationId = "capstone-attack", SourceRevision = 1, Kind = RollKind.Attack }, _ => 10);
        var hit = PlayRules.ResolveAttack(w with { Revision = 2, Character = attack }, new() { OperationId = "capstone-hit", SourceRevision = 2, RollId = "capstone-attack", Hit = true, HuntersMark = true }, sides => sides);
        Assert.AreEqual(10, hit.Play.LastRoll!.Damage.Single(x => x.Name == "Hunter’s Mark").Total);
    }

    [TestMethod]
    public void KnownCantripsRemainCastableForPreparedCastersWithoutPreparingLeveledSpells()
    {
        var c = Caster() with { ClassName = "Wizard", Play = Caster().Play with { PreparedSpellIds = [], KnownSpellIds = ["fire-bolt", "cure-wounds"], Preparation = new() { CanChangeOnLongRest = true }, Spells = [new() { Id = "fire-bolt", Name = "Fire Bolt", Level = 0 }] } };
        var w = new WorkspaceSnapshot { Revision = 1, Character = c };
        var cast = PlayRules.Cast(w, new() { OperationId = "known-cantrip", SourceRevision = 1, SpellId = "fire-bolt", SlotLevel = 0, PaymentResourceId = "" });
        Assert.AreEqual("fire-bolt", cast.Play.LastCast!.Preview.SpellId);
        Assert.AreEqual(4, cast.Resources.Single(x => x.Id == "slot1").Remaining);
        Assert.Throws<ArgumentException>(() => PlayRules.Cast(w, new() { OperationId = "unprepared-leveled", SourceRevision = 1, SpellId = "cure-wounds", SlotLevel = 1, PaymentResourceId = "slot1" }));
    }

    [TestMethod]
    public void GrantedSpellUsesItsOwnCastingAbilityAndOnlyItsBoundFreePool()
    {
        var definition = SpellCatalog.All.Single(x => x.Id == "cure-wounds") with { Id = "magic-initiate-cure", CastingAbility = Ability.Charisma, GrantResourceId = "initiate-use" };
        var caster = Caster();
        caster.AbilityScores[Ability.Charisma] = 20;
        caster = caster with { Resources = [.. caster.Resources, new() { Id = "initiate-use", Name = "Magic Initiate", Remaining = 1, Maximum = 1 }], Play = caster.Play with { GrantedSpellIds = ["magic-initiate-cure", "disguise-self"], Spells = [definition] } };
        var preview = SpellRules.Preview(caster, definition.Id, 1);
        Assert.AreEqual(5, preview.Modifier);
        var w = new WorkspaceSnapshot { Revision = 1, Character = caster };
        var cast = PlayRules.Cast(w, new() { OperationId = "grant-cast", SourceRevision = 1, SpellId = definition.Id, SlotLevel = 1, PaymentResourceId = "initiate-use" });
        Assert.AreEqual(0, cast.Resources.Single(x => x.Id == "initiate-use").Remaining);
        Assert.AreEqual(4, cast.Resources.Single(x => x.Id == "slot1").Remaining);
        Assert.Throws<ArgumentException>(() => PlayRules.Cast(w, new() { OperationId = "wrong-grant-spell", SourceRevision = 1, SpellId = "disguise-self", SlotLevel = 1, PaymentResourceId = "initiate-use" }));
        Assert.Throws<ArgumentException>(() => PlayRules.Cast(w, new() { OperationId = "grant-upcast", SourceRevision = 1, SpellId = definition.Id, SlotLevel = 2, PaymentResourceId = "initiate-use" }));
    }

    [TestMethod]
    public void AtWillAndRitualGrantsRespectTheirSourceCastingPolicies()
    {
        var atWill = SpellCatalog.All.Single(x => x.Id == "cure-wounds") with { Id = "mastery-cure", AtWill = true };
        var ritual = new SpellDefinition { Id = "detect-magic", Name = "Detect Magic", Level = 1, Ritual = true };
        var restricted = SpellCatalog.All.Single(x => x.Id == "disguise-self") with { Id = "innate-disguise", AllowsSlotCasting = false, GrantResourceId = "innate-use" };
        var c = Caster();
        c = c with { ClassName = "Wizard", Resources = [.. c.Resources, new() { Id = "innate-use", Name = "Innate use", Remaining = 1, Maximum = 1 }], Play = c.Play with
        {
            GrantedSpellIds = ["mastery-cure", "innate-disguise"], PreparedSpellIds = [], SpellbookIds = ["detect-magic"], Preparation = new() { CanChangeOnLongRest = true }, Spells = [atWill, ritual, restricted]
        } };
        var w = new WorkspaceSnapshot { Revision = 1, Character = c };
        var free = PlayRules.Cast(w, new() { OperationId = "at-will", SourceRevision = 1, SpellId = "mastery-cure", SlotLevel = 1, PaymentResourceId = "none" });
        Assert.AreEqual(4, free.Resources.Single(x => x.Id == "slot1").Remaining);
        Assert.Throws<ArgumentException>(() => PlayRules.Cast(w, new() { OperationId = "free-upcast", SourceRevision = 1, SpellId = "mastery-cure", SlotLevel = 2, PaymentResourceId = "none" }));
        var ritualCast = PlayRules.Cast(w, new() { OperationId = "book-ritual", SourceRevision = 1, SpellId = "detect-magic", SlotLevel = 1, PaymentResourceId = "none", Ritual = true });
        Assert.AreEqual("detect-magic", ritualCast.Play.LastCast!.Preview.SpellId);
        Assert.Throws<ArgumentException>(() => PlayRules.Cast(w, new() { OperationId = "book-normal", SourceRevision = 1, SpellId = "detect-magic", SlotLevel = 1, PaymentResourceId = "slot1" }));
        Assert.Throws<ArgumentException>(() => PlayRules.Cast(w, new() { OperationId = "restricted-slot", SourceRevision = 1, SpellId = "innate-disguise", SlotLevel = 1, PaymentResourceId = "slot1" }));
        var innate = PlayRules.Cast(w, new() { OperationId = "restricted-use", SourceRevision = 1, SpellId = "innate-disguise", SlotLevel = 1, PaymentResourceId = "innate-use" });
        Assert.AreEqual(0, innate.Resources.Single(x => x.Id == "innate-use").Remaining);
        var ritualOnly = ritual with { Id = "ritual-only", RitualOnly = true };
        c = c with { Play = c.Play with { GrantedSpellIds = ["ritual-only"], Spells = [ritualOnly] } };
        Assert.Throws<ArgumentException>(() => PlayRules.Cast(w with { Character = c }, new() { OperationId = "ritual-only-normal", SourceRevision = 1, SpellId = "ritual-only", SlotLevel = 1, PaymentResourceId = "slot1" }));
    }

    [TestMethod]
    public void ReviewedSaveCantripsScaleByCharacterLevelWithoutSlotsOrInventedRiders()
    {
        var c = Caster() with { ClassName = "Wizard", Level = 5, Play = Caster().Play with { KnownSpellIds = ["acid-splash"], Preparation = new() { CanChangeOnLongRest = true } } };
        Assert.AreEqual(new DiceFormula(1, 6), SpellRules.Preview(c with { Level = 1 }, "acid-splash", 0).Dice);
        Assert.AreEqual(new DiceFormula(2, 6), SpellRules.Preview(c, "acid-splash", 0).Dice);
        Assert.AreEqual(new DiceFormula(3, 6), SpellRules.Preview(c with { Level = 11 }, "acid-splash", 0).Dice);
        Assert.AreEqual(new DiceFormula(4, 6), SpellRules.Preview(c with { Level = 17 }, "acid-splash", 0).Dice);
        Assert.AreEqual(new DiceFormula(2, 8), SpellRules.Preview(c, "sacred-flame", 0).Dice);
        Assert.AreEqual(Ability.Wisdom, SpellRules.Preview(c, "vicious-mockery", 0).SaveAbility);
        Assert.AreEqual(Ability.Intelligence, SpellRules.Preview(c, "mind-sliver", 0).SaveAbility);
        Assert.AreEqual("Thunder", SpellRules.Preview(c, "thunderclap", 0).DamageType);
        Assert.AreEqual(Ability.Constitution, SpellRules.Preview(c, "word-of-radiance", 0).SaveAbility);
        var w = new WorkspaceSnapshot { Revision = 1, Character = c };
        var cast = PlayRules.Cast(w, new() { OperationId = "cantrip-cast", SourceRevision = 1, SpellId = "acid-splash", SlotLevel = 0, PaymentResourceId = "none" });
        Assert.AreEqual(4, cast.Resources.Single(x => x.Id == "slot1").Remaining);
        var damage = PlayRules.ResolveCastEffect(w with { Revision = 2, Character = cast }, new() { OperationId = "cantrip-effect", SourceRevision = 2, CastId = "cantrip-cast" }, _ => 3);
        Assert.AreEqual(6, damage.Play.LastCast!.EffectTotal);
        Assert.AreEqual(32, damage.CurrentHp);
        StringAssert.Contains(damage.Play.LastCast.Preview.Note, "table");
    }

    [TestMethod]
    public void SpellNumericHelpUsesDomainBreakdownsForTheSelectedCastingSource()
    {
        var c = Caster();
        var hail = SpellRules.Preview(c, "hail-of-thorns", 2);
        Assert.IsNotNull(hail.CastingModifierCalculation);
        Assert.AreEqual(4, hail.CastingModifierCalculation.Total);
        Assert.IsNotNull(hail.SaveDcCalculation);
        Assert.AreEqual(15, hail.SaveDcCalculation.Total);
        Assert.AreEqual(15, hail.SaveDcCalculation.Terms.Sum(x => x.Value));
        Assert.IsTrue(hail.SaveDcCalculation.Terms.Any(x => x.Label.Contains("Wayfinder")));
        c.AbilityScores[Ability.Charisma] = 20;
        c = c with { Play = c.Play with { Spells = [SpellCatalog.All.Single(x => x.Id == "hail-of-thorns") with { Id = "granted-hail", CastingAbility = Ability.Charisma }] } };
        var grant = SpellRules.Preview(c, "granted-hail", 1);
        Assert.AreEqual(5, grant.CastingModifierCalculation!.Total);
        Assert.AreEqual(16, grant.SaveDcCalculation!.Total);
        Assert.IsTrue(grant.CastingModifierCalculation.Terms.Any(x => x.Label.Contains("Charisma")));
        Assert.IsFalse(grant.CastingModifierCalculation.Terms.Any(x => x.Label.Contains("Wayfinder")));
    }

    [TestMethod]
    public void UnmodeledDurationsAreNotInventedAsInstantaneous()
    {
        var c = Caster() with { Play = Caster().Play with { Spells = [new() { Id = "unknown-duration", Name = "Unknown Duration", Level = 1, Concentration = true }] } };
        var preview = SpellRules.Preview(c, "unknown-duration", 1);
        StringAssert.Contains(preview.DurationText, "unmodeled");
        c = c with { Play = c.Play with { Spells = [c.Play.Spells[0] with { DurationAmount = 2, DurationUnit = "hour" }] } };
        Assert.AreEqual("2 hour", SpellRules.Preview(c, "unknown-duration", 1).DurationText);
    }

    [TestMethod]
    public void ExplicitUnmodeledCharacterSpellPreservesItsSourcePoliciesOverBuiltinIds()
    {
        var manual = new SpellDefinition { Id = "cure-wounds", Name = "Manual Cure Wounds", Source = "Homebrew", Level = 1, Modeled = false, CastingAbility = Ability.Charisma, AtWill = true, AllowsSlotCasting = false };
        var c = Caster() with { Play = Caster().Play with { GrantedSpellIds = [manual.Id], Spells = [manual] } };
        var found = SpellCatalog.Find(c, manual.Id);
        Assert.IsFalse(found.Modeled);
        Assert.AreEqual("Homebrew", found.Source);
        Assert.AreEqual(Ability.Charisma, found.CastingAbility);
        Assert.IsTrue(found.AtWill);
        Assert.IsFalse(found.AllowsSlotCasting);
        var preview = SpellRules.Preview(c, manual.Id, 1);
        Assert.IsFalse(preview.Modeled);
        Assert.IsNull(preview.Dice);
        var cast = PlayRules.Cast(new() { Revision = 1, Character = c }, new() { OperationId = "manual-source", SourceRevision = 1, SpellId = manual.Id, SlotLevel = 1, PaymentResourceId = "none" });
        Assert.AreEqual(4, cast.Resources.Single(x => x.Id == "slot1").Remaining);
    }
}
