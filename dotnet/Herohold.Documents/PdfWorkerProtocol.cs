using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
[assembly:InternalsVisibleTo("Herohold.PdfWorker")]
namespace Herohold.Documents;

internal static class PdfWorkerProtocol
{
    public const int MaximumMessageCharacters=32*1024*1024;
    public static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping,Converters={new JsonStringEnumConverter()}};
    public static async Task<string> ReadBoundedAsync(StreamReader reader,int limit,CancellationToken token=default)
    {
        var text=new StringBuilder();var buffer=new char[8192];
        while(true){var read=await reader.ReadAsync(buffer.AsMemory(),token);if(read==0)return text.ToString();if(text.Length+read>limit)throw new PdfFormatException("pdf_worker_output_too_large","The PDF operation exceeded its output limit.");text.Append(buffer,0,read);}
    }
}
