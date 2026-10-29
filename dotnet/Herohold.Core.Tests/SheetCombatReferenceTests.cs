using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]
public sealed class SheetCombatReferenceTests
{
    [TestMethod]
    [DataRow(Ability.Strength,12,1)][DataRow(Ability.Dexterity,18,4)][DataRow(Ability.Constitution,8,-1)]
    [DataRow(Ability.Intelligence,10,0)][DataRow(Ability.Wisdom,16,3)][DataRow(Ability.Charisma,14,2)]
    public void AbilityChecksUseOnlyTheResolvedAbilityModifier(Ability ability,int score,int expected)
    {
        var c=Fixtures.Ranger() with{Equipment=[]};c.AbilityScores[ability]=score;
        var after=PlayRules.Roll(new(){Character=c,Revision=1},new(){OperationId="ability-check",SourceRevision=1,Kind=RollKind.Ability,Ability=ability,Name=ability+" check",Mode=DiceMode.Physical,PhysicalD20=[10]});
        Assert.AreEqual(expected,after.Play.LastRoll!.StandingBonus.Total);Assert.AreEqual(10+expected,after.Play.LastRoll.Total);
    }
    [TestMethod]
    [DataRow(10,6,6)][DataRow(11,8,6)][DataRow(19,8,6)][DataRow(20,8,10)]
    public void RangerDamageUpgradesUseRangerLevelsNotMulticlassTotal(int rangerLevel,int dreadSides,int markSides)
    {
        var c=Fixtures.Ranger() with{Level=20,Build=new(){ClassLevels=new(){["Ranger"]=rangerLevel,["Fighter"]=20-rangerLevel}}};
        Assert.AreEqual(dreadSides,RangerRules.DreadfulStrikeDamage(c).Sides);Assert.AreEqual(markSides,RangerRules.HuntersMarkDamage(c).Sides);
    }
    [TestMethod][DataRow(0,false)][DataRow(1,true)][DataRow(2,false)][DataRow(3,false)]
    public void DreadfulStrikeRequiresARecordedWeaponAttackSource(int attackKind,bool allowed)
    {
        var c=Fixtures.Ranger() with{Resources=[new(){Id="dread",Name="Dreadful Strike",Maximum=1,Remaining=1}]};
        var request=System.Text.Json.JsonSerializer.Deserialize<RollRequest>($$"""{"operationId":"typed-attack","sourceRevision":1,"kind":0,"attackKind":{{attackKind}},"mode":1,"physicalD20":[12]}""",new System.Text.Json.JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        var rolled=PlayRules.Roll(new(){Character=c,Revision=1},request);var resolution=new AttackResolutionRequest{OperationId="typed-dread",SourceRevision=2,RollId="typed-attack",Hit=true,BaseFixedDamage=1,DamageModifier=1,DreadfulStrike=true,PhysicalDamageDice=[2,3]};
        if(allowed){var after=PlayRules.ResolveAttack(new(){Character=rolled,Revision=2},resolution);Assert.AreEqual(0,after.Resources.Single().Remaining);}
        else Assert.Throws<ArgumentException>(()=>PlayRules.ResolveAttack(new(){Character=rolled,Revision=2},resolution));
        Assert.AreEqual(1,c.Resources.Single().Remaining);
    }
    [TestMethod][DataRow(false)][DataRow(true)]
    public void FixedUnarmedDamageHasNoImaginaryDiceAndDoesNotDoubleOnCritical(bool critical)
    {
        var c=Fixtures.Ranger();var rolled=PlayRules.Roll(new(){Character=c,Revision=1},new(){OperationId="unarmed-attack",SourceRevision=1,Kind=RollKind.Attack,Mode=DiceMode.Physical,PhysicalD20=[12],Ability=Ability.Strength});
        var after=PlayRules.ResolveAttack(new(){Character=rolled,Revision=2},new(){OperationId="unarmed-hit",SourceRevision=2,RollId="unarmed-attack",Hit=true,Critical=critical,BaseFixedDamage=1,DamageModifier=2,DamageType="Bludgeoning"},_=>throw new AssertFailedException("Fixed damage cannot call RNG."));
        var damage=after.Play.LastRoll!.Damage.Single();Assert.AreEqual(3,damage.Total);Assert.AreEqual(0,damage.Dice.Count);WorkspaceRules.Validate(after);
    }
}
