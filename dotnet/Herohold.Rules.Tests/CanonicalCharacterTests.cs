using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass] public sealed class CanonicalCharacterTests
{
    [TestMethod] public void NewCharactersHaveIndependentIdentityAndDetachedDefaults()
    {
        var first=CanonicalCharacterRules.Create();var second=CanonicalCharacterRules.Create();Assert.AreNotEqual(first["character_id"]!.GetValue<string>(),second["character_id"]!.GetValue<string>());
        first["spells"]!["cantrips"]!.AsArray().Add("Light");Assert.AreEqual(0,second["spells"]!["cantrips"]!.AsArray().Count);Assert.AreEqual(5,first["schema_version"]!.GetValue<int>());
    }
    [TestMethod] public void LegacyWizardAndHpAliasesMigrateWithoutMutatingInput()
    {
        var original=JsonNode.Parse("""{"schema_version":1,"class":"Wizard","spells":{"known":["Shield","Magic Missile"]},"hit_points":{"max":12,"current":7},"extension":{"notes":"Keep","file_path":"secret","nested":{"level_up_draft":{},"path":"secret"}}}""")!.AsObject();var before=original.ToJsonString();
        var migrated=CanonicalCharacterRules.Migrate(original);Assert.AreEqual(12,migrated["hit_points"]!["maximum"]!.GetValue<int>());Assert.AreEqual(2,migrated["spells"]!["spellbook"]!.AsArray().Count);Assert.AreEqual(0,migrated["spells"]!["known"]!.AsArray().Count);
        Assert.AreEqual("Keep",migrated["extension"]!["notes"]!.GetValue<string>());Assert.IsFalse(migrated["extension"]!.AsObject().ContainsKey("file_path"));Assert.AreEqual(before,original.ToJsonString());
        Assert.IsTrue(JsonNode.DeepEquals(migrated,CanonicalCharacterRules.Migrate(migrated)));Assert.IsTrue(JsonNode.DeepEquals(migrated,CanonicalCharacterRules.Migrate(original)));
    }
    [TestMethod] public void FeatInstancesRemainStableAndLegacyEquipmentIsMarkedForReconciliation()
    {
        var original=JsonNode.Parse("""{"general_feats":["Actor",{"instance_id":"kept","name":"Elemental Adept","choices":{"damage_type":"Fire"}}],"equipment":["Rope","Rope","Shield"]}""")!.AsObject();
        var first=CanonicalCharacterRules.Migrate(original);var second=CanonicalCharacterRules.Migrate(original);
        Assert.IsTrue(JsonNode.DeepEquals(first,second));Assert.AreEqual("kept",first["general_feats"]![1]!["instance_id"]!.GetValue<string>());
        Assert.AreEqual(2,first["custom_equipment"]!.AsArray().Count);Assert.IsTrue(first["equipment_migration_pending"]!.GetValue<bool>());
    }
    [TestMethod] public void UnsupportedSchemaAndMalformedKnownStructuresFailClearly()
    {
        foreach(var json in new[]{"""{"schema_version":6}""","""{"schema_version":true}""","""{"spells":[]}""","""{"hit_points":"wrong"}"""})Assert.Throws<ArgumentException>(()=>CanonicalCharacterRules.Migrate(JsonNode.Parse(json)!.AsObject()));
    }
    [TestMethod] public void WarlockArcanumMigrationCreatesStableLevelSpecificRecords()
    {
        var source=JsonNode.Parse("""{"class":"Warlock","level":13,"class_options":{"mystic_arcanum":["Circle of Death","Finger of Death"]}}""")!.AsObject();var migrated=CanonicalCharacterRules.Migrate(source);
        Assert.AreEqual("Circle of Death",migrated["class_options"]!["mystic_arcanum_6"]![0]!.GetValue<string>());
        Assert.AreEqual("warlock_mystic_arcanum_7",migrated["mystic_arcanum"]![1]!["instance_id"]!.GetValue<string>());Assert.AreEqual(13,migrated["mystic_arcanum"]![1]!["source_level"]!.GetValue<int>());
        Assert.AreEqual("finger_of_death_xphb",migrated["mystic_arcanum"]![1]!["spell_id"]!.GetValue<string>());
        Assert.IsTrue(JsonNode.DeepEquals(migrated,CanonicalCharacterRules.Migrate(migrated)));
    }
    [TestMethod] public void ProvenanceDropsPathsAndMalformedDataWhileRetainingSafeScalarEvidence()
    {
        var source=JsonNode.Parse("""{"import_provenance":{"source_kind":"pdf","status":"reviewed","path":"private","arbitrary":"drop"},"import_overrides":{"maximum_hp_adjustment":true}}""")!.AsObject();var migrated=CanonicalCharacterRules.Migrate(source);
        Assert.AreEqual(0,migrated["import_overrides"]!["maximum_hp_adjustment"]!.GetValue<int>());Assert.AreEqual(0,migrated["import_provenance"]!["warnings"]!.AsArray().Count);Assert.IsFalse(migrated["import_provenance"]!.AsObject().ContainsKey("arbitrary"));
    }
    [TestMethod] public void PdfPassthroughKeepsSafeNarrativeFieldsAndExcludesOwnedNumbers()
    {
        var migrated=CanonicalCharacterRules.Migrate(JsonNode.Parse("""{"pdf_passthrough_fields":{"Age":"42","AC":"999","CharacterName":"Wrong","Unknown":"drop"}}""")!.AsObject());
        Assert.AreEqual("42",migrated["pdf_passthrough_fields"]!["Age"]!.GetValue<string>());Assert.AreEqual(1,migrated["pdf_passthrough_fields"]!.AsObject().Count);
    }
}
