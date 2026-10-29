using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]
public sealed class ExhaustionSheetTests
{
    [TestMethod]
    [DataRow(0,0,30)] [DataRow(1,-2,25)] [DataRow(2,-4,20)] [DataRow(5,-10,5)]
    public void ExhaustionChangesD20BonusesAndSpeedExactlyOnceWithoutChangingBaseModifiers(int level,int penalty,int speed)
    {
        var normal=Fixtures.Ranger();var c=normal with{Exhaustion=level};
        Assert.AreEqual(CharacterCalculations.Ability(normal,Ability.Wisdom).Total,CharacterCalculations.Ability(c,Ability.Wisdom).Total);
        Assert.AreEqual(CharacterCalculations.Save(normal,Ability.Wisdom).Total+penalty,CharacterCalculations.Save(c,Ability.Wisdom).Total);
        Assert.AreEqual(CharacterCalculations.Skill(normal,"Perception",Ability.Wisdom).Total+penalty,CharacterCalculations.Skill(c,"Perception",Ability.Wisdom).Total);
        Assert.AreEqual(CharacterCalculations.Attack(normal,Ability.Dexterity,true).Total+penalty,CharacterCalculations.Attack(c,Ability.Dexterity,true).Total);
        Assert.AreEqual(CharacterCalculations.Initiative(normal).Total+penalty,CharacterCalculations.Initiative(c).Total);
        Assert.AreEqual(CharacterCalculations.Ability(normal,Ability.Wisdom).Total+penalty,CharacterCalculations.AbilityCheck(c,Ability.Wisdom).Total);
        Assert.AreEqual(CharacterCalculations.Ability(normal,Ability.Wisdom).Total+CharacterCalculations.ProficiencyBonus(c.Level)+penalty,CharacterCalculations.SpellAttack(c,Ability.Wisdom).Total);
        Assert.AreEqual(speed,CharacterCalculations.Speed(c));Assert.AreEqual(30,c.Speed);Assert.AreEqual(normal.ArmorClass,c.ArmorClass);
        Assert.AreEqual(level==0?0:1,CharacterCalculations.Save(c,Ability.Wisdom).Terms.Count(t=>t.Label.StartsWith("Exhaustion")));
    }
    [TestMethod]public void NaturalDeathSaveExceptionsRemainRawWhileOrdinaryDeathSaveTakesExhaustionPenalty()
    {
        var c=Fixtures.Ranger() with{CurrentHp=0,Exhaustion=1};Assert.AreEqual(1,SheetRules.RecordDeathSave(c,11).Sheet.DeathSaves.Failures);Assert.AreEqual(1,SheetRules.RecordDeathSave(c,12).Sheet.DeathSaves.Successes);Assert.AreEqual(2,SheetRules.RecordDeathSave(c,1).Sheet.DeathSaves.Failures);Assert.AreEqual(1,SheetRules.RecordDeathSave(c,20).CurrentHp);
    }
    [TestMethod]public void ExhaustionSixKillsAndLoweringItDoesNotRevive()
    {
        var dead=WorkspaceRules.SetExhaustion(Fixtures.Ranger(),6);Assert.IsTrue(dead.Sheet.DeathSaves.Dead);Assert.AreEqual(0,dead.CurrentHp);Assert.IsTrue(WorkspaceRules.SetExhaustion(dead,5).Sheet.DeathSaves.Dead);
    }
}
