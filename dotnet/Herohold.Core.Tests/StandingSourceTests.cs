using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass] public sealed class StandingSourceTests
{
    private static CharacterState Character()=>new(){Id="source-character",Name="Source character",AbilityScores=Enum.GetValues<Ability>().ToDictionary(a=>a,_=>10),StandingAdjustments=new(){{"skill:Arcana",2}},StandingAdjustmentTerms=new(){{"skill:Arcana",[new("Jack of All Trades · half proficiency",2)]}}};
    [TestMethod] public void NamedNativeAdjustmentSourcesAppearInCalculationBreakdown()
    {
        var result=CharacterCalculations.Skill(Character(),"Arcana",Ability.Intelligence);
        Assert.AreEqual(2,result.Total);Assert.IsTrue(result.Terms.Any(t=>t.Label=="Jack of All Trades · half proficiency"));
        Assert.IsFalse(result.Terms.Any(t=>t.Label.StartsWith("Additional character features")));
    }
    [TestMethod] public void IncorrectSourceSumsRejectAndSourceListsAreDetachedByMutations()
    {
        var original=Character();original.StandingAdjustmentTerms["skill:Arcana"][0]=new("Wrong sum",3);
        Assert.Throws<ArgumentException>(()=>WorkspaceRules.Validate(original));
        original=Character();var changed=WorkspaceRules.AdjustHp(original,0);
        changed.StandingAdjustmentTerms["skill:Arcana"].Clear();
        Assert.AreEqual(1,original.StandingAdjustmentTerms["skill:Arcana"].Count);
    }
}
