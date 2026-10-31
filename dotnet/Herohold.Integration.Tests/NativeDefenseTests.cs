using Herohold.Core;
using Herohold.TestSupport;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;

[TestCategory("RulesData")]
[TestClass]
public sealed class NativeDefenseTests
{
    [TestMethod]
    public void NativeArmorCorrectionReachesTypedStateAndCanonicalExportWithoutHealing()
    {
        var original=SampleWorkspace.Create().Character;
        var build=original.Build! with{ClassName="Monk",Subclass="",Level=3,ClassLevels=new(){["Monk"]=3},ClassOptions=new(),HpHistory=null,BaseAbilityScores=Enum.GetValues<Ability>().ToDictionary(a=>a,a=>a==Ability.Dexterity?16:a==Ability.Wisdom?18:10)};
        var source=original with{ClassName="Monk",Subclass="",Level=3,CurrentHp=7,Build=build,Equipment=[new(){Id="shield",Name="Shield",Equipped=true}]};
        var corrected=TestStores.Workflow.Recalculate(CharacterDraft.Of(source),source);
        var dexterity=CharacterCalculations.Ability(corrected,Ability.Dexterity).Total;var wisdom=CharacterCalculations.Ability(corrected,Ability.Wisdom).Total;
        Assert.AreEqual(10+dexterity+2,corrected.ArmorClass,"A Monk with a shield loses Unarmored Defense and keeps the shield.");
        Assert.AreNotEqual(10+dexterity+wisdom,corrected.ArmorClass);
        Assert.AreEqual(7,corrected.CurrentHp,"Recalculating armor never heals.");
        Assert.AreEqual(corrected.ArmorClass,WorkspaceProjection.ExportDocument(corrected)["armor_class"]!.GetValue<int>());
    }
    [TestMethod]
    [DataRow("Arid Land","Fire")]
    [DataRow("Polar Land","Cold")]
    [DataRow("Temperate Land","Lightning")]
    [DataRow("Tropical Land","Poison")]
    public void NaturesWardResistsTheChosenLandsDamageType(string land,string type)
    {
        var original=SampleWorkspace.Create().Character;
        var build=original.Build! with{ClassName="Druid",Subclass="Circle of the Land",Level=10,ClassLevels=new(){["Druid"]=10},ClassOptions=new(){["structured_spell_choice"]=ChoiceValue.Of(land)},HpHistory=null};
        var source=original with{ClassName="Druid",Subclass="Circle of the Land",Level=10,Build=build};
        var druid=TestStores.Workflow.Recalculate(CharacterDraft.Of(source),source);
        Assert.IsTrue(druid.Defenses.Any(d=>d.Kind=="conditionImmunity"&&d.Type=="Poisoned"),"Nature's Ward makes the druid immune to Poisoned.");
        CollectionAssert.AreEqual(new[]{type},druid.Defenses.Where(d=>d.Kind=="resistance").Select(d=>d.Type).ToArray(),"Nature's Ward resists the chosen land's damage type.");
    }
    [TestMethod]
    public void RovingSpeedAndModesUseActiveArmorAndSourceSpeciesBase()
    {
        var original=SampleWorkspace.Create().Character;
        var source=original with{Level=6,Build=original.Build! with{Level=6,ClassLevels=new(){["Ranger"]=6}},Equipment=[new(){Id="plate",Name="Plate",Equipped=true}]};
        var heavy=TestStores.Builder.Calculate(CharacterDraft.Of(source),source,false);
        Assert.AreEqual(30,heavy.Speed);Assert.AreEqual(30,heavy.DerivedResources["climb_speed"].Number);
        source=source with{Equipment=[source.Equipment[0] with{Equipped=false}]};
        var free=TestStores.Builder.Calculate(CharacterDraft.Of(source),source,false);
        Assert.AreEqual(40,free.Speed);Assert.AreEqual(40,free.DerivedResources["swim_speed"].Number);
    }
}
