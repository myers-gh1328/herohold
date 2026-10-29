using System.Text.Json.Nodes;
using Herohold.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestCategory("RulesData")][TestClass]public sealed class PermanentOriginEffectTests
{
    private static JsonObject Raw()=>JsonNode.Parse("""{"class":"Fighter","level":20,"background":"Scribe","species":"Human","base_ability_scores":{"Strength":10,"Dexterity":10,"Constitution":10,"Intelligence":10,"Wisdom":10,"Charisma":10},"species_options":{"origin_feats":["Skilled"]},"origin_feat_choices":{"skills":["Arcana","History","Religion"]},"folio_origin_feat_choices":{"Skilled":{"skills":["Insight","Survival","Nature"]}},"general_feats":[{"instance_id":"fictional-skilled","name":"Skilled","choices":{"proficiencies":["Skill: Stealth","Skill: Acrobatics","Tool: Dice Set"]}},{"instance_id":"fictional-alert","name":"Alert","choices":{}},{"instance_id":"fictional-lucky","name":"Lucky","choices":{}},{"instance_id":"fictional-tough","name":"Tough","choices":{}}]}""")!.AsObject();
    [TestMethod]public void TavernBrawlerProvidesItsOwnedImprovisedWeaponProficiencyAndRemovalClearsIt()
    {
        var raw=Raw();raw["general_feats"]!.AsArray().Add(new JsonObject{["instance_id"]="fictional-brawler",["name"]="Tavern Brawler"});
        var catalog=CharacterBuildCatalog.Parse(ClassProgressionCatalog.Parse(SpellGrantFixture.Read("classes")),SpellGrantFixture.Read("backgrounds"),SpellGrantFixture.Read("species"));
        Assert.IsTrue(catalog.Derive(raw,[],true).ProficiencySources.Any(source=>source.Kind==ProficiencySourceKind.Weapon&&source.Target=="Improvised Weapons"&&source.SourceId=="feat:fictional-brawler"));
        raw["general_feats"]!.AsArray().RemoveAt(raw["general_feats"]!.AsArray().Count-1);Assert.IsFalse(catalog.Derive(raw,[],true).ProficiencySources.Any(source=>source.Target=="Improvised Weapons"));
    }
    [TestMethod]public void GeneralMagicInitiateRetainsItsCastingAbilityPoolAndIndependentSpellOwnership()
    {
        var raw=JsonNode.Parse("""{"class":"Fighter","level":4,"background":"Acolyte","species":"Orc","origin_feat_choices":{"cantrips":["Guidance","Light"],"spell":"Bless","spellcasting_ability":"Wisdom"},"general_feats":[{"instance_id":"fictional-druid-initiate","feat_id":"magic_initiate_druid","name":"Magic Initiate (Druid)","choices":{"cantrips":["Guidance","Starry Wisp"],"spell":"Cure Wounds","spellcasting_ability":"Charisma"}}]}""")!.AsObject();
        var rules=new SpellGrantRules(ClassProgressionCatalog.Parse(SpellGrantFixture.Read("classes")),SpellGrantFixture.Snapshot(),SpellGrantFixture.Species());var grants=rules.Derive(raw,Enum.GetValues<Ability>().ToDictionary(ability=>ability,_=>2));
        var cure=grants.Metadata.Single(grant=>grant.SpellName=="Cure Wounds");Assert.AreEqual(Ability.Charisma,cure.Ability);Assert.AreEqual(1,cure.MaxUses);Assert.IsTrue(cure.AllowsSlots);Assert.AreEqual(2,grants.Metadata.Count(grant=>grant.SpellName=="Guidance"));Assert.IsTrue(grants.Sources.Any(source=>source.SourceId=="feat:fictional-druid-initiate"));
    }
    [TestMethod]public void PermanentOriginFeatsFeedStandingHpAndResourcesAndKeepEachSkilledSource()
    {
        var snapshot=SpellGrantFixture.Snapshot();var classes=ClassProgressionCatalog.Parse(SpellGrantFixture.Read("classes"));var raw=Raw();var catalog=CharacterBuildCatalog.Parse(classes,SpellGrantFixture.Read("backgrounds"),SpellGrantFixture.Read("species"));var build=catalog.Derive(raw,[],true);
        foreach(var name in new[]{"Arcana","History","Religion","Insight","Survival","Nature","Stealth","Acrobatics"})Assert.IsTrue(build.Standing.SkillProficiencies.Contains(name),name+" must retain its owned Skilled source.");
        Assert.AreEqual(6,build.Standing.Initiative.Total);Assert.AreEqual(3,build.ProficiencySources.Where(source=>source.Kind==ProficiencySourceKind.Skill&&new[]{"Arcana","Insight","Stealth"}.Contains(source.Target)).Select(source=>source.SourceId).Distinct().Count());
        Assert.AreEqual(164,HitPointRules.Calculate(CanonicalFixtures.HitPoints(raw,10,0)).Maximum);
        var choices=new ClassChoiceRules(classes,snapshot,BeastChoiceCatalog.Parse("[]"),SpellCapabilityCatalog.Parse("{\"spell\":[]}"));var active=new ActiveFeatureRules(snapshot,classes,choices,RulesReference.Empty,SpeciesSourceCatalog.Parse(File.ReadAllText(Path.Combine(SpellGrantFixture.Root(),"data/raw/5etools/races.json")))).Derive(raw,build);
        Assert.AreEqual(6,new ClassResourceRules(classes).Derive(raw,build,active).Metadata.Single(pool=>pool.Id=="luck").Maximum);Assert.AreEqual(3,active.Details.Count(detail=>detail.Name=="Skilled"));
    }
}
