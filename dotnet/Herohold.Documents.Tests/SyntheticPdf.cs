using System.Text;
using System.Globalization;
namespace Herohold.Documents.Tests;

internal static class SyntheticPdf
{
    // Independent minimal PDF writer: intentionally repeats the same name-tree key.
    public static byte[] DuplicateBuilderAttachments(byte[] payload)
    {
        var name=PdfEnvelope.AttachmentName;
        var file=$"<< /Type /Filespec /F ({name}) /EF << /F 5 0 R >> >>";
        byte[] Stream(byte[] data)=>Encoding.ASCII.GetBytes($"<< /Type /EmbeddedFile /Length {data.Length} >>\nstream\n").Concat(data).Concat(Encoding.ASCII.GetBytes("\nendstream")).ToArray();
        var objects=new[]{Encoding.ASCII.GetBytes($"<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [({name}) 4 0 R ({name}) 6 0 R] >> >> >>"),Encoding.ASCII.GetBytes("<< /Type /Pages /Count 1 /Kids [3 0 R] >>"),Encoding.ASCII.GetBytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> >>"),Encoding.ASCII.GetBytes(file),Stream(payload),Encoding.ASCII.GetBytes(file.Replace("5 0 R","7 0 R")),Stream(payload)};
        using var output=new MemoryStream();void Write(string text)=>output.Write(Encoding.ASCII.GetBytes(text));
        Write("%PDF-1.4\n");var offsets=new List<long>{0};
        for(var i=0;i<objects.Length;i++){offsets.Add(output.Position);Write($"{i+1} 0 obj\n");output.Write(objects[i]);Write("\nendobj\n");}
        var xref=output.Position;Write($"xref\n0 {objects.Length+1}\n0000000000 65535 f \n");
        foreach(var offset in offsets.Skip(1))Write(offset.ToString("D10",CultureInfo.InvariantCulture)+" 00000 n \n");
        Write($"trailer\n<< /Size {objects.Length+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");return output.ToArray();
    }
}
