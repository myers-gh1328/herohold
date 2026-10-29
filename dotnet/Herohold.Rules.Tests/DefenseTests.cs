using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;

[TestClass]
public sealed class DefenseTests
{
    private static EquipmentItem Item(string name, bool equipped = true) => new(){Id=name.Replace(' ','-'),Name=name,Equipped=equipped};

    [TestMethod]
    public void AllStandardArmorUsesItsOwnBaseAndDexterityCap()
    {
        foreach(var (name, basis, cap) in new[]{
            ("Padded Armor",11,10),("Leather Armor",11,10),("Studded Leather Armor",12,10),
            ("Hide Armor",12,2),("Chain Shirt",13,2),("Scale Mail",14,2),("Breastplate",14,2),("Half Plate Armor",15,2),
            ("Ring Mail",14,0),("Chain Mail",16,0),("Splint Armor",17,0),("Plate Armor",18,0)})
        foreach(var dex in new[]{-2,4})
        foreach(var shield in new[]{false,true})
        {
            var equipment=new List<EquipmentItem>{Item(name)};if(shield)equipment.Add(Item("Shield"));
            var result=ArmorRules.Calculate(new(){DexterityModifier=dex,Equipment=equipment});
            var expected=basis+(cap==0?0:Math.Min(dex,cap))+(shield?2:0);
            Assert.AreEqual(expected,result.Best.Calculation.Total,name+" Dex "+dex);
            Assert.AreEqual(expected,result.Best.Calculation.Terms.Sum(x=>x.Value));
        }
    }

    [TestMethod]
    public void ExactAliasesActivationAndMediumArmorMasterKeepEquipmentSemantics()
    {
        var input=new ArmorInput{DexterityScore=16,DexterityModifier=5,MediumArmorMaster=true,DefenseStyle=true,Equipment=[Item("half plate"),Item("Shield")]};
        Assert.AreEqual(21,ArmorRules.Calculate(input).Best.Calculation.Total);
        Assert.AreEqual(20,ArmorRules.Calculate(input with{DexterityScore=15}).Best.Calculation.Total);
        Assert.AreEqual(15,ArmorRules.Calculate(input with{Equipment=[Item("Plate",false)]}).Best.Calculation.Total);
        Assert.AreEqual(15,ArmorRules.Calculate(input with{Equipment=[Item("Plate") with{RequiresAttunement=true}]}).Best.Calculation.Total);
        Assert.AreEqual(15,ArmorRules.Calculate(input with{Equipment=[Item("Custom Plate")]}).Best.Calculation.Total);
    }

    [TestMethod]
    public void ShieldDisablesMonkAndDanceDefenseWhileDraconicDefenseAllowsIt()
    {
        var input=new ArmorInput{ClassName="Monk",Level=3,DexterityModifier=3,WisdomModifier=4,CharismaModifier=4,Equipment=[Item("Shield")]};
        Assert.AreEqual(15,ArmorRules.Calculate(input).Best.Calculation.Total);
        Assert.AreEqual(15,ArmorRules.Calculate(input with{ClassName="Bard",Subclass="College of Dance"}).Best.Calculation.Total);
        Assert.AreEqual(19,ArmorRules.Calculate(input with{ClassName="Sorcerer",Subclass="Draconic Sorcery"}).Best.Calculation.Total);
        Assert.AreEqual(15,ArmorRules.Calculate(input with{ClassName="Sorcerer",Subclass="Draconic Sorcery",Level=2}).Best.Calculation.Total);
        Assert.AreEqual(18,ArmorRules.Calculate(input with{Equipment=[Item("Plate Armor")]}).Best.Calculation.Total);
        Assert.AreEqual(18,ArmorRules.Calculate(input with{ClassName="Barbarian",ConstitutionModifier=3}).Best.Calculation.Total);
    }

    [TestMethod]
    public void RovingAndFastMovementRespectActiveHeavyArmorAndKeepRovingModes()
    {
        var input=new MovementInput{ClassName="Ranger",Level=6,Equipment=[Item("Plate",false)],Feats=["Speedy","Speedy","Boon of Speed"]};
        var free=MovementRules.Calculate(input);Assert.AreEqual(80,free.Speed);Assert.AreEqual(80,free.ClimbSpeed);Assert.AreEqual(80,free.SwimSpeed);
        var heavy=MovementRules.Calculate(input with{Equipment=[Item("Plate")]});
        Assert.AreEqual(70,heavy.Speed);Assert.AreEqual(70,heavy.ClimbSpeed);Assert.IsFalse(heavy.Terms.Single(x=>x.Name=="Roving").Applies);
        Assert.AreEqual(30,MovementRules.Calculate(new(){ClassName="Ranger",Level=5}).Speed);
        Assert.AreEqual(40,MovementRules.Calculate(new(){ClassName="Barbarian",Level=5}).Speed);
        Assert.AreEqual(30,MovementRules.Calculate(new(){ClassName="Barbarian",Level=5,Equipment=[Item("Ring Mail")]}).Speed);
    }

    [TestMethod]
    public void MonkMovementRequiresNoArmorOrShieldAndReturnsDetachedTerms()
    {
        var input=new MovementInput{ClassName="Monk",Level=10,BaseSpeed=35,UnarmoredMovementBonus=20};
        Assert.AreEqual(55,MovementRules.Calculate(input).Speed);
        Assert.AreEqual(35,MovementRules.Calculate(input with{Equipment=[Item("Shield")]}).Speed);
        Assert.AreEqual(35,MovementRules.Calculate(input with{Equipment=[Item("Padded")]}).Speed);
        Assert.AreEqual(55,MovementRules.Calculate(input with{Equipment=[Item("Shield") with{Quantity=0}]}).Calculation.Terms.Sum(x=>x.Value));
        Assert.Throws<ArgumentException>(()=>ArmorRules.Calculate(new(){Equipment=[Item("Shield"),Item("Shield")]}));
    }
}
