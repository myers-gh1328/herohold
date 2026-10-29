using Herohold.Core;
using Herohold.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass]
public sealed class MulticlassBoundaryTests
{
    [TestMethod]
    [DataRow("Fighter",12,13,12,12,12,true)][DataRow("Fighter",13,12,12,12,12,true)][DataRow("Fighter",12,12,13,13,13,false)]
    [DataRow("Ranger",13,13,13,12,13,false)][DataRow("Ranger",13,12,13,13,13,false)][DataRow("Ranger",12,13,12,13,12,true)]
    [DataRow("Paladin",13,12,12,12,13,true)][DataRow("Paladin",13,13,13,13,12,false)]
    [DataRow("Monk",12,13,12,13,12,true)][DataRow("Monk",13,13,13,12,13,false)]
    [DataRow("Wizard",13,13,12,13,13,false)][DataRow("Wizard",12,12,13,12,12,true)]
    public void EntryPrerequisitesUseScoreThirteenAndEachClassesAndOrForm(string name,int str,int dex,int intelligence,int wis,int cha,bool allowed)
    {
        var scores=new Dictionary<Ability,int>{{Ability.Strength,str},{Ability.Dexterity,dex},{Ability.Constitution,10},{Ability.Intelligence,intelligence},{Ability.Wisdom,wis},{Ability.Charisma,cha}};
        Assert.AreEqual(allowed,MulticlassRules.MeetsPrerequisite(name,scores));
    }
    [TestMethod]
    public void EveryExistingClassMustQualifyAndTheSourceScoreDictionaryIsNotModified()
    {
        var scores=Enum.GetValues<Ability>().ToDictionary(a=>a,_=>13);scores[Ability.Wisdom]=12;var tracks=new[]{new ClassTrack("class-ranger","Ranger","XPHB",3,"",new Dictionary<string,ChoiceValue>())};var issues=MulticlassRules.ValidateEntry(tracks,"Wizard",scores);Assert.AreEqual(1,issues.Count);StringAssert.Contains(issues[0].Message,"Ranger");Assert.AreEqual(12,scores[Ability.Wisdom]);
    }
}
