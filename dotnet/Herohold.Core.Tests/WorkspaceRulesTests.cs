using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Core.Tests;

[TestClass]
public sealed class WorkspaceRulesTests
{
    [TestMethod]
    public void InventoryChangesAreDetachedAndBonusesFollowEquipmentActivation()
    {
        var original = Fixtures.Ranger();
        var updated = WorkspaceRules.SetEquipment(original, "charm", false, false);
        Assert.AreEqual(4, CharacterCalculations.Ability(original, Ability.Wisdom).Total);
        Assert.AreEqual(3, CharacterCalculations.Ability(updated, Ability.Wisdom).Total);
        updated = WorkspaceRules.SetEquipment(updated, "charm", true, true);
        Assert.AreEqual(4, CharacterCalculations.Ability(updated, Ability.Wisdom).Total);
        updated = WorkspaceRules.AdjustQuantity(updated, "potion", -1);
        Assert.AreEqual(2, updated.Equipment.Single(x => x.Id == "potion").Quantity);
        Assert.AreEqual(3, original.Equipment.Single(x => x.Id == "potion").Quantity);
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkspaceRules.AdjustQuantity(updated, "potion", -3));
        Assert.Throws<ArgumentException>(() => WorkspaceRules.AdjustQuantity(updated, "missing", -1));
        updated.AbilityScores[Ability.Wisdom] = 1;
        updated.Equipment[0].ModifierBonuses[Ability.Wisdom] = 8;
        Assert.AreEqual(16, original.AbilityScores[Ability.Wisdom]);
        Assert.AreEqual(1, original.Equipment[0].ModifierBonuses[Ability.Wisdom]);
        Assert.AreEqual(32, updated.CurrentHp);
    }

    [TestMethod]
    public void ResourceAndHpActionsAreBoundedAndNeverTouchOtherPools()
    {
        var original = Fixtures.Ranger() with { TemporaryHp = 7 };
        var spent = WorkspaceRules.AdjustResource(original, "luck", -1);
        Assert.AreEqual(2, spent.Resources[0].Remaining);
        Assert.AreEqual(3, original.Resources[0].Remaining);
        var hurt = WorkspaceRules.AdjustHp(spent, -5);
        Assert.AreEqual(27, hurt.CurrentHp);
        Assert.AreEqual(7, hurt.TemporaryHp);
        Assert.AreEqual(2, hurt.Resources[0].Remaining);
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkspaceRules.AdjustHp(original, 13));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkspaceRules.AdjustHp(original, -33));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkspaceRules.AdjustResource(original, "luck", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkspaceRules.AdjustResource(original, "luck", -4));
        Assert.Throws<ArgumentException>(() => WorkspaceRules.AdjustResource(original, "missing", -1));
    }

    [TestMethod]
    public void JournalAndExplicitEffectCountdownPreserveResourcesAndReplaceConcentration()
    {
        var original = Fixtures.Ranger();
        var noted = WorkspaceRules.AddNote(original, "Bridge", "<script>this stays plain text</script>");
        Assert.AreEqual(0, original.Notes.Count);
        Assert.AreEqual(1, noted.Notes.Count);
        Assert.AreEqual("<script>this stays plain text</script>", noted.Notes[0].Text);
        Assert.IsFalse(string.IsNullOrWhiteSpace(noted.Notes[0].Id));
        var marked = WorkspaceRules.AddEffect(noted, new() { Id = "mark", Name = "Hunter’s Mark", Concentration = true });
        var timed = WorkspaceRules.AddEffect(marked, new() { Id = "poison", Name = "Poisoned", RemainingRounds = 2 });
        var fog = WorkspaceRules.AddEffect(timed, new() { Id = "fog", Name = "Fog Cloud", Concentration = true });
        Assert.AreEqual(2, fog.Effects.Count);
        Assert.IsFalse(fog.Effects.Any(x => x.Id == "mark"));
        var next = WorkspaceRules.AdvanceTurn(fog);
        Assert.AreEqual(2, next.Turn);
        Assert.AreEqual(1, next.Effects.Single(x => x.Id == "poison").RemainingRounds);
        next = WorkspaceRules.AdvanceTurn(next);
        Assert.AreEqual(1, next.Effects.Count);
        Assert.AreEqual("fog", next.Effects[0].Id);
        Assert.IsNull(next.Effects[0].RemainingRounds);
        Assert.AreEqual(1, next.Notes.Count);
        Assert.AreEqual(3, next.Resources[0].Remaining);
        Assert.AreEqual(32, next.CurrentHp);
        next = WorkspaceRules.RemoveEffect(next, "fog");
        Assert.AreEqual(0, next.Effects.Count);
        Assert.Throws<ArgumentException>(() => WorkspaceRules.AddEffect(original, new() { Name = "" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkspaceRules.AddEffect(original, new() { Id = "bad", Name = "Bad", RemainingRounds = 0 }));
        Assert.Throws<ArgumentException>(() => WorkspaceRules.AddNote(original, "", ""));
    }

    [TestMethod]
    public void InventoryEditingAndTargetKnowledgeStayDetachedAndValidated()
    {
        var original = Fixtures.Ranger();
        var item = new EquipmentItem { Id = "cloak", Name = "Travel cloak", Quantity = 1 };
        var added = WorkspaceRules.AddEquipment(original, item);
        Assert.AreEqual(3, added.Equipment.Count);
        Assert.AreEqual(2, original.Equipment.Count);
        Assert.Throws<ArgumentException>(() => WorkspaceRules.AddEquipment(added, item));
        var updated = WorkspaceRules.UpdateEquipment(added, item with { Quantity = 2 });
        Assert.AreEqual(2, updated.Equipment.Single(x => x.Id == "cloak").Quantity);
        updated = WorkspaceRules.RemoveEquipment(updated, "cloak");
        Assert.AreEqual(2, updated.Equipment.Count);
        var defense = new DamageDefense { Kind = "resistance", Type = "Fire", Source = "Potion" };
        updated = WorkspaceRules.SetDefenses(updated, [defense]);
        Assert.AreEqual(0, original.Defenses.Count);
        Assert.AreEqual("unknown", updated.KnownTarget.Knowledge);
        updated = WorkspaceRules.SetKnownTarget(updated, new() { Name = "Sentinel", Knowledge = "recorded", Defenses = [new() { Kind = "immunity", Type = "Poison" }] });
        Assert.AreEqual("Fire", updated.Defenses[0].Type);
        Assert.AreEqual("Poison", updated.KnownTarget.Defenses[0].Type);
        Assert.Throws<ArgumentException>(() => WorkspaceRules.SetDefenses(updated, [defense, defense]));
        Assert.Throws<ArgumentException>(() => WorkspaceRules.SetKnownTarget(updated, new() { Knowledge = "made-up" }));
    }

    [TestMethod]
    public void IncapacitatingConditionsEndConcentrationAndMarkedTarget()
    {
        foreach (var name in new[] { "Incapacitated", "Paralyzed", "Stunned", "Petrified", "Unconscious" })
        {
            var c = Fixtures.Ranger() with { Effects = [new() { Id = "mark", Name = "Hunter’s Mark", Source = "spell:hunters-mark", Concentration = true }], Play = new() { HunterTarget = "Sentinel" } };
            var changed = WorkspaceRules.AddEffect(c, new() { Id = "condition", Name = name });
            Assert.IsFalse(changed.Effects.Any(x => x.Concentration), name);
            Assert.AreEqual("", changed.Play.HunterTarget, name);
            Assert.IsTrue(c.Effects.Any(x => x.Concentration));
            Assert.Throws<ArgumentException>(() => WorkspaceRules.AddEffect(changed, new() { Id = "new-mark", Name = "Hunter’s Mark", Source = "spell:hunters-mark", Concentration = true }));
        }
    }

    [TestMethod]
    public void RemovingOrExpiringTheMarkClearsItsTrackedTarget()
    {
        var c = Fixtures.Ranger() with { Effects = [new() { Id = "mark", Name = "Hunter’s Mark", Source = "spell:hunters-mark", Concentration = true, RemainingRounds = 1 }], Play = new() { HunterTarget = "Sentinel" } };
        Assert.AreEqual("", WorkspaceRules.RemoveEffect(c, "mark").Play.HunterTarget);
        var next = WorkspaceRules.AdvanceTurn(c);
        Assert.AreEqual(0, next.Effects.Count);
        Assert.AreEqual("", next.Play.HunterTarget);
    }

    [TestMethod]
    public void ResourceOwnershipIsExplicitAndRejectsUnsupportedSources()
    {
        var c = Fixtures.Ranger();
        Assert.AreEqual("manual", c.Resources[0].SourceKind);
        foreach (var kind in new[] { "manual", "build", "grant" })
            WorkspaceRules.Validate(c with { Resources = [c.Resources[0] with { SourceKind = kind }] });
        Assert.Throws<ArgumentException>(() => WorkspaceRules.Validate(c with { Resources = [c.Resources[0] with { SourceKind = "unknown-source" }] }));
    }
}
