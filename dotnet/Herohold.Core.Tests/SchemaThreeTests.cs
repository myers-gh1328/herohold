using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;
[TestClass] public sealed class SchemaThreeTests
{
    [TestMethod] public async Task SchemaTwoReadsAsThreeWithoutWriting()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-schema3-"+Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);var c=Fixtures.Ranger();var path=Path.Combine(directory,c.Id+".json");
            var document=System.Text.Json.JsonSerializer.SerializeToNode(new WorkspaceSnapshot{Revision=1,Character=c},LegacyJsonSaves.Json)!.AsObject();
            document["schemaVersion"]=2;document["character"]!.AsObject().Remove("standingAdjustmentTerms");
            var old=document.ToJsonString();await File.WriteAllTextAsync(path,old);
            var loaded=(await LegacyJsonSaves.ReadAllAsync(directory)).Single().Snapshot;Assert.AreEqual(3,loaded.SchemaVersion);Assert.AreEqual(old,await File.ReadAllTextAsync(path));
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
