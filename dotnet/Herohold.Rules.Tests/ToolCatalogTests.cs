using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Rules.Tests;
[TestClass]public sealed class ToolCatalogTests
{
    internal static ToolCatalog Actual()=>ToolCatalog.Parse(File.ReadAllText(Path.Combine(SpellGrantFixture.Root(),"data/raw/5etools/items-base.json")),File.ReadAllText(Path.Combine(SpellGrantFixture.Root(),"data/raw/5etools/items.json")));
    [TestCategory("RulesData")][TestMethod]public void TrustedRawTypesProduceActualToolsRatherThanCategoryProficiencies()
    {
        var catalog=Actual();Assert.AreEqual(17,catalog.ArtisanTools.Count);Assert.AreEqual(4,catalog.GamingSets.Count);Assert.AreEqual(10,catalog.MusicalInstruments.Count);Assert.AreEqual(6,catalog.OtherTools.Count);Assert.AreEqual(37,catalog.All.Count);
        CollectionAssert.AreEqual(new[]{"Dice Set","Dragonchess Set","Playing Cards","Three-Dragon Ante Set"},catalog.Expand(["Any gaming set"]).ToArray());Assert.IsTrue(catalog.Expand(["Any artisans tool"]).Contains("Smith's Tools"));Assert.IsFalse(catalog.All.Any(name=>name.StartsWith("Any ")));
        CollectionAssert.AreEqual(new[]{"Thieves' Tools"},catalog.Expand(["Thieves' Tools"]).ToArray());Assert.AreEqual(37,catalog.Expand(["Any artisan's tools","Any gaming set","Any musical instrument","Any other tool"]).Count);
    }
    [TestMethod]public void ToolParsingUsesFixedStructuredTypesAndExcludesOtherSourcesAndUnknownCategories()
    {
        var catalog=ToolCatalog.Parse("""{"baseitem":[{"name":"Known Instrument","source":"XPHB","type":"INS|XPHB"},{"name":"Wrong Source","source":"PHB","type":"INS|PHB"}]}""","""{"item":[{"name":"Known Game","source":"XPHB","type":"GS|XPHB"},{"name":"Not A Tool","source":"XPHB","type":"G|XPHB"}]}""");
        CollectionAssert.AreEqual(new[]{"Known Game","Known Instrument"},catalog.All.ToArray());Assert.AreEqual(0,catalog.Expand(["Any unsupported group"]).Count);
        Assert.ThrowsExactly<ArgumentException>(()=>ToolCatalog.Parse("[]"));Assert.ThrowsExactly<ArgumentException>(()=>ToolCatalog.Parse("{\"baseitem\":42}"));
    }
}
