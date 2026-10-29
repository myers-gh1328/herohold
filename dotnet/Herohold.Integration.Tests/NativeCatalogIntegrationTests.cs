using Herohold.TestSupport;
using Herohold.Rules;
using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;

[TestCategory("RulesData")]
[TestClass]
public sealed class NativeCatalogIntegrationTests
{
    internal static RulesCatalogSnapshot Snapshot()
    {
        var root=new DirectoryInfo(Environment.CurrentDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"data/imported/5etools/classes.json")))root=root.Parent;
        var data=Path.Combine(root!.FullName,"data/imported/5etools");
        string Read(string name)=>File.ReadAllText(Path.Combine(data,name+".json"));
        return RulesCatalogSnapshot.Parse(Read("classes"),Read("species"),Read("backgrounds"),Read("feats"),Read("spells"),Read("equipment"),Read("weapons"),
            File.ReadAllText(Path.Combine(root.FullName,"data/raw/5etools/spells/spells-xphb.json")));
    }
    [TestMethod]
    public async Task SessionCatalogLoadingUsesTheNativeRulesData()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-native-catalog-"+Guid.NewGuid().ToString("N"));
        try
        {
            var session=new WorkspaceSession(TestStores.Open(directory),NativeWorkflowTests.Workflow());
            await session.InitializeAsync();await session.LoadCatalogAsync();
            Assert.IsNull(session.Error);
            Assert.AreEqual(217,session.Items!.Equipment.Count);
            Assert.Contains("Longbow",session.Items.Weapons);
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
