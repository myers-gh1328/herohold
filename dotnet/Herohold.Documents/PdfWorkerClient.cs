using Herohold.Core;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Diagnostics;
using System.Text;
namespace Herohold.Documents;

public sealed class PdfWorkerClient(string workerAssemblyPath,TimeSpan? timeout=null):IPdfService
{
    private static readonly SemaphoreSlim Slots=new(1,1);
    public async Task<PdfInspection> InspectAsync(byte[] pdf,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if(pdf.Length>PdfCodec.MaximumPdfBytes)return PdfCodec.Error("pdf_too_large","The PDF exceeds the supported 20 MB limit.");
        var response=await InvokeAsync(new(){["version"]=1,["operation"]="inspect",["pdf"]=Convert.ToBase64String(pdf)},cancellationToken);
        return response["inspection"]?.Deserialize<PdfInspection>(PdfWorkerProtocol.Json)??throw new PdfFormatException("pdf_worker_invalid_response","The PDF service returned an invalid response.");
    }
    public async Task<byte[]> ExportAsync(CharacterState character,JsonObject canonical,CancellationToken cancellationToken=default)
    {
        var response=await InvokeAsync(new(){["version"]=1,["operation"]="export",["character"]=JsonSerializer.SerializeToNode(character,PdfWorkerProtocol.Json),["canonical"]=canonical.DeepClone()},cancellationToken);
        var bytes=Convert.FromBase64String(response["pdf"]?.GetValue<string>()??throw new PdfFormatException("pdf_worker_invalid_response","The PDF service returned an invalid response."));
        if(bytes.Length>PdfCodec.MaximumPdfBytes)throw new PdfFormatException("pdf_too_large","The exported PDF exceeds the supported 20 MB limit.");
        return bytes;
    }
    private async Task<JsonObject> InvokeAsync(JsonObject request,CancellationToken cancellationToken)
    {
        var message=request.ToJsonString(PdfWorkerProtocol.Json);
        if(message.Length>PdfWorkerProtocol.MaximumMessageCharacters)throw new PdfFormatException("pdf_request_too_large","The PDF request exceeds the supported size limit.");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(timeout??TimeSpan.FromSeconds(45));
        Process? process=null;var acquired=false;
        try
        {
            await Slots.WaitAsync(deadline.Token);acquired=true;
            var path=Path.GetFullPath(workerAssemblyPath);
            if(!File.Exists(path))throw new PdfFormatException("pdf_worker_unavailable","The PDF service is unavailable.");
            var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=new UTF8Encoding(false),StandardErrorEncoding=new UTF8Encoding(false),WorkingDirectory=Path.GetDirectoryName(path)!};
            start.ArgumentList.Add(path);
            start.Environment.Clear();
            foreach(var key in new[]{"PATH","DOTNET_ROOT","SystemRoot","WINDIR","TMPDIR","TMP","TEMP"})if(Environment.GetEnvironmentVariable(key) is {} value)start.Environment[key]=value;
            start.Environment["DOTNET_GCHeapHardLimit"]="10000000";start.Environment["DOTNET_EnableDiagnostics"]="0";
            process=Process.Start(start)??throw new PdfFormatException("pdf_worker_unavailable","The PDF service could not start.");
            var stdout=PdfWorkerProtocol.ReadBoundedAsync(process.StandardOutput,PdfWorkerProtocol.MaximumMessageCharacters,deadline.Token);
            var stderr=PdfWorkerProtocol.ReadBoundedAsync(process.StandardError,64*1024,deadline.Token);
            await process.StandardInput.WriteAsync(message.AsMemory(),deadline.Token);process.StandardInput.Close();
            await Task.WhenAll(stdout,stderr,process.WaitForExitAsync(deadline.Token));
            var diagnostics=await stderr;
            if(process.ExitCode!=0)throw new PdfFormatException("pdf_worker_failed","The PDF service could not complete this operation."){Diagnostics=diagnostics};
            var result=JsonNode.Parse(await stdout,documentOptions:new(){MaxDepth=64}) as JsonObject??throw new JsonException();
            if(result["ok"]?.GetValue<bool>() is not true)throw new PdfFormatException(result["error"]?["code"]?.GetValue<string>()??"pdf_worker_failed",result["error"]?["message"]?.GetValue<string>()??"The PDF operation failed."){Diagnostics=diagnostics};
            return result;
        }
        catch(OperationCanceledException)when(!cancellationToken.IsCancellationRequested){throw new PdfFormatException("pdf_worker_timeout","The PDF operation took too long. Try a smaller supported PDF.");}
        catch(JsonException){throw new PdfFormatException("pdf_worker_invalid_response","The PDF service returned an invalid response.");}
        finally
        {
            if(process is not null)
            {
                try{if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));}}catch(InvalidOperationException){}catch(TimeoutException){}
                process.Dispose();
            }
            if(acquired)Slots.Release();
        }
    }
}
