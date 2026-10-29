using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Core.Tests;

[TestClass]
public sealed class CommonSpellTests
{
    [TestMethod]
    public void CommonSaveSpellsUseReviewedBaseDiceAndHigherSlotsWithoutAbilityDamage()
    {
        var c = SpellTests.Caster();
        foreach (var (id, baseSlot, count, sides, save, type) in new[]
        {
            ("fireball", 3, 8, 6, Ability.Dexterity, "Fire"),
            ("burning-hands", 1, 3, 6, Ability.Dexterity, "Fire"),
            ("thunderwave", 1, 2, 8, Ability.Constitution, "Thunder")
        })
        {
            var basic = SpellRules.Preview(c, id, baseSlot);
            var higher = SpellRules.Preview(c, id, baseSlot + 2);
            Assert.IsTrue(basic.Modeled);
            Assert.AreEqual(new DiceFormula(count, sides), basic.Dice);
            Assert.AreEqual(new DiceFormula(count + 2, sides), higher.Dice);
            Assert.AreEqual(save, higher.SaveAbility);
            Assert.AreEqual(type, higher.DamageType);
            Assert.AreEqual("target-save-half", higher.Resolution);
            Assert.AreEqual(0, higher.Modifier);
        }
        Assert.AreEqual(20, SpellRules.Preview(c, "fireball", 5).RadiusFt);
    }

    [TestMethod]
    public void HealingWordUses2024DiceAndSelectedSourceModifierWithoutHealingCaster()
    {
        var c = SpellTests.Caster();
        c = c with { Play = c.Play with { PreparedSpellIds = ["healing-word"] } };
        Assert.AreEqual(new DiceFormula(2, 4), SpellRules.Preview(c, "healing-word", 1).Dice);
        var preview = SpellRules.Preview(c, "healing-word", 2);
        Assert.AreEqual(new DiceFormula(4, 4), preview.Dice);
        Assert.AreEqual(4, preview.Modifier);
        var cast = PlayRules.Cast(new() { Revision = 1, Character = c }, new()
        {
            OperationId = "healing-word", SourceRevision = 1, SpellId = "healing-word",
            SlotLevel = 2, PaymentResourceId = "slot2", Mode = DiceMode.Physical
        });
        var resolved = PlayRules.ResolveCastEffect(new() { Revision = 2, Character = cast }, new()
        {
            OperationId = "healing-word-effect", SourceRevision = 2, CastId = "healing-word",
            PhysicalDice = [1, 2, 3, 4]
        }, _ => throw new Exception("Physical dice must not invoke RNG."));
        Assert.AreEqual(14, resolved.Play.LastCast!.EffectTotal);
        Assert.AreEqual(c.CurrentHp, resolved.CurrentHp);
        Assert.AreEqual(1, resolved.Resources.Single(x => x.Id == "slot2").Remaining);
    }
}
