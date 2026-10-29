namespace Herohold.Web.Services;
public sealed record CharacterSummary(string Id,string Name,string Species,string ClassName,int Level,long Revision);
public sealed record ExportArtifact(string FileName,string MediaType,byte[] Bytes);
