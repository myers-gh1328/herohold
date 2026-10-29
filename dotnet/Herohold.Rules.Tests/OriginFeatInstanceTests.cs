using Herohold.Core;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass]public sealed class OriginFeatInstanceTests
{
    private static JsonObject Background()=>new(){["name"]="Scribe",["origin_feats"]=new JsonArray("Skilled")};
    private static JsonObject Human()=>new(){["name"]="Human",["traits"]=new JsonArray("Versatile")};
    [TestMethod]public void ExplicitSkilledVirtualSelectionReplacesOlderLegacyChoiceFields()
    {
        var raw=JsonNode.Parse("""{"general_feats":[{"instance_id":"fictional-skilled","name":"Skilled","choices":{"skills":["History"],"proficiencies":["Skill: Stealth","Tool: Dice Set","Tool: Flute"]}}]}""")!.AsObject();
        var instance=CanonicalFixtures.OriginFeats(raw,null,null).Single();CollectionAssert.AreEqual(new[]{"Stealth"},ChoiceReading.Names(instance.Choices["skills"]).ToArray());
    }
    [TestMethod]public void RepeatedBackgroundHumanAndGeneralSkilledKeepIndependentOwnersAndChoices()
    {
        var raw=JsonNode.Parse("""{"species_options":{"origin_feats":["Skilled"]},"origin_feat_choices":{"skills":["Arcana","History","Religion"]},"folio_origin_feat_choices":{"Skilled":{"skills":["Insight","Perception","Survival"]}},"general_feats":[{"instance_id":"fictional-skilled","name":"Skilled","choices":{"proficiencies":["Skill: Stealth","Tool: Dice Set","Tool: Flute"]}}]}""")!.AsObject();var before=raw.ToJsonString();
        var instances=CanonicalFixtures.OriginFeats(raw,Background(),Human());Assert.AreEqual(3,instances.Count);Assert.AreEqual(3,instances.Select(instance=>instance.SourceId).Distinct().Count());Assert.AreEqual(3,instances.Select(instance=>instance.ChoiceId).Distinct().Count());
        CollectionAssert.AreEqual(new[]{"origin_feat_choices"},instances[0].ChoicePath.ToArray());CollectionAssert.AreEqual(new[]{"folio_origin_feat_choices","Skilled"},instances[1].ChoicePath.ToArray());Assert.AreEqual("feat:fictional-skilled",instances[2].SourceId);Assert.AreEqual("Stealth",instances[2].Choices["skills"].Items![0].Text);Assert.AreEqual(2,instances[2].Choices["tools"].Items!.Count);Assert.AreEqual(before,raw.ToJsonString());
        instances[1].Choices["skills"].Items!.Clear();Assert.AreEqual(before,raw.ToJsonString());
    }
    [TestMethod]public void GenericOriginOwnersSurviveSourceChangesAndBackgroundSelectionsDoNotDuplicateFixedGrants()
    {
        var raw=JsonNode.Parse("""{"background_options":{"origin_feats":["Skilled"]},"species_options":{"origin_feats":["Skilled"]}}""")!.AsObject();
        var first=CanonicalFixtures.OriginFeats(raw,Background(),Human());Assert.AreEqual(2,first.Count);var humanId=first[1].SourceId;
        var withoutBackground=CanonicalFixtures.OriginFeats(raw,new(){["name"]="Soldier",["origin_feats"]=new JsonArray("Savage Attacker")},Human());Assert.AreEqual(humanId,withoutBackground.Single(instance=>instance.Name=="Skilled").SourceId);Assert.AreEqual(first[1].ChoiceId,withoutBackground.Single(instance=>instance.Name=="Skilled").ChoiceId);
    }
}
