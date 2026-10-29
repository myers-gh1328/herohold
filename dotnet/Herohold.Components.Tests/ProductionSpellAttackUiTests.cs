using Herohold.TestSupport;
using Bunit;
using Herohold.Core;
using Herohold.Rules;
using Herohold.Web.Components.Pages;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json.Nodes;
namespace Herohold.Components.Tests;

[TestClass]
public sealed class ProductionSpellAttackUiTests
{
    [TestCategory("RulesData")]
    [TestMethod]
    [DataRow("Fire Bolt",1,false,1,1)]
    [DataRow("Eldritch Blast",5,true,2,1)]
    public async Task ActualCatalogSpellRendersFinitePhysicalAttackAndPerHitDamageInBothSheetModes(string name,int level,bool combat,int attacks,int dice)
    {
        var root=new DirectoryInfo(Environment.CurrentDirectory);while(root is not null&&!File.Exists(Path.Combine(root.FullName,"data/imported/5etools/classes.json")))root=root.Parent;
        var data=Path.Combine(root!.FullName,"data/imported/5etools");string Read(string file)=>File.ReadAllText(Path.Combine(data,file+".json"));
        var snapshot=RulesCatalogSnapshot.Parse(Read("classes"),Read("species"),Read("backgrounds"),Read("feats"),Read("spells"),Read("equipment"),Read("weapons"),File.ReadAllText(Path.Combine(root.FullName,"data/raw/5etools/spells/spells-xphb.json")));
        var record=snapshot.Spell(name)!;
        var raw=CanonicalCharacterRules.Create();raw["character_id"]="mounted-real-spell";raw["name"]="Mounted spell sheet";raw["class"]=name=="Eldritch Blast"?"Warlock":"Wizard";raw["level"]=level;raw["hit_points"]=new JsonObject{["current"]=20,["maximum"]=20};raw["spells"]=new JsonObject{["cantrips"]=new JsonArray(name)};
        var calculation=TestStores.Builder.Calculate(TestStores.Builder.Import(raw),null,false);
        var c=WorkspaceProjection.FromCalculation(calculation with{Spells=[WorkspaceProjection.MapSpell(record,calculation.Build.ClassName)],SpellcastingAbility=name=="Eldritch Blast"?Ability.Charisma:Ability.Intelligence,Grants=[],GrantedSpellNames=[],MaximumHp=20,CurrentHp=20});
        var directory=Path.Combine(Path.GetTempPath(),"herohold-production-spell-ui-"+Guid.NewGuid().ToString("N"));
        try
        {
            var repo=TestStores.Open(directory);await repo.SaveAsync(new(){Character=c},0);var session=new WorkspaceSession(repo);await session.SwitchCharacterAsync(c.Id);
            using var ctx=new BunitContext();ctx.JSInterop.Mode=JSRuntimeMode.Loose;ctx.Services.AddSingleton(session).AddSingleton(new HeroholdFeatures(Combat:true));var cut=ctx.Render<Home>();
            Task Click(string selector)=>cut.InvokeAsync(()=>cut.Find(selector).ClickAsync(new()));
            Task Change(string selector,object value)=>cut.InvokeAsync(()=>cut.Find(selector).ChangeAsync(new(){Value=value}));
            await Change("[data-testid='preferred-dice-mode']",nameof(DiceMode.Physical));
            if(combat){await Click("[data-testid='enter-combat']");await Change("[data-testid='combat-own-turn-now']",true);await Click("[data-testid='confirm-enter-combat']");cut.WaitForAssertion(()=>Assert.IsTrue(session.Character!.Combat.Enabled));}
            var spell=session.Character!.Play.Spells.Single(s=>s.Name==name);await Click(combat?$"[data-combat-cast='{spell.Id}']":$"[data-testid='cast-{spell.Id}']");await Click("[data-testid='confirm-cast']");cut.WaitForAssertion(()=>Assert.AreEqual(attacks,session.Character!.Play.LastCast!.AttacksRemaining));
            Assert.AreEqual(0,cut.FindAll("[data-testid='roll-cast-effect']").Count,"Per-hit damage must never be rolled once for the entire casting.");
            await Click("#spell-cast-dialog [aria-label='Close casting']");
            await Change("[data-testid='preferred-dice-mode']",nameof(DiceMode.Digital));
            await Click(combat?"[data-testid='combat-resume-cast']":"[data-testid='resume-pending-cast']");
            await Click("[data-testid='roll-spell-attack']");Assert.IsNull(session.Character!.Play.LastRoll,"Physical casting receipt must not generate hidden Digital RNG when the global preference changes.");
            await Change("#physical-d20","12");await Click("[data-testid='roll-digital']");cut.WaitForAssertion(()=>Assert.IsNotNull(session.Character!.Play.LastRoll));
            Assert.AreEqual(DiceMode.Physical,session.Character!.Play.LastRoll!.Mode);Assert.AreEqual(AttackKind.Spell,session.Character.Play.LastRoll.AttackKind);Assert.AreEqual(attacks-1,session.Character.Play.LastCast!.AttacksRemaining);
            await Click("[data-testid='close-roll']");await Click(combat?"[data-testid='combat-resume-roll']":"[data-testid='resume-pending-roll']");
            Assert.AreEqual(12,session.Character!.Play.LastRoll!.ChosenD20);Assert.AreEqual(attacks-1,session.Character.Play.LastCast!.AttacksRemaining);
            cut.WaitForElement("#physical-damage-dice");await Change("#physical-damage-dice",string.Join(",",Enumerable.Repeat(3,dice)));await Click("[data-testid='attack-hit']");cut.WaitForAssertion(()=>Assert.IsTrue(session.Character!.Play.LastRoll!.Resolved));Assert.AreEqual(dice*3,session.Character!.Play.LastRoll!.Damage.Sum(t=>t.Total));Assert.AreEqual("Spell",session.Character.Play.LastRoll.Damage.Single().Name);
            if(attacks>1){await Click("[data-testid='next-spell-attack']");await Change("#physical-d20","12");await Click("[data-testid='roll-digital']");cut.WaitForAssertion(()=>Assert.IsFalse(session.Character!.Play.LastRoll!.Resolved));await Click("[data-testid='attack-miss']");cut.WaitForAssertion(()=>Assert.IsTrue(session.Character!.Play.LastRoll!.Resolved));Assert.AreEqual(0,session.Character!.Play.LastRoll!.Damage.Count);}
            Assert.AreEqual(0,session.Character!.Play.LastCast!.AttacksRemaining);if(combat)Assert.IsFalse(session.Character.Combat.ActionAvailable);
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
