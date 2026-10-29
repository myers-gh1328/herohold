using Herohold.TestSupport;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Rules;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestClass]
public sealed class ProductionSpellAttackTests
{
    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow("Fire Bolt",1,0,1,1,10)] [DataRow("Fire Bolt",17,0,1,4,10)]
    [DataRow("Eldritch Blast",1,0,1,1,10)] [DataRow("Eldritch Blast",5,0,2,1,10)] [DataRow("Eldritch Blast",11,0,3,1,10)] [DataRow("Eldritch Blast",17,0,4,1,10)]
    [DataRow("Scorching Ray",5,2,3,2,6)] [DataRow("Scorching Ray",5,3,4,2,6)]
    public void ActualCatalogCastAllowsExactlyItsFiniteAttacksAndEachHitUsesPerHitDamage(string name,int level,int slot,int attacks,int damageDice,int sides)
    {
        foreach(var combat in new[]{false,true})
        {
        var c=FromCatalog(name,level,slot);var spell=c.Play.Spells.Single(s=>s.Name==name);if(combat)c=CombatRules.Transition(c,new(){Kind=CombatTransitionKind.Enter,NewEncounter=true,EncounterId="real-catalog-encounter",OwnTurnNow=true,TableBoundaryConfirmed=true,BoundaryId="first-turn"});
        c=PlayRules.Cast(new(){Character=c},new(){SpellId=spell.Id,SlotLevel=slot,PaymentResourceId=slot==0?"none":"slot"+slot,Mode=DiceMode.Physical,CombatContext=CombatRules.Context(c,"spell:"+spell.Id)});
        Assert.AreEqual(attacks,c.Play.LastCast!.Preview.AttackRollCount,"Actual catalog metadata must reach the committed cast receipt.");Assert.AreEqual(attacks,c.Play.LastCast!.AttacksRemaining);Assert.AreEqual(damageDice,c.Play.LastCast.Preview.Dice!.Count);Assert.AreEqual(sides,c.Play.LastCast.Preview.Dice.Sides);if(combat)Assert.IsFalse(c.Combat.ActionAvailable);
        for(var i=0;i<attacks;i++)
        {
            c=PlayRules.Roll(new(){Character=c},new(){Kind=RollKind.SpellAttack,Ability=name=="Eldritch Blast"?Ability.Charisma:Ability.Intelligence,Mode=DiceMode.Physical,PhysicalD20=[12],CombatContext=CombatRules.Context(c,"spell:"+spell.Id)});Assert.AreEqual(attacks-i-1,c.Play.LastCast!.AttacksRemaining);
            c=PlayRules.ResolveAttack(new(){Character=c},new(){RollId=c.Play.LastRoll!.Id,Hit=attacks==1||i!=0,BaseDamage=c.Play.LastCast!.Preview.Dice!,DamageModifier=0,DamageType=c.Play.LastCast.Preview.DamageType??"",PhysicalDamageDice=attacks>1&&i==0?[]:Enumerable.Repeat(2,damageDice).ToList()});Assert.AreEqual(attacks>1&&i==0?0:damageDice*2,c.Play.LastRoll!.Damage.Sum(t=>t.Total));
        }
        Assert.ThrowsExactly<ArgumentException>(()=>PlayRules.Roll(new(){Character=c},new(){Kind=RollKind.SpellAttack,Ability=Ability.Charisma,Mode=DiceMode.Physical,PhysicalD20=[12],CombatContext=CombatRules.Context(c,"spell:"+spell.Id)}));if(slot>0)Assert.AreEqual(1,c.Resources.Single(p=>p.Id=="slot"+slot).Remaining,"One casting pays once, not once per ray.");
        }
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public void ActualDigitalFireBoltRollsOnlyItsFourPerHitDamageDiceAtTotalLevelSeventeen()
    {
        var c=FromCatalog("Fire Bolt",17,0);var spell=c.Play.Spells.Single();c=PlayRules.Cast(new(){Character=c},new(){SpellId=spell.Id,SlotLevel=0,PaymentResourceId="none",Mode=DiceMode.Digital});c=PlayRules.Roll(new(){Character=c},new(){Kind=RollKind.SpellAttack,Ability=Ability.Intelligence,Mode=DiceMode.Digital,CombatContext=CombatRules.Context(c,"spell:"+spell.Id)},_=>12);var calls=0;c=PlayRules.ResolveAttack(new(){Character=c},new(){RollId=c.Play.LastRoll!.Id,Hit=true,BaseDamage=c.Play.LastCast!.Preview.Dice!,DamageType="Fire",DamageModifier=0},_=>{calls++;return 2;});Assert.AreEqual(4,calls);Assert.AreEqual(8,c.Play.LastRoll!.Damage.Single().Total);Assert.AreEqual(0,c.Play.LastCast!.AttacksRemaining);
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public void TrustedLegacySpellReferencesGainReviewedExecutionWithoutRewritingStoredDefinition()
    {
        var c=FromCatalog("Fire Bolt",17,0);c=c with{Play=c.Play with{Spells=c.Play.Spells.Select(s=>s with{AttackRollKind=null,AttackPattern=SpellAttackPattern.Unmodeled,Modeled=false,Dice=null,CharacterLevelScaling=[]}).ToList()}};var stored=c.Play.Spells.Single();var preview=SpellRules.Preview(c,stored.Id,0);Assert.AreEqual(1,preview.AttackRollCount);Assert.AreEqual(4,preview.Dice!.Count);Assert.IsFalse(stored.Modeled);Assert.IsNull(stored.AttackRollKind);
    }
    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow("Produce Flame")][DataRow("Flame Blade")][DataRow("Chromatic Orb")][DataRow("Steel Wind Strike")]
    public void RawAttackFlagDoesNotInventFiniteAttacksForUnmodeledTimingOrConditionalTargets(string name)
    {
        var c=FromCatalog(name,7,3);var spell=c.Play.Spells.Single(s=>s.Name==name);Assert.IsNotNull(spell.AttackRollKind);Assert.IsNull(SpellRules.Preview(c,spell.Id,spell.Level).AttackRollCount);
    }
    internal static CharacterState FromCatalog(string name,int level,int slot)
    {
        var record=NativeCatalogIntegrationTests.Snapshot().Spell(name)!;var raw=CanonicalCharacterRules.Create();raw["character_id"]="production-spell-attacks";raw["name"]="Catalog spell attacks";raw["class"]=name=="Eldritch Blast"?"Warlock":"Wizard";raw["level"]=level;raw["hit_points"]=new JsonObject{["maximum"]=20,["current"]=20};raw["spells"]=new JsonObject{["cantrips"]=record.Level==0?new JsonArray(name):new JsonArray(),["prepared"]=record.Level>0?new JsonArray(name):new JsonArray()};
        var calculation=TestStores.Builder.Calculate(TestStores.Builder.Import(raw),null,false);
        return WorkspaceProjection.FromCalculation(calculation with{Spells=[WorkspaceProjection.MapSpell(record,calculation.Build.ClassName)],SpellcastingAbility=name=="Eldritch Blast"?Ability.Charisma:Ability.Intelligence,
            Resources=slot==0?[]:[new("slot"+slot,"Selected spell slot",2,"long_rest",slot)],Grants=[],GrantedSpellNames=[],MaximumHp=20,CurrentHp=20});
    }
}
