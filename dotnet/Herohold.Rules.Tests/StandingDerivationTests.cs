using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass] public sealed class StandingDerivationTests
{
    private static Dictionary<Ability,int> Scores()=>Enum.GetValues<Ability>().ToDictionary(a=>a,a=>a==Ability.Intelligence?14:a==Ability.Wisdom?16:10);
    [TestMethod] public void FeatProficienciesAndExpertiseUsePreFeatBaseSetsExactlyOnce()
    {
        var input=new StandingInput{Level=5,Scores=Scores(),SkillProficiencies=["Perception"],Feats=[new(){Name="Skill Expert",SkillProficiency="Arcana",Expertise="Perception"},new(){Name="Keen Mind",Skill="History"},new(){Name="Observant",Skill="Perception"}]};
        var result=StandingRules.Calculate(input);
        Assert.AreEqual(5,result.Skills["Arcana"].Total);Assert.AreEqual(5,result.Skills["History"].Total);Assert.AreEqual(9,result.Skills["Perception"].Total);
        Assert.AreEqual(3,result.SkillProficiencies.Count);Assert.AreEqual(1,result.Expertise.Count);
        Assert.AreEqual(result.Skills["History"].Total,StandingRules.Calculate(input).Skills["History"].Total);
        Assert.AreEqual(1,input.SkillProficiencies.Count);
    }
    [TestMethod] public void ResilientAndBoonOfSkillGiveTypedDeduplicatedGrants()
    {
        var result=StandingRules.Calculate(new(){Level=5,Scores=Scores(),Feats=[new(){Name="Resilient",Ability=Ability.Constitution},new(){Name="Boon of Skill",Expertise="Stealth"}]});
        Assert.AreEqual(3,result.Saves[Ability.Constitution].Total);Assert.AreEqual(18,result.SkillProficiencies.Count);
        Assert.AreEqual(6,result.Skills["Stealth"].Total);Assert.IsTrue(result.Grants.Any(g=>g.Source=="Resilient"&&g.Target=="Constitution"));
    }
    [TestMethod] public void AlertAndGloomUseFinalModifiersAndNegativeWisdom()
    {
        var scores=Scores();scores[Ability.Dexterity]=12;scores[Ability.Wisdom]=6;
        var result=StandingRules.Calculate(new(){ClassName="Ranger",Subclass="Gloom Stalker",Level=5,Scores=scores,OriginFeats=["Alert","Alert"]});
        Assert.AreEqual(2,result.Initiative.Total);Assert.AreEqual(1,result.Initiative.Terms.Count(t=>t.Label.Contains("Alert")));
    }
    [TestMethod] public void JackOfAllTradesAddsHalfProficiencyOnlyToUntrainedSkills()
    {
        var input=new StandingInput{ClassName="Bard",Level=5,Scores=Scores(),SkillProficiencies=["History"]};
        var result=StandingRules.Calculate(input);
        Assert.AreEqual(3,result.Skills["Arcana"].Total);Assert.AreEqual(5,result.Skills["History"].Total);Assert.AreEqual(0,result.Initiative.Total);
        Assert.AreEqual(2,StandingRules.Calculate(input with{Level=1}).Skills["Arcana"].Total);
    }
    [TestMethod] public void TypedOrdersUseFinalWisdomWithMinimumOneAndNoOtherSkillBonus()
    {
        var scores=Scores();scores[Ability.Wisdom]=6;
        var cleric=StandingRules.Calculate(new(){ClassName="Cleric",Scores=scores,DivineOrder=DivineOrder.Thaumaturge});
        Assert.AreEqual(3,cleric.Skills["Arcana"].Total);Assert.AreEqual(3,cleric.Skills["Religion"].Total);Assert.AreEqual(2,cleric.Skills["Nature"].Total);
        var druid=StandingRules.Calculate(new(){ClassName="Druid",Scores=Scores(),PrimalOrder=PrimalOrder.Magician,Equipment=[new(){Id="wis-charm",Name="Wis charm",Equipped=true,ModifierBonuses=new(){{Ability.Wisdom,1}}}]});
        Assert.AreEqual(6,druid.Skills["Nature"].Total);Assert.AreEqual(2,druid.Skills["Religion"].Total);
    }
    [TestMethod] public void EquipmentChangesModifiersOncePerActiveItemAndUnknownFeatsStayUnmodeled()
    {
        var result=StandingRules.Calculate(new(){Scores=Scores(),Equipment=[new(){Id="active",Name="Charm",Quantity=3,Equipped=true,ModifierBonuses=new(){{Ability.Intelligence,1}}},new(){Id="inactive",Name="Unattuned",Equipped=true,RequiresAttunement=true,ModifierBonuses=new(){{Ability.Intelligence,5}}}],Feats=[new(){Name="Custom campaign feat"}]});
        Assert.AreEqual(3,result.Modifiers[Ability.Intelligence].Total);Assert.AreEqual(3,result.Skills["Arcana"].Total);
        CollectionAssert.AreEqual(new[]{"Custom campaign feat"},result.UnmodeledStandingFeats.ToArray());
    }
    [TestMethod] public void UnknownSkillsOrphanExpertiseAndWrongClassOrderReject()
    {
        Assert.Throws<ArgumentException>(()=>StandingRules.Calculate(new(){Scores=Scores(),SkillProficiencies=["Hacking"]}));
        Assert.Throws<ArgumentException>(()=>StandingRules.Calculate(new(){Scores=Scores(),Expertise=["Arcana"]}));
        Assert.Throws<ArgumentException>(()=>StandingRules.Calculate(new(){ClassName="Wizard",Scores=Scores(),DivineOrder=DivineOrder.Thaumaturge}));
    }
    [TestMethod] public void AllFeatChoiceFieldsAreBoundedEvenWhenTheFeatIsUnmodeled()
    {
        Assert.Throws<ArgumentException>(()=>StandingRules.Calculate(new(){Scores=Scores(),Feats=[new(){Name="Custom feat",Skill=new string('x',201)}]}));
        Assert.Throws<ArgumentException>(()=>StandingRules.Calculate(new(){Scores=Scores(),Feats=[new(){Name="Custom feat",Expertise=new string('x',201)}]}));
        Assert.Throws<ArgumentException>(()=>StandingRules.Calculate(new(){Scores=Scores(),Feats=[new(){Name="Custom feat",SkillProficiency=new string('x',201)}]}));
    }
    [TestMethod] public void RepeatedUnmodeledFeatsRemainDescriptiveButDuplicateModeledFeatsCannotUpgradeThemselves()
    {
        var result=StandingRules.Calculate(new(){Scores=Scores(),Feats=[new(){Name="Elemental Adept"},new(){Name="Elemental Adept"}]});
        CollectionAssert.AreEqual(new[]{"Elemental Adept"},result.UnmodeledStandingFeats.ToArray());
        Assert.Throws<ArgumentException>(()=>StandingRules.Calculate(new(){Scores=Scores(),Feats=[new(){Name="Keen Mind",Skill="Arcana"},new(){Name="Keen Mind",Skill="Arcana"}]}));
    }
    [TestMethod] public void InventoryCapacityMatchesExistingCoreFiveHundredItemBoundary()
    {
        var equipment=Enumerable.Range(0,500).Select(i=>new EquipmentItem{Id="item"+i,Name="Carried item"}).ToList();
        Assert.AreEqual(2,StandingRules.Calculate(new(){Scores=Scores(),Equipment=equipment}).Modifiers[Ability.Intelligence].Total);
        equipment.Add(new(){Id="item500",Name="Overflow"});
        Assert.Throws<ArgumentException>(()=>StandingRules.Calculate(new(){Scores=Scores(),Equipment=equipment}));
    }
}
