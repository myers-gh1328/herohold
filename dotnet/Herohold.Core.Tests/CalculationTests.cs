using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Core.Tests;

[TestClass]
public sealed class CalculationTests
{
    [TestMethod]
    public void AbilityModifierFloorsNegativeOddScores()
    {
        Assert.AreEqual(-1, CharacterCalculations.AbilityModifier(9));
        Assert.AreEqual(-5, CharacterCalculations.AbilityModifier(1));
        Assert.AreEqual(0, CharacterCalculations.AbilityModifier(10));
        Assert.AreEqual(4, CharacterCalculations.AbilityModifier(18));
    }

    [TestMethod]
    public void CalculationsUseOnlyActiveItemsAndExplainEachContribution()
    {
        var c = Fixtures.Ranger();
        Assert.AreEqual(3, CharacterCalculations.ProficiencyBonus(5));
        Assert.AreEqual(6, CharacterCalculations.ProficiencyBonus(20));
        Assert.AreEqual(4, CharacterCalculations.Ability(c, Ability.Wisdom).Total);
        Assert.AreEqual(10, CharacterCalculations.Skill(c, "Perception", Ability.Wisdom).Total);
        Assert.AreEqual(7, CharacterCalculations.Save(c, Ability.Dexterity).Total);
        Assert.AreEqual(4, CharacterCalculations.Save(c, Ability.Wisdom).Total);
        Assert.AreEqual(9, CharacterCalculations.Attack(c, Ability.Dexterity, true, 2).Total);
        Assert.AreEqual(8, CharacterCalculations.Initiative(c).Total);
        var unequipped = c with { Equipment = [c.Equipment[0] with { Equipped = false }] };
        Assert.AreEqual(3, CharacterCalculations.Ability(unequipped, Ability.Wisdom).Total);
        var unattuned = c with { Equipment = [c.Equipment[0] with { Attuned = false }] };
        Assert.AreEqual(3, CharacterCalculations.Ability(unattuned, Ability.Wisdom).Total);
        var empty = c with { Equipment = [c.Equipment[0] with { Quantity = 0 }] };
        Assert.AreEqual(3, CharacterCalculations.Ability(empty, Ability.Wisdom).Total);
        var calculation = CharacterCalculations.Skill(c, "Perception", Ability.Wisdom);
        Assert.AreEqual(calculation.Total, calculation.Terms.Sum(x => x.Value));
        Assert.AreEqual(16, c.AbilityScores[Ability.Wisdom]);
        Assert.AreEqual(32, c.CurrentHp);
    }

    [TestMethod]
    public void TransitionalFeatureAdjustmentsApplyAtTheirOwnCalculationLevelOnce()
    {
        var ranger = Fixtures.Ranger() with { StandingAdjustments = new() { ["ability:Wisdom"] = 1, ["skill:Perception"] = 2, ["save:Wisdom"] = 3, ["initiative"] = 1 } };
        Assert.AreEqual(5, CharacterCalculations.Ability(ranger, Ability.Wisdom).Total);
        Assert.AreEqual(13, CharacterCalculations.Skill(ranger, "Perception", Ability.Wisdom).Total);
        Assert.AreEqual(8, CharacterCalculations.Save(ranger, Ability.Wisdom).Total);
        Assert.AreEqual(10, CharacterCalculations.Initiative(ranger).Total);
        var bard = Fixtures.Ranger() with { ClassName = "Bard", Subclass = "Lore", StandingAdjustments = new() { ["skill:Athletics"] = 1 } };
        Assert.AreEqual(2, CharacterCalculations.Skill(bard, "Athletics", Ability.Strength).Total);
        var explanation = CharacterCalculations.Skill(ranger, "Perception", Ability.Wisdom);
        Assert.AreEqual(explanation.Total, explanation.Terms.Sum(x => x.Value));
        Assert.AreEqual(1, explanation.Terms.Count(x => x.Label.Contains("Wayfinder")));
    }
}
