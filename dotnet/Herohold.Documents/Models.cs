using System.Text.Json.Nodes;
using Herohold.Core;

namespace Herohold.Documents;

public sealed record PdfIssue(string Code,string Severity,string Section,string Label,JsonNode? Observed=null,JsonNode? Expected=null,string Remedy="");
public sealed record PdfInspection
{
    public string Status {get;init;}="review_required";
    public string SourceKind {get;init;}="compatible_form";
    public JsonObject Fields {get;init;}=new();
    public JsonObject? EmbeddedCanonical {get;init;}
    public JsonObject? Envelope {get;init;}
    public JsonObject PassthroughFields {get;init;}=new();
    public List<PdfIssue> Issues {get;init;}=[];
    public JsonObject ExtractedData {get;init;}=new();
    public JsonObject PayloadInfo {get;init;}=new();
    public int PageCount {get;init;}
    public string TemplateMatch {get;init;}="unsupported";
}
public interface IPdfService
{
    Task<PdfInspection> InspectAsync(byte[] pdf,CancellationToken cancellationToken=default);
    Task<byte[]> ExportAsync(CharacterState character,JsonObject canonical,CancellationToken cancellationToken=default);
}
