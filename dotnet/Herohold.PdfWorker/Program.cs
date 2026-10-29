using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Documents;

Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);
var protocolOutput=Console.Out;
JsonObject response;
try
{
    using var input=new StreamReader(Console.OpenStandardInput(),new UTF8Encoding(false,true));
    var message=await PdfWorkerProtocol.ReadBoundedAsync(input,PdfWorkerProtocol.MaximumMessageCharacters);
    var request=JsonNode.Parse(message,documentOptions:new(){MaxDepth=64}) as JsonObject??throw new JsonException();
    if(request["version"]?.GetValue<int>()!=1)throw new PdfFormatException("pdf_protocol_version","Unsupported PDF worker protocol.");
    Console.SetOut(TextWriter.Null);
    switch(request["operation"]?.GetValue<string>())
    {
        case "inspect":
            var bytes=Convert.FromBase64String(request["pdf"]?.GetValue<string>()??"");
            response=new(){["ok"]=true,["inspection"]=JsonSerializer.SerializeToNode(PdfCodec.Inspect(bytes),PdfWorkerProtocol.Json)};break;
        case "export":
            var character=request["character"]?.Deserialize<CharacterState>(PdfWorkerProtocol.Json)??throw new JsonException();
            var canonical=request["canonical"] as JsonObject??throw new JsonException();
            response=new(){["ok"]=true,["pdf"]=Convert.ToBase64String(PdfCodec.Export(character,canonical))};break;
        default:throw new PdfFormatException("pdf_unsupported_operation","Unsupported PDF operation.");
    }
}
catch(PdfFormatException exception){response=new(){["ok"]=false,["error"]=new JsonObject{["code"]=exception.Code,["message"]=exception.Message}};}
catch(Exception exception)when(exception is not OutOfMemoryException){Console.Error.WriteLine(exception.GetType().FullName+": "+exception.Message);response=new(){["ok"]=false,["error"]=new JsonObject{["code"]="pdf_invalid_request",["message"]="The PDF request could not be processed."}};}
Console.SetOut(protocolOutput);
await protocolOutput.WriteAsync(response.ToJsonString(PdfWorkerProtocol.Json));
