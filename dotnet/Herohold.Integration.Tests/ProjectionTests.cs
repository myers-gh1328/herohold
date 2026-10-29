using Herohold.Rules;
using Herohold.TestSupport;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;
[TestClass] public sealed class ProjectionTests
{
 [TestMethod] public void CanonicalExportCarriesTheBuildAndPlayInventory()
 {
  var c=SampleWorkspace.Create().Character with{Build=SampleWorkspace.Create().Character.Build! with{Background="Sage"}};
  var canonical=WorkspaceProjection.ToCanonical(c);
  Assert.AreEqual("Sage",canonical["background"]!.GetValue<string>());
  Assert.AreEqual(c.CurrentHp,canonical["hit_points"]!["current"]!.GetValue<int>());
  Assert.IsNotNull(canonical["folio_inventory"]);
  Assert.IsNull(c.Build!.ToCanonical(c.Id)["folio_inventory"],"Inventory is a play fact, not part of the build.");
 }
 [TestCategory("RulesData")][TestMethod] public void ProjectionRetainsPortablePlayAndCreatesDistinctLibraryIdentity()
 {
  var original=SampleWorkspace.Create().Character;
  var canonical=WorkspaceProjection.ToCanonical(original);
  var projected=TestStores.Restored(canonical);
  Assert.AreEqual(original.Equipment.Count,projected.Equipment.Count);
  Assert.AreEqual(original.CurrentHp,projected.CurrentHp);
  Assert.AreEqual(original.Notes[0].Text,projected.Notes[0].Text);
  var adopted=WorkspaceProjection.WithNewIdentity(projected);
  Assert.AreNotEqual(projected.Id,adopted.Id);
  Assert.AreEqual(adopted.Id,WorkspaceProjection.ToCanonical(adopted)["character_id"]!.GetValue<string>());
 }
 [TestCategory("RulesData")][TestMethod] public void BuildProjectionRefreshesDerivedDefensesAndKeepsManualSources()
 {
  var original=SampleWorkspace.Create().Character with{Defenses=[new(){Kind="resistance",Type="Cold",Source="Character build"},new(){Kind="resistance",Type="Fire",Source="Potion"}]};
  var calculation=TestStores.Calculation(original);
  var result=WorkspaceProjection.FromCalculation(calculation with{Facts=new Dictionary<string,IReadOnlyList<string>>(calculation.Facts){["damage_resistances"]=["Lightning"]}},original);
  Assert.IsFalse(result.Defenses.Any(d=>d.Type=="Cold"));
  Assert.IsTrue(result.Defenses.Any(d=>d.Type=="Lightning"&&d.Source=="Character build"));
  Assert.IsTrue(result.Defenses.Any(d=>d.Type=="Fire"&&d.Source=="Potion"));
 }

 [TestCategory("RulesData")][TestMethod] public async Task ALargeSpellCatalogIsRecalculatedNotStored()
 {
  var spells=Enumerable.Range(0,400).Select(i=>new SpellDefinition{Id="reference-spell-"+i,Name="Reference Spell "+i,Level=1,Description=new string('x',3000)}).ToList();
  var c=WorkspaceProjection.FromCalculation(TestStores.Calculation(SampleWorkspace.Create().Character) with{Spells=spells});var directory=Path.Combine(Path.GetTempPath(),"herohold-large-caster-"+Guid.NewGuid().ToString("N"));
  try
  {
   Assert.IsTrue(c.Play.Spells.Count>=400);
   var record=CharacterRecords.FromState(c,0);
   Assert.IsFalse(System.Text.Json.JsonSerializer.Serialize(record).Contains("Reference Spell",StringComparison.Ordinal),"Spell definitions are rules output and are not stored.");
   var saved=await TestStores.Open(directory).SaveAsync(new(){Character=c},0);
   Assert.IsTrue(new FileInfo(Path.Combine(directory,"herohold.db")).Length<2*1024*1024);
   var second=await TestStores.Open(directory).SaveAsync(saved,1);
   Assert.AreEqual(2L,second.Revision);
  }
  finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
 }

 [TestCategory("RulesData")][TestMethod] public void SpellGrantSourcesKeepDistinctAbilitiesAndSpentFreeUses()
 {
  var original=SampleWorkspace.Create().Character;
  var grants=new List<SpellGrantMetadata>{
   new(){Id="grant-wis",SpellName="Cure Wounds",Ability=Ability.Wisdom,ResourceId="grant-pool-wis",MaxUses=1,Recovery="long_rest",AllowsSlots=true,BaseLevel=1,Source="Wisdom feature"},
   new(){Id="grant-int",SpellName="Cure Wounds",Ability=Ability.Intelligence,ResourceId="grant-pool-int",MaxUses=1,Recovery="long_rest",AllowsSlots=true,BaseLevel=1,Source="Intelligence feature"}};
  var prior=original with{Resources=[..original.Resources,new(){Id="grant-pool-wis",Name="Spent grant",Maximum=1,Remaining=0,SourceKind="grant"}]};
  var projected=WorkspaceProjection.FromCalculation(TestStores.Calculation(original) with{Grants=grants,GrantSources=[]},prior);
  Assert.AreEqual(4,SpellRules.Preview(projected,"grant-wis",1).Modifier);
  Assert.AreEqual(0,SpellRules.Preview(projected,"grant-int",1).Modifier);
  Assert.AreEqual(0,projected.Resources.Single(r=>r.Id=="grant-pool-wis").Remaining);
  Assert.AreEqual(1,projected.Resources.Single(r=>r.Id=="grant-pool-int").Remaining);
 }
 [TestCategory("RulesData")][TestMethod] public void ClassPreparationPolicyExcludesOutOfClassGrantedSpells()
 {
  var calculation=TestStores.Calculation(SampleWorkspace.Create().Character) with{Spells=[new(){Id="shield",Name="Shield",Level=1}],
   Preparation=new(){CanChangeOnLongRest=true,EligibleSpells=["Cure Wounds","Fog Cloud"],ReplacementLimit=1,PreparedLimit=6}};
  var projected=WorkspaceProjection.FromCalculation(calculation);
  Assert.IsFalse(projected.Play.Preparation.EligibleSpellIds.Contains("shield"));
  Assert.IsTrue(projected.Play.Preparation.EligibleSpellIds.Contains("cure-wounds"));
 }
 [TestCategory("RulesData")][TestMethod] public void ResourceMetadataPreservesSpentCapacityAndManualPoolsButRemovesOldBuildPools()
 {
  var original=SampleWorkspace.Create().Character with{Resources=[new(){Id="slot2",Name="Slots",Maximum=2,Remaining=1,SourceKind="build"},new(){Id="old-feature",Name="Old feature",Maximum=2,Remaining=0,SourceKind="build"},new(){Id="wand-charges",Name="Wand charges",Maximum=3,Remaining=1,SourceKind="manual"}]};
  var projected=WorkspaceProjection.FromCalculation(TestStores.Calculation(original) with{Resources=[new("slot2","Slots",3,"long_rest",2)],Grants=[]},original);
  Assert.AreEqual(2,projected.Resources.Single(r=>r.Id=="slot2").Remaining);
  Assert.AreEqual(1,projected.Resources.Single(r=>r.Id=="wand-charges").Remaining);
  Assert.IsFalse(projected.Resources.Any(r=>r.Id=="old-feature"));
 }

 [TestCategory("RulesData")][TestMethod] public void WizardAutomaticBookGrantsRemainAvailableWithoutDuplicatingOrdinaryAdditions()
 {
  var calculation=TestStores.Calculation(SampleWorkspace.Create().Character);
  calculation=calculation with{Build=calculation.Build with{ClassName="Wizard",Spells=new(){Spellbook=["Shield"]}},Spellbook=["Shield","Detect Magic"],Grants=[],GrantedSpellNames=[],
   Spells=[new(){Id="shield",Name="Shield",Level=1},new(){Id="detect-magic",Name="Detect Magic",Level=1,Ritual=true}]};
  var c=WorkspaceProjection.FromCalculation(calculation);
  Assert.IsTrue(c.Play.SpellbookIds.Contains("detect-magic"));Assert.IsTrue(c.Play.SpellbookIds.Contains("shield"));
  var exported=WorkspaceProjection.ToCanonical(c)["spells"]!["spellbook"]!.AsArray();
  Assert.AreEqual(1,exported.Count);Assert.AreEqual("Shield",exported[0]!.GetValue<string>());
 }

 [TestCategory("RulesData")][TestMethod] public void ExplicitManualResourceIsNotDiscardedBecauseItsIdResemblesABuildPool()
 {
  var original=SampleWorkspace.Create().Character with{Resources=[new(){Id="luck",Name="House rule luck",Maximum=7,Remaining=2,SourceKind="manual"}]};
  var calculation=TestStores.Calculation(original) with{Grants=[]};
  var c=WorkspaceProjection.FromCalculation(calculation with{Resources=[]},original);
  Assert.AreEqual(2,c.Resources.Single(r=>r.Id=="luck").Remaining);
  Assert.AreEqual("manual",c.Resources.Single(r=>r.Id=="luck").SourceKind);
  var overridden=WorkspaceProjection.FromCalculation(calculation with{Resources=[new("luck","Class luck",3,"long_rest")]},original);
  Assert.AreEqual(7,overridden.Resources.Single(r=>r.Id=="luck").Maximum);
 }

}
