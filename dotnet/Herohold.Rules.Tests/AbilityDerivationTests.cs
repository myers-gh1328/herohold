using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass] public sealed class AbilityDerivationTests
{
    [TestMethod] public void BackgroundAndOrderedCappedSourcesExplainOnlyActualIncreases()
    {
        var result=AbilityScoreRules.Calculate(new(){BaseScores=new Dictionary<Ability,int>{{Ability.Strength,17}},
            Background=new(){Primary=Ability.Strength,Secondary=Ability.Dexterity},Adjustments=[
                new(){SourceId="asi4",SourceName="Level4 ASI",Increases=new Dictionary<Ability,int>{{Ability.Strength,2}}},
                new(){SourceId="boon19",SourceName="Epic boon",Maximum=30,Increases=new Dictionary<Ability,int>{{Ability.Strength,1}}},
                new(){SourceId="half-feat",SourceName="Half feat",Increases=new Dictionary<Ability,int>{{Ability.Strength,1}}}]});
        Assert.AreEqual(21,result.Scores[Ability.Strength]);Assert.AreEqual(11,result.Scores[Ability.Dexterity]);
        Assert.AreEqual(2,result.BackgroundBonuses[Ability.Strength]);Assert.AreEqual(2,result.SourceBonuses[Ability.Strength]);
        Assert.AreEqual(21,result.Calculations[Ability.Strength].Terms.Sum(t=>t.Value));
        Assert.AreEqual(20,result.Maximums[Ability.Strength]);
    }
    [TestMethod] public void NativeClassCapstonesApplyOnceAndNeverReduceHigherManualScores()
    {
        var barbarian=AbilityScoreRules.Calculate(new(){ClassName="Barbarian",Level=20,BaseScores=new Dictionary<Ability,int>{{Ability.Strength,20},{Ability.Constitution,27}}});
        Assert.AreEqual(24,barbarian.Scores[Ability.Strength]);Assert.AreEqual(27,barbarian.Scores[Ability.Constitution]);Assert.AreEqual(25,barbarian.Maximums[Ability.Strength]);
        var monk=AbilityScoreRules.Calculate(new(){ClassName="Monk",Level=20,BaseScores=new Dictionary<Ability,int>{{Ability.Dexterity,22},{Ability.Wisdom,20}}});
        Assert.AreEqual(25,monk.Scores[Ability.Dexterity]);Assert.AreEqual(24,monk.Scores[Ability.Wisdom]);
        Assert.AreEqual(20,AbilityScoreRules.Calculate(new(){ClassName="Monk",Level=19,BaseScores=new Dictionary<Ability,int>{{Ability.Wisdom,20}}}).Scores[Ability.Wisdom]);
    }
    [TestMethod] public void BackgroundCapsDoNotLowerManualScoresAndRejectInvalidSelections()
    {
        var input=new AbilityScoreInput{BaseScores=new Dictionary<Ability,int>{{Ability.Strength,22}},Background=new(){Primary=Ability.Strength,Secondary=Ability.Dexterity}};
        Assert.AreEqual(22,AbilityScoreRules.Calculate(input).Scores[Ability.Strength]);
        Assert.Throws<ArgumentException>(()=>AbilityScoreRules.Calculate(input with{Background=new(){Primary=Ability.Strength,Secondary=Ability.Strength}}));
        Assert.Throws<ArgumentException>(()=>AbilityScoreRules.Calculate(input with{Background=new(){Primary=Ability.Strength,AllowedAbilities=[Ability.Wisdom]}}));
    }
    [TestMethod] public void ScoreSourcesRejectDuplicatesNegativeRaisesAndInvalidBounds()
    {
        var source=new ScoreAdjustment{SourceId="asi4",Increases=new Dictionary<Ability,int>{{Ability.Strength,2}}};
        Assert.Throws<ArgumentException>(()=>AbilityScoreRules.Calculate(new(){Adjustments=[source,source]}));
        Assert.Throws<ArgumentException>(()=>AbilityScoreRules.Calculate(new(){Adjustments=[source with{Increases=new Dictionary<Ability,int>{{Ability.Strength,-1}}}]}));
        Assert.Throws<ArgumentException>(()=>AbilityScoreRules.Calculate(new(){BaseScores=new Dictionary<Ability,int>{{Ability.Strength,31}}}));
        Assert.Throws<ArgumentException>(()=>AbilityScoreRules.Calculate(new(){Adjustments=[source with{SourceId=""}]}));
    }
    [TestMethod] public void ScoresAreDetachedAndMissingBaseScoresDefaultToTen()
    {
        var scores=new Dictionary<Ability,int>{{Ability.Strength,15}};
        var input=new AbilityScoreInput{BaseScores=scores};var result=AbilityScoreRules.Calculate(input);
        scores[Ability.Strength]=1;Assert.AreEqual(15,result.Scores[Ability.Strength]);Assert.AreEqual(10,result.Scores[Ability.Wisdom]);
        Assert.AreEqual(15,result.Calculations[Ability.Strength].Total);
    }
}
