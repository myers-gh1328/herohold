using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Documents.Tests;

[TestClass]
public sealed class PdfWorkerTests
{
    private static string WorkerPath=>Path.Combine(AppContext.BaseDirectory,"pdf-worker","Herohold.PdfWorker.dll");
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task CompiledWorkerInspectsAndExportsEditableNativePdfWithoutPython()
    {
        var client=new PdfWorkerClient(WorkerPath);
        var source=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"CharacterSheet.pdf"));
        var raw=await client.InspectAsync(source);Assert.AreEqual(334,raw.Fields.Count);
        var character=PdfFieldMapperTests.FictionalCharacter();
        var canonical=new JsonObject{["schema_version"]=5,["name"]=character.Name,["class"]=character.ClassName,["species"]=character.Species,["level"]=character.Level};
        var exported=await client.ExportAsync(character,canonical);
        var result=await client.InspectAsync(exported);
        Assert.AreEqual("ready",result.Status);
        Assert.AreEqual("31",result.Fields["HPCurrent"]!.GetValue<string>());
        Assert.AreEqual("Éowyn Ω",result.EmbeddedCanonical!["name"]!.GetValue<string>());
    }
    [TestCategory("RulesData")]
    [TestMethod]
    public async Task ExpiredDeadlineTerminatesTheWorkerInsteadOfLeavingPdfParsingRunning()
    {
        var client=new PdfWorkerClient(WorkerPath,TimeSpan.FromMilliseconds(1));
        var watch=System.Diagnostics.Stopwatch.StartNew();
        var error=await Assert.ThrowsAsync<PdfFormatException>(()=>client.InspectAsync(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"CharacterSheet.pdf"))));
        Assert.AreEqual("pdf_worker_timeout",error.Code);
        Assert.IsLessThan(TimeSpan.FromSeconds(5),watch.Elapsed);
    }
    [TestMethod]
    public async Task CallerCancellationIsPreservedAndOversizeInputDoesNotLaunchAWorker()
    {
        var client=new PdfWorkerClient("missing-worker.dll");
        var large=await client.InspectAsync(new byte[PdfCodec.MaximumPdfBytes+1]);
        Assert.AreEqual("pdf_too_large",large.Issues.Single().Code);
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(()=>client.InspectAsync([1,2,3],cancel.Token));
    }
    [TestMethod]
    public async Task UnexpectedWorkerErrorsReachStandardErrorButPlayersSeeAGenericMessage()
    {
        var start=new System.Diagnostics.ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add(WorkerPath);
        using var process=System.Diagnostics.Process.Start(start)!;
        await process.StandardInput.WriteAsync("""{"version":1,"operation":"export","character":5,"canonical":{}}""");process.StandardInput.Close();
        var output=await process.StandardOutput.ReadToEndAsync();var error=await process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();
        StringAssert.Contains(output,"pdf_invalid_request");Assert.IsFalse(output.Contains("JsonException"),"Exception details stay out of the protocol reply.");
        StringAssert.Contains(error,"JsonException");
    }
}
