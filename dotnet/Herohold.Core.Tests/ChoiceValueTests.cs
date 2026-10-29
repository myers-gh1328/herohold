using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Core.Tests;

[TestClass]
public sealed class ChoiceValueTests
{
    private static ChoiceValue Parse(string json) => JsonSerializer.Deserialize<ChoiceValue>(json)!;

    [TestMethod]
    public void SameComparesListsInOrderAndNamedPartsInAnyOrder()
    {
        Assert.IsTrue(ChoiceValue.Same(Parse("""{"mode":"asi","ability_increases":{"Strength":1,"Dexterity":1}}"""), Parse("""{"ability_increases":{"Dexterity":1,"Strength":1},"mode":"asi"}""")));
        Assert.IsFalse(ChoiceValue.Same(Parse("""["Stealth","Arcana"]"""), Parse("""["Arcana","Stealth"]""")));
        Assert.IsFalse(ChoiceValue.Same(ChoiceValue.Of("1"), ChoiceValue.Of(1)), "A name and a number are different choices.");
        Assert.IsTrue(ChoiceValue.Same(null, new ChoiceValue()));
    }

    [TestMethod]
    public void WithSetsANestedPartWithoutChangingTheOriginal()
    {
        var original = Parse("""{"mode":"feat","feat_choices":{"skill_proficiency":"Insight"}}""");
        var changed = original.With(["feat_choices", "expertise"], ChoiceValue.Of("Stealth"));
        Assert.AreEqual("Stealth", changed.At(["feat_choices", "expertise"])!.Text);
        Assert.AreEqual("Insight", changed.At(["feat_choices", "skill_proficiency"])!.Text);
        Assert.IsNull(original.At(["feat_choices", "expertise"]));
        Assert.AreEqual("feat", ChoiceValue.Of("x").With(["mode"], ChoiceValue.Of("feat")).At(["mode"])!.Text, "Setting a part of a plain value starts named parts.");
    }

    [TestMethod]
    public void DisplayReadsNamesListsAndNamedParts()
    {
        Assert.AreEqual("Stealth, Arcana", Parse("""["Stealth","Arcana"]""").Display);
        Assert.AreEqual("Mode: asi · Ability Increases: Strength: 2", Parse("""{"mode":"asi","ability_increases":{"Strength":2}}""").Display);
        Assert.AreEqual("Yes", ChoiceValue.Of(true).Display);
        Assert.AreEqual("", new ChoiceValue().Display);
    }
}
