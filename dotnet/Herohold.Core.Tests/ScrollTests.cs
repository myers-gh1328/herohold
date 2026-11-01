using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass]public sealed class ScrollTests
{
    private static ScrollSpell Spell(string name,int level,string castingTime="Action",bool concentration=false,params string[] classes)=>new(new(){Id=name.ToLowerInvariant().Replace(' ','-'),Name=name,Level=level,CastingTime=castingTime,Concentration=concentration},classes);
    private static readonly ScrollSpell FogCloud=Spell("Fog Cloud",1,"Action",true,"Druid","Ranger","Sorcerer","Wizard");
    private static readonly ScrollSpell SpeakWithDead=Spell("Speak with Dead",3,"Action",false,"Bard","Cleric","Wizard");
    private static readonly ScrollSpell Conjure=Spell("Conjure Animals",3,"Action",true,"Druid","Ranger");
    private static readonly ScrollSpell Ritualish=Spell("Find Traps",2,"1 minute",false,"Ranger");
    /// <summary>A level 5 Ranger with level 1 and 2 slots, holding one scroll of <paramref name="scroll"/>.</summary>
    private static CharacterState Reader(ScrollSpell scroll)=>Fixtures.Ranger() with
    {
        Play=new(){SpellcastingAbility=Ability.Wisdom},
        Resources=[new(){Id="slot1",Name="Level 1 slots",SlotLevel=1,Maximum=4,Remaining=4},new(){Id="slot2",Name="Level 2 slots",SlotLevel=2,Maximum=2,Remaining=2}],
        Equipment=[new(){Id="scroll",Name="Spell Scroll (Level "+scroll.Spell.Level+")",Quantity=1,Consumable=true,ScrollSpell=scroll.Spell.Name}]
    };

    [TestMethod]public void TheScrollsNumbersFollowTheSpellLevel()
    {
        Assert.AreEqual((13,5),ScrollRules.Numbers(0));Assert.AreEqual((15,7),ScrollRules.Numbers(3));Assert.AreEqual((17,9),ScrollRules.Numbers(5));Assert.AreEqual((19,11),ScrollRules.Numbers(9));
        Assert.AreEqual(13,ScrollRules.CheckDc(3));
    }

    [TestMethod]public void ASpellOffYourListIsUnintelligibleAndNothingIsSpent()
    {
        var c=Reader(SpeakWithDead);
        StringAssert.Contains(ScrollRules.Reason(c,c.Equipment.Single(),SpeakWithDead),"not on your spell list");
        Assert.ThrowsExactly<ArgumentException>(()=>ScrollRules.Read(c,"scroll",SpeakWithDead,null,false));
        var cleric=c with{Build=new(){ClassLevels=new(){["Ranger"]=3,["Cleric"]=2}}};
        Assert.IsNull(ScrollRules.Reason(cleric,cleric.Equipment.Single(),SpeakWithDead),"A Cleric level puts it on your list.");
    }

    [TestMethod]public void ASpellYouCanCastIsCastWithoutACheckAndStartsConcentration()
    {
        var c=Reader(FogCloud);
        Assert.IsFalse(ScrollRules.NeedsCheck(c,FogCloud));
        var read=ScrollRules.Read(c,"scroll",FogCloud,null,false);
        Assert.AreEqual(0,read.Equipment.Single().Quantity);
        var effect=read.Effects.Single();Assert.AreEqual("Fog Cloud",effect.Name);Assert.IsTrue(effect.Concentration);StringAssert.Contains(effect.Note,"save DC 13, attack +5");
        Assert.ThrowsExactly<ArgumentException>(()=>ScrollRules.Read(c,"scroll",FogCloud,12,false),"No check is rolled for a spell you can cast.");
    }

    [TestMethod]public void AHigherLevelSpellNeedsTheCheckAndAFailureLosesTheScroll()
    {
        var c=Reader(Conjure);
        Assert.IsTrue(ScrollRules.NeedsCheck(c,Conjure),"Level 3 is above this Ranger's level 2 slots.");
        Assert.AreEqual(3,ScrollRules.Check(c).Total,"Wisdom 16.");
        Assert.ThrowsExactly<ArgumentException>(()=>ScrollRules.Read(c,"scroll",Conjure,null,false));
        var failed=ScrollRules.Read(c,"scroll",Conjure,9,false);
        Assert.IsFalse(ScrollRules.CheckSucceeds(c,Conjure,9));Assert.AreEqual(0,failed.Equipment.Single().Quantity);Assert.AreEqual(0,failed.Effects.Count,"The spell fades.");
        var cast=ScrollRules.Read(c,"scroll",Conjure,10,false);
        Assert.IsTrue(ScrollRules.CheckSucceeds(c,Conjure,10));Assert.AreEqual("Conjure Animals",cast.Effects.Single().Name);
    }

    [TestMethod]public void InCombatTheScrollTakesTheSpellsCastingTime()
    {
        CharacterState Fight(CharacterState c)=>CombatRules.Transition(c,new(){Kind=CombatTransitionKind.Enter,NewEncounter=true,EncounterId="e",OwnTurnNow=true,TableBoundaryConfirmed=true,BoundaryId="t1"});
        var c=Fight(Reader(FogCloud));
        Assert.ThrowsExactly<ArgumentException>(()=>ScrollRules.Read(c,"scroll",FogCloud,null,false),"The table facts are confirmed first.");
        var read=ScrollRules.Read(c,"scroll",FogCloud,null,true);
        Assert.IsFalse(read.Combat.ActionAvailable);Assert.IsTrue(read.Combat.BonusActionAvailable);
        var slow=Fight(Reader(Ritualish));
        StringAssert.Contains(ScrollRules.Reason(slow,slow.Equipment.Single(),Ritualish),"longer than a turn");
    }

    [TestMethod]public void AScrollMustHoldTheSpellItIsReadAs()
    {
        var c=Reader(FogCloud);
        Assert.ThrowsExactly<ArgumentException>(()=>ScrollRules.Read(c,"scroll",Conjure,10,false));
        Assert.IsNotNull(ScrollRules.Reason(c,c.Equipment.Single(),null),"A spell the rules data does not know cannot be read.");
    }
}
