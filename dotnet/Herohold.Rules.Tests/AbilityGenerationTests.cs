using System.Text.Json.Nodes;
using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass]
public sealed class AbilityGenerationTests
{
    [TestMethod] public void StandardArrayHasTheSixReviewedScoresAndReadonlyIndependentReads()
    {
        CollectionAssert.AreEqual(new[]{15,14,13,12,10,8},AbilityGenerationRules.StandardArray.ToArray());
        if(AbilityGenerationRules.StandardArray is IList<int> mutable)Assert.ThrowsExactly<NotSupportedException>(()=>mutable[0]=99);
    }
    [TestMethod] public void ExplicitRollDropsExactlyOneLowestOfEachFourThenSortsSixScores()
    {
        var dice=new Queue<int>(new[]{1,2,3,4,6,6,6,1,1,1,1,1,2,2,2,2,6,5,4,3,6,6,1,1});var calls=0;
        var scores=AbilityGenerationRules.Roll(()=>{calls++;return dice.Dequeue();});
        CollectionAssert.AreEqual(new[]{18,15,13,9,6,3},scores.ToArray());Assert.AreEqual(24,calls);Assert.AreEqual(0,dice.Count);
        foreach(var invalid in new[]{0,7,-1})Assert.ThrowsExactly<ArgumentException>(()=>AbilityGenerationRules.Roll(()=>invalid));
    }
    [TestMethod] public void AssignmentUsesSixDistinctPositionsEvenWhenAllValuesAreEqual()
    {
        var positions=Enum.GetValues<Ability>().Select((a,i)=>(a,i)).ToDictionary(p=>p.a,p=>p.i);
        var equal=AbilityGenerationRules.ResolveAssignments(new[]{12,12,12,12,12,12},positions);Assert.AreEqual(6,equal.Count);Assert.IsTrue(equal.Values.All(v=>v==12));
        positions[Ability.Charisma]=0;Assert.ThrowsExactly<ArgumentException>(()=>AbilityGenerationRules.ResolveAssignments(new[]{15,14,13,12,10,8},positions));
        positions.Remove(Ability.Charisma);Assert.ThrowsExactly<ArgumentException>(()=>AbilityGenerationRules.ResolveAssignments(new[]{15,14,13,12,10,8},positions));
        Assert.ThrowsExactly<ArgumentException>(()=>AbilityGenerationRules.ResolveAssignments(new[]{19,14,13,12,10,8},Enum.GetValues<Ability>().Select((a,i)=>(a,i)).ToDictionary(p=>p.a,p=>p.i)));
    }
    private static BuildFacts NewBuild()=>BuildFacts.FromCanonical(CanonicalCharacterRules.Create());
    [TestMethod] public void GenerationIsExplicitDetachedAndSwapNeverRollsOrChangesThePool()
    {
        var source=NewBuild() with{Name="Fictional scout"};var before=System.Text.Json.JsonSerializer.Serialize(source);var rolls=0;
        var generated=AbilityGenerationRules.Generate(source,AbilityGenerationMethod.Roll4d6,()=>{rolls++;return 4;});Assert.AreEqual(24,rolls);Assert.AreEqual("Roll 4d6",generated.AbilityScoreMethod);
        CollectionAssert.AreEqual(new[]{12,12,12,12,12,12},generated.RolledAbilityScores!.ToArray());
        var swapped=AbilityGenerationRules.Swap(generated,Ability.Strength,5);Assert.AreEqual(24,rolls);Assert.AreEqual(5,swapped.AbilityScoreAssignments!["Strength"]);Assert.AreEqual(0,swapped.AbilityScoreAssignments!["Charisma"]);
        Assert.AreEqual(0,generated.AbilityScoreAssignments!["Strength"]);Assert.AreEqual(before,System.Text.Json.JsonSerializer.Serialize(source));Assert.AreEqual("Fictional scout",swapped.Name);
        Assert.ThrowsExactly<ArgumentException>(()=>AbilityGenerationRules.Swap(source,Ability.Strength,1));
    }
    [TestMethod] public void StandardResetAppliesBaseScoresWithoutTouchingTheRestOfTheBuild()
    {
        var source=NewBuild() with{ImportProvenance=new(){SourceKind="fictional"},RolledAbilityScores=[18,18,18,18,18,18]};
        var standard=AbilityGenerationRules.Generate(source,AbilityGenerationMethod.StandardArray,()=>throw new AssertFailedException("Standard array must not roll."));
        Assert.AreEqual("Standard array",standard.AbilityScoreMethod);Assert.AreEqual(15,standard.BaseAbilityScores![Ability.Strength]);Assert.AreEqual(8,standard.BaseAbilityScores![Ability.Charisma]);
        var swapped=AbilityGenerationRules.Swap(standard,Ability.Charisma,0);Assert.AreEqual(15,swapped.BaseAbilityScores![Ability.Charisma]);Assert.AreEqual(8,swapped.BaseAbilityScores![Ability.Strength]);
        Assert.AreEqual("fictional",swapped.ImportProvenance!.SourceKind);
        Assert.ThrowsExactly<ArgumentException>(()=>AbilityGenerationRules.Generate(source,(AbilityGenerationMethod)99));Assert.ThrowsExactly<ArgumentException>(()=>AbilityGenerationRules.Swap(standard,Ability.Strength,6));
    }
    [TestMethod] public void GeneratedMethodCannotBypassPoolOrPermutationByPatchingManualScores()
    {
        var source=NewBuild();Assert.AreEqual(0,AbilityGenerationRules.Validate(source).Count);
        source=source with{AbilityScoreMethod="Roll 4d6"};Assert.IsTrue(AbilityGenerationRules.Validate(source).Any(i=>i.Code=="missing_ability_roll"));
        var generated=AbilityGenerationRules.Generate(source,AbilityGenerationMethod.StandardArray);Assert.AreEqual(0,AbilityGenerationRules.Validate(generated).Count);
        generated=generated with{BaseAbilityScores=new(generated.BaseAbilityScores!){[Ability.Strength]=30}};Assert.IsTrue(AbilityGenerationRules.Validate(generated).Any(i=>i.Code=="ability_assignment_mismatch"));
        generated=generated with{AbilityScoreAssignments=new(generated.AbilityScoreAssignments!){["Charisma"]=0}};Assert.IsTrue(AbilityGenerationRules.Validate(generated).Any(i=>i.Code=="invalid_ability_assignments"));
        source=source with{AbilityScoreMethod="Invented method"};Assert.IsTrue(AbilityGenerationRules.Validate(source).Any(i=>i.Code=="invalid_ability_method"));
    }
    [TestMethod] public void CreationValidationIncludesGeneratedPoolFailuresAndManualImportsKeepWorking()
    {
        const string cls="""[{"name":"Fighter","hit_die":10,"progression":{"1":{"features":[]}}}]""";
        var catalog=RulesCatalogSnapshot.Parse(cls,"[]","[]","[]","[]","[]","[]","{}");var classes=ClassProgressionCatalog.Parse(cls);
        var creation=new CreationChoiceRules(catalog,classes,new(classes,catalog,BeastChoiceCatalog.Parse("[]"),SpellCapabilityCatalog.Parse("{\"spell\":[]}")),new());
        var source=CanonicalCharacterRules.Create();source["ability_score_method"]="Roll 4d6";
        Assert.IsTrue(creation.Describe(CanonicalFixtures.Creation(source)).Issues.Any(i=>i.Code=="missing_ability_roll"));
        source["ability_score_method"]="Manual entry";Assert.IsFalse(creation.Describe(CanonicalFixtures.Creation(source)).Issues.Any(i=>i.Code=="missing_ability_roll"));
    }
}
