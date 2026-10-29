using System.Text.Json.Nodes;
using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestCategory("RulesData")][TestClass]public sealed class CharacterBuildDraftTests
{
    [TestMethod]public void InvalidFeatAbilityStaysInEditableDraftButNeverBecomesAnActiveNumericGrant()
    {
        var raw=JsonNode.Parse("""{"class":"Fighter","level":4,"background":"Soldier","species":"Orc","base_ability_scores":{"Strength":10,"Dexterity":10,"Constitution":10,"Intelligence":10,"Wisdom":10,"Charisma":10},"background_options":{"primary_ability":"Strength","secondary_ability":"Dexterity"},"general_feats":[{"instance_id":"fictional-resilient","name":"Resilient","choices":{"ability":"Luck"}}]}""")!.AsObject();var before=raw.ToJsonString();
        var catalog=CharacterBuildCatalog.Parse(ClassProgressionCatalog.Parse(SpellGrantFixture.Read("classes")),SpellGrantFixture.Read("backgrounds"),SpellGrantFixture.Read("species"));
        foreach(var invalid in new JsonNode[]{JsonValue.Create("Luck")!,JsonValue.Create("0")!,JsonValue.Create(7)!,new JsonArray("Wisdom")})
        {
            raw["general_feats"]![0]!["choices"]!["ability"]=invalid;before=raw.ToJsonString();
            var draft=catalog.Derive(raw,[],true);Assert.IsTrue(draft.Issues.Any(issue=>issue.Contains("Resilient",StringComparison.Ordinal)));Assert.AreEqual(before,raw.ToJsonString());Assert.IsFalse(draft.Standing.SaveProficiencies.Contains(Ability.Wisdom));
            Assert.Throws<ArgumentException>(()=>catalog.Derive(raw,[]));
        }
        raw["general_feats"]![0]!["choices"]!["ability"]="Wisdom";Assert.IsTrue(catalog.Derive(raw,[]).Standing.SaveProficiencies.Contains(Ability.Wisdom));
    }
}
