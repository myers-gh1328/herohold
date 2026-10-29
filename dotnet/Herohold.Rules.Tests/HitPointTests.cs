using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;

[TestClass]
public sealed class HitPointTests
{
    [TestMethod]
    public void FixedHistoryRecalculatesConstitutionRetroactivelyWithoutHealing()
    {
        var input = new HitPointInput { ClassName = "Ranger", Level = 3, HitDie = 10, ConstitutionModifier = 2, CurrentHp = 12, TemporaryHp = 7 };
        var result = HitPointRules.Calculate(input);
        Assert.AreEqual(28, result.Maximum);
        Assert.AreEqual(12, result.CurrentHp);
        Assert.AreEqual(7, result.TemporaryHp);
        Assert.AreEqual(3, result.History.Count);
        Assert.AreEqual(0, input.History.Count);
        var increased = HitPointRules.Calculate(input with { ConstitutionModifier = 3 });
        Assert.AreEqual(31, increased.Maximum);
        Assert.AreEqual(12, increased.CurrentHp);
        Assert.AreEqual(31, increased.Contributions.Sum(x => x.Total));
    }

    [TestMethod]
    public void RolledHistoryAppliesPerLevelMinimumAndRetroactiveBonusesOnce()
    {
        var history = new[] { new HitPointHistoryEntry(1, "Wizard", HitPointMethod.Maximum, 6), new HitPointHistoryEntry(2, "Wizard", HitPointMethod.Rolled, 1) };
        var input = new HitPointInput { ClassName = "Wizard", Level = 2, HitDie = 6, ConstitutionModifier = -5, History = history, CurrentHp = 20, TemporaryHp = 7 };
        Assert.AreEqual(2, HitPointRules.Calculate(input).Maximum);
        var changed = HitPointRules.Calculate(input with { Tough = true, DwarvenToughness = true, AdditionalMaximumBonus = 4, ImportedMaximumAdjustment = -3 });
        Assert.AreEqual(12, changed.CalculatedBeforeImportAdjustment);
        Assert.AreEqual(9, changed.Maximum);
        Assert.AreEqual(9, changed.CurrentHp);
        Assert.AreEqual(7, changed.TemporaryHp);
        Assert.AreEqual(1, changed.History[1].BaseGain);
        Assert.AreEqual(HitPointMethod.Rolled, changed.History[1].Method);
        Assert.AreEqual(6, changed.Contributions.Sum(x => x.ToughBonus + x.SpeciesBonus));
        Assert.AreEqual(2, history.Length);
    }

    [TestMethod]
    public void InvalidOrForeignHistoryRejectsWithoutFillingOrMutatingTheInput()
    {
        var input = new HitPointInput { ClassName = "Ranger", Level = 3, HitDie = 10, History = [new(3, "Ranger", HitPointMethod.Rolled, 9)] };
        var result = HitPointRules.Calculate(input);
        Assert.AreEqual(25, result.Maximum);
        Assert.AreEqual(1, input.History.Count);
        Assert.Throws<ArgumentException>(() => HitPointRules.Calculate(input with { History = [new(1, "Wizard", HitPointMethod.Maximum, 10)] }));
        Assert.Throws<ArgumentException>(() => HitPointRules.Calculate(input with { History = [new(1, "Ranger", HitPointMethod.Maximum, 10), new(1, "Ranger", HitPointMethod.Maximum, 10)] }));
        Assert.Throws<ArgumentException>(() => HitPointRules.Calculate(input with { History = [new(2, "Ranger", HitPointMethod.Rolled, 11)] }));
        Assert.Throws<ArgumentException>(() => HitPointRules.Calculate(input with { History = [new(2, "Ranger", HitPointMethod.Fixed, 9)] }));
    }

    [TestMethod]
    public void CanonicalAdapterRetainsRolledHistoryAndImportedAdjustmentWithoutMutation()
    {
        var canonical = System.Text.Json.Nodes.JsonNode.Parse("""
            {"class":"Ranger","level":3,"hp_history":[{"character_level":2,"class_name":"Ranger","method":"rolled","base_gain":9}],"hit_points":{"current":12,"temporary":5},"import_overrides":{"maximum_hp_adjustment":7},"unknown_custom":"Retain"}
            """)!.AsObject();
        var before = canonical.ToJsonString();
        var input = CanonicalFixtures.HitPointsOfBuild(canonical, 10, 2, tough:true);
        var result = HitPointRules.Calculate(input);
        Assert.AreEqual(44, result.Maximum);
        Assert.AreEqual(12, result.CurrentHp);
        Assert.AreEqual(5, result.TemporaryHp);
        Assert.AreEqual(7, result.ImportAdjustment);
        Assert.AreEqual(HitPointMethod.Rolled, result.History[1].Method);
        Assert.AreEqual(9, result.History[1].BaseGain);
        Assert.AreEqual(before, canonical.ToJsonString());
    }

    [TestMethod]
    public void CanonicalAdapterRejectsMulticlassAndMalformedHpBlocksInsteadOfReinterpretingThem()
    {
        var multi = System.Text.Json.Nodes.JsonNode.Parse("""
            {"class":"Fighter","level":2,"class_levels":{"Fighter":1,"Wizard":1},"hp_history":[]}
            """)!.AsObject();
        Assert.Throws<ArgumentException>(() => CanonicalFixtures.HitPointsOfBuild(multi, 10, 2));
        var malformed = System.Text.Json.Nodes.JsonNode.Parse("""
            {"class":"Fighter","level":2,"hit_points":"not an HP object"}
            """)!.AsObject();
        Assert.Throws<ArgumentException>(() => CanonicalFixtures.HitPointsOfBuild(malformed, 10, 2));
    }

    [TestMethod]
    public void SingleClassFirstLevelRequiresMaximumHitDieHistory()
    {
        var input = new HitPointInput { ClassName = "Wizard", Level = 1, HitDie = 6 };
        Assert.Throws<ArgumentException>(() => HitPointRules.Calculate(input with { History = [new(1, "Wizard", HitPointMethod.Rolled, 3)] }));
        Assert.Throws<ArgumentException>(() => HitPointRules.Calculate(input with { History = [new(1, "Wizard", HitPointMethod.Fixed, 4)] }));
        Assert.AreEqual(6, HitPointRules.Calculate(input).Maximum);
    }
}
