using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Core.Tests;

[TestClass]
public sealed class LegacyItemDefenseTests
{
    [TestMethod]
    public async Task BoundedUnknownItemDefenseLoadsAndSurvivesAnUnrelatedSourceEditAndJsonRoundTrip()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-legacy-item-defense-"+Guid.NewGuid().ToString("N"));
        try
        {
            var c=Fixtures.Ranger() with{Id="legacy-item-defense",Equipment=[new(){Id="moon-ward",Name="Old ward",Defenses=[new(){Kind="resistance",Type="Moonlight",Source="Imported source",Note="Custom table rule"}]}]};
            var json=new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={new JsonStringEnumConverter()}};var snapshot=new WorkspaceSnapshot{Revision=1,Character=c};Directory.CreateDirectory(directory);await File.WriteAllTextAsync(Path.Combine(directory,c.Id+".json"),JsonSerializer.Serialize(snapshot,json));
            var loaded=(await LegacyJsonSaves.ReadAllAsync(directory)).Single().Snapshot;Assert.AreEqual("Moonlight",loaded.Character.Equipment.Single().Defenses.Single().Type);
            var edited=WorkspaceRules.UpdateEquipment(loaded.Character,loaded.Character.Equipment.Single() with{Name="Renamed ward"});var saved=loaded with{Character=edited};var exported=JsonSerializer.Serialize(saved,json);var restored=JsonSerializer.Deserialize<WorkspaceSnapshot>(exported,json)!;Assert.AreEqual("Imported source",restored.Character.Equipment.Single().Defenses.Single().Source);Assert.AreEqual("Custom table rule",restored.Character.Equipment.Single().Defenses.Single().Note);
            Assert.ThrowsExactly<ArgumentException>(()=>WorkspaceRules.UpdateEquipment(edited,edited.Equipment.Single() with{Defenses=[new(){Kind="invalid",Type="Moonlight"}]}));Assert.ThrowsExactly<ArgumentException>(()=>WorkspaceRules.UpdateEquipment(edited,edited.Equipment.Single() with{Defenses=[new(){Kind="resistance",Type=new string('x',101)}]}));
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
