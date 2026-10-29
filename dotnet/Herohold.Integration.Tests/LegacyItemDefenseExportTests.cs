using Herohold.TestSupport;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;

[TestClass]
public sealed class LegacyItemDefenseExportTests
{
    [TestMethod]
    public async Task LegacyItemDefenseRemainsInPublicWorkspaceExportAfterEditingItsItem()
    {
        var directory=Path.Combine(Path.GetTempPath(),"herohold-legacy-defense-export-"+Guid.NewGuid().ToString("N"));
        try
        {
            var c=SampleWorkspace.Create().Character with{Id="legacy-defense-export",Build=null,Equipment=[new(){Id="moon-ward",Name="Old ward",Defenses=[new(){Kind="resistance",Type="Moonlight",Source="Legacy origin",Note="Keep this reference"}]}]};var options=new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={new JsonStringEnumConverter()}};await HandMadeStore.At(directory).SaveAsync(new(){Character=c},0);_=options;
            var session=new WorkspaceSession(HandMadeStore.At(directory));await session.SwitchCharacterAsync(c.Id);Assert.IsNull(session.Error);await session.UpdateEquipmentAsync(session.Character!.Equipment.Single() with{Name="Renamed ward"});Assert.IsNull(session.Error);
            var artifact=await session.ExportAsync("workspace");Assert.IsNull(session.Error);Assert.IsNotNull(artifact);var doc=JsonNode.Parse(artifact.Bytes)!;var defense=doc["character"]!["equipment"]![0]!["defenses"]![0]!;Assert.AreEqual("Moonlight",defense["type"]!.GetValue<string>());Assert.AreEqual("Legacy origin",defense["source"]!.GetValue<string>());Assert.AreEqual("Keep this reference",defense["note"]!.GetValue<string>());await session.ReloadAsync();Assert.AreEqual("Moonlight",session.Character!.Equipment.Single().Defenses.Single().Type);
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
