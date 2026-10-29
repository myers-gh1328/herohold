using System.Text.Json;
using Herohold.Core;
using Herohold.TestSupport;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Integration.Tests;

/// <summary>A saved character's build read back typed is the build its export file carries.</summary>
[TestCategory("RulesData")]
[TestClass]
public sealed class BuildOfTests
{
    private static string Text(BuildFacts build) => JsonSerializer.Serialize(build);

    private static IEnumerable<CharacterState> Characters()
    {
        var sample = SampleWorkspace.Create().Character;
        yield return sample;
        yield return SheetRules.SetHeroicInspiration(SheetRules.ChangeCoins(SheetRules.UpdateProfile(sample, new() { Appearance = "Scarred" }, 7777), CoinChangeKind.Set, new() { Cp = 1, Sp = 2, Ep = 3, Gp = 4, Pp = 5 }), true);
        yield return sample with { Defenses = [new() { Kind = "resistance", Type = "Fire", Source = "Potion" }], Build = sample.Build! with { ManualFacts = new() { DamageResistances = ["Cold", "Fire"] } } };
        yield return TestStores.Calculated(NativeCharacterBuilderTests.Wizard());
        yield return TestStores.Calculated(MulticlassCompositionTests.Combine(MulticlassCompositionTests.Complete("Wizard", 17), MulticlassCompositionTests.Complete("Warlock", 3)));
        yield return TestStores.Calculated(NativeCharacterBuilderTests.Fighter());
        yield return sample with { Build = null };
    }

    [TestMethod]
    public void TheTypedBuildOfASavedCharacterIsTheBuildItsExportCarries()
    {
        var compared = 0;
        foreach (var c in Characters())
        {
            Assert.AreEqual(Text(BuildFacts.FromCanonical(WorkspaceProjection.ToCanonical(c))), Text(WorkspaceProjection.BuildOf(c)), c.Name);
            compared++;
        }
        Assert.AreEqual(7, compared);
    }
}
