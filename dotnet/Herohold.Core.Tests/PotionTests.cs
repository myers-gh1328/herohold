using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]public sealed class PotionTests
{
    private static CharacterState Hurt(int hp,int potions=2)=>Fixtures.Ranger() with
    {
        CurrentHp=hp,Equipment=[new(){Id="potion",Name="Potion of Healing",Quantity=potions,Consumable=true,UseAction=ItemUseAction.BonusAction,HealingDice=new(2,4),HealingBonus=2}]
    };
    private static int Potions(CharacterState c)=>c.Equipment.Single().Quantity;

    [TestMethod]public void DrinkingAPotionSpendsItAndHealsTheRolledAmount()
    {
        var c=SheetRules.UseEquipment(Hurt(20),"potion",healed:7);
        Assert.AreEqual(27,c.CurrentHp);Assert.AreEqual(1,Potions(c));
    }

    [TestMethod]public void HealingStopsAtMaximumHitPoints()
    {
        var c=SheetRules.UseEquipment(Hurt(40),"potion",healed:10);
        Assert.AreEqual(44,c.CurrentHp);Assert.AreEqual(1,Potions(c));
    }

    [TestMethod]public void ATotalTheDiceCannotGiveIsRefusedAndNothingIsSpent()
    {
        foreach(var total in new int?[]{null,3,11})
        {
            var error=Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.UseEquipment(Hurt(20),"potion",healed:total));
            StringAssert.Contains(error.Message,"2d4 + 2: 4 to 10");
        }
        Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.UseEquipment(Fixtures.Ranger(),"potion",healed:5),"An item without healing takes no healing total.");
    }

    [TestMethod]public void APotionAtZeroHitPointsWakesTheCharacterAndClearsDeathSaves()
    {
        var c=Hurt(0) with{Sheet=new(){DeathSaves=new(){Successes=1,Failures=2}}};
        var after=SheetRules.UseEquipment(c,"potion",healed:5);
        Assert.AreEqual(5,after.CurrentHp);Assert.AreEqual(0,after.Sheet.DeathSaves.Failures);
        Assert.ThrowsExactly<ArgumentException>(()=>SheetRules.UseEquipment(Hurt(0) with{Sheet=new(){DeathSaves=new(){Failures=3,Dead=true}}},"potion",healed:5));
    }

    [TestMethod]public void HealingTextReadsAndWritesDiceAndBonus()
    {
        Assert.AreEqual("2d4 + 2",ItemLinking.HealingText(Hurt(1).Equipment.Single()));
        Assert.AreEqual((new DiceFormula(10,4),20),ItemLinking.ParseHealing("10d4+20"));Assert.AreEqual((new DiceFormula(1,8),0),ItemLinking.ParseHealing(" 1d8 "));
        Assert.IsNull(ItemLinking.ParseHealing("two dice"));Assert.IsNull(ItemLinking.ParseHealing("0d4"));Assert.IsNull(ItemLinking.ParseHealing("1d1"));
        Assert.AreEqual((12,42),ItemLinking.HealingRange(new(){HealingDice=new(10,4),HealingBonus=2}));
    }

    [TestMethod]public void HealingOutsideItsBoundsIsInvalid()
    {
        Assert.ThrowsExactly<ArgumentException>(()=>WorkspaceRules.Validate(Fixtures.Ranger() with{Equipment=[new(){Id="x",Name="Odd",HealingBonus=3}]}));
        Assert.ThrowsExactly<ArgumentException>(()=>WorkspaceRules.Validate(Fixtures.Ranger() with{Equipment=[new(){Id="x",Name="Odd",HealingDice=new(0,4)}]}));
    }
}
