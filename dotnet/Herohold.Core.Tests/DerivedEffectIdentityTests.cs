using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Core.Tests;

[TestClass]
public sealed class DerivedEffectIdentityTests
{
    [TestMethod]
    public void DerivedEffectsKeepDistinctIdentitiesForLongItemsAndImportedManualIds()
    {
        var prefix=new string('a',40);
        var character=new CharacterState
        {
            Id="source-identity-test",MaximumHp=10,CurrentHp=0,
            Effects=[new(){Id="zero-hp",Name="Frightened",Source="Imported manual source"}],
            Equipment=[
                new(){Id=prefix+"-first",Name="First ward",Equipped=true,PassiveEffects=[new(){Name="Poisoned"}]},
                new(){Id=prefix+"-second",Name="Second ward",Equipped=true,PassiveEffects=[new(){Name="Poisoned"}]}
            ]
        };
        var effects=SheetRules.EffectiveEffects(character);
        Assert.AreEqual(4,effects.Count);
        Assert.AreEqual(4,effects.Select(e=>e.Id).Distinct(StringComparer.Ordinal).Count(),"Each manual/item/health source must retain its own identity.");
        Assert.AreEqual("Imported manual source",effects.Single(e=>e.Id=="zero-hp").Source);
        CollectionAssert.AreEquivalent(effects.Select(e=>e.Id).ToArray(),SheetRules.EffectiveEffects(character).Select(e=>e.Id).ToArray());
        var unequipped=character with{Equipment=[character.Equipment[0] with{Equipped=false},character.Equipment[1]]};
        var surviving=SheetRules.EffectiveEffects(unequipped);
        Assert.AreEqual(3,surviving.Count);
        Assert.IsTrue(surviving.Any(e=>e.Source.Contains("Second ward",StringComparison.Ordinal)));
        Assert.IsTrue(surviving.Any(e=>e.Id=="zero-hp"));
    }
}
