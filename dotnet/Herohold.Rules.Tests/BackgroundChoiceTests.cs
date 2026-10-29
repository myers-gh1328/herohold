using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass] public sealed class BackgroundChoiceTests
{
    [TestMethod] public void BackgroundCanIncreaseAllThreeAllowedAbilitiesByOne()
    {
        var result=AbilityScoreRules.Calculate(new(){Background=new(){Name="Soldier",AllowedAbilities=[Ability.Strength,Ability.Dexterity,Ability.Constitution],SplitEvenly=true}});
        foreach(var ability in new[]{Ability.Strength,Ability.Dexterity,Ability.Constitution})Assert.AreEqual(11,result.Scores[ability]);Assert.AreEqual(10,result.Scores[Ability.Intelligence]);Assert.AreEqual(3,result.BackgroundBonuses.Values.Sum());
    }
    [TestMethod] public void EvenSplitHonorsCapsAndRequiresExactlyThreeDistinctAllowedAbilities()
    {
        var result=AbilityScoreRules.Calculate(new(){BaseScores=new Dictionary<Ability,int>{{Ability.Strength,20},{Ability.Dexterity,19},{Ability.Constitution,10}},Background=new(){Name="Soldier",AllowedAbilities=[Ability.Strength,Ability.Dexterity,Ability.Constitution],SplitEvenly=true}});
        Assert.AreEqual(20,result.Scores[Ability.Strength]);Assert.AreEqual(20,result.Scores[Ability.Dexterity]);Assert.AreEqual(11,result.Scores[Ability.Constitution]);
        Assert.Throws<ArgumentException>(()=>AbilityScoreRules.Calculate(new(){Background=new(){AllowedAbilities=[Ability.Strength,Ability.Dexterity],SplitEvenly=true}}));
    }
}
