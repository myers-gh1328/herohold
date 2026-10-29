using Aspose.Pdf;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;

namespace Herohold.Documents;

/// <summary>PDF parsing belongs in the disposable worker; application callers use IPdfService.</summary>
public static class PdfCodec
{
    public static byte[] Export(CharacterState character,JsonObject canonical)
    {
        var owned=PdfFieldMapper.Create(character);
        var fields=PdfPassthrough.Normalize(canonical["pdf_passthrough_fields"]);
        foreach(var (key,value) in owned)fields[key]=value?.DeepClone();
        var spells=PdfFieldMapper.OwnedSpells(character);
        var overflow=spells.GroupBy(s=>s.Level).Any(group=>!PdfFields.Spells.TryGetValue(group.Key,out var rows)||group.Count()>rows.Length);
        return FillTemplate(fields,PdfEnvelope.Create(canonical,owned),overflow?spells:null);
    }
    private static readonly Lazy<bool> FontsConfigured=new(()=>
    {
        using var font=typeof(PdfCodec).Assembly.GetManifestResourceStream("Herohold.Documents.NotoSans-Regular.ttf")??throw new InvalidOperationException("The bundled PDF font is unavailable.");
        using var buffer=new MemoryStream();font.CopyTo(buffer);FontRepository.Sources.Add(new MemoryFontSource(buffer.ToArray()));return true;
    });
    public static byte[] FillTemplate(JsonObject fields,byte[] envelope)
        =>FillTemplate(fields,envelope,null);
    private static byte[] FillTemplate(JsonObject fields,byte[] envelope,IReadOnlyList<SpellDefinition>? continuation)
    {
        _=PdfEnvelope.Parse(envelope);_=FontsConfigured.Value;
        if(fields.Count>MaximumFields)throw new PdfFormatException("form_too_many_fields","Too many export fields.");
        using var document=Document.Open(TemplateBytes());document.Form!.AutoRecalculate=false;
        // Filled fields and attachments invalidate the trusted template's byte-offset hints.
        document.IsLinearized=false;
        foreach(var field in document.Form.Fields)
        {
            if(field.FullName is not {} name||!fields.TryGetPropertyValue(name,out var value))continue;
            if(field is CheckboxField checkbox){checkbox.Value=value is JsonValue boolean&&boolean.TryGetValue<bool>(out var selected)&&selected?"Yes":"Off";continue;}
            var text=value is JsonValue scalar&&scalar.TryGetValue<string>(out var content)?content:value?.ToJsonString()??"";
            if(Encoding.UTF8.GetByteCount(text)>100_000)throw new PdfFormatException("form_field_value_too_long","A PDF export field exceeds 100 KB.");
            if(field is TextBoxField box)
            {
                var size=name is "Features and Traits" or "Feat+Traits" or "Equipment" or "ProficienciesLang"?7:Math.Clamp(((box.Rect?.Height??18)-4)*.65,7,16);
                box.DefaultAppearance=new DefaultAppearance("Noto Sans",size,System.Drawing.Color.Black);
                box.Multiline=text.Contains('\n')||box.Multiline;box.Value=text;
            }
            else field.Value=text;
        }
        using var attachment=new MemoryStream(envelope);
        document.EmbeddedFiles!.Add(new FileSpecification(attachment,PdfEnvelope.AttachmentName){MimeType="application/json"});
        if(continuation is not null)AppendSpells(document,continuation);
        var output=document.ToArray();
        if(output.Length>MaximumPdfBytes)throw new PdfFormatException("pdf_too_large","The exported PDF exceeds the supported 20 MB limit.");
        return output;
    }
    private static void AppendSpells(Document document,IReadOnlyList<SpellDefinition> spells)
    {
        // Bounded, text-only pages preserve the trusted template's editable field signature.
        const int width=48,linesPerPage=44,maximumPages=32;
        using var fontStream=typeof(PdfCodec).Assembly.GetManifestResourceStream("Herohold.Documents.NotoSans-Regular.ttf")??throw new InvalidOperationException("The bundled PDF font is unavailable.");
        var font=FontRepository.OpenFont(fontStream,FontTypes.TTF);font.IsEmbedded=true;
        var lines=new List<string>();
        foreach(var group in spells.GroupBy(s=>s.Level).OrderBy(g=>g.Key))
        {
            AddLine(group.Key==0?"Cantrips (Level 0)":$"Level {group.Key}");
            foreach(var spell in group)
            {
                var remaining=spell.Name.Replace('\r',' ').Replace('\n',' ').EnumerateRunes().ToArray();
                for(var offset=0;offset<remaining.Length;offset+=width)
                    AddLine("  "+string.Concat(remaining.Skip(offset).Take(width).Select(r=>r.ToString())));
            }
            AddLine("");
        }
        Write(document.Pages[3],font,"Complete spell list continues on page 4.",48,18,8);
        for(var offset=0;offset<lines.Count;offset+=linesPerPage)
        {
            var page=document.Pages.Add();page.PageInfo.Width=612;page.PageInfo.Height=792;
            Write(page,font,"Complete spell list"+(offset==0?"":" (continued)"),48,744,16);
            Write(page,font,"Prepared, known, spellbook and granted spells",48,724,9);
            var y=698d;
            foreach(var line in lines.Skip(offset).Take(linesPerPage)){Write(page,font,line,48,y,10);y-=14;}
            Write(page,font,$"Spell list continuation {offset/linesPerPage+1}",48,24,8);
        }
        void AddLine(string line)
        {
            if(lines.Count>=linesPerPage*maximumPages)throw new PdfFormatException("spell_appendix_too_large","The complete printable spell list exceeds 32 continuation pages.");
            lines.Add(line);
        }
    }
    private static void Write(Page page,Font font,string text,double x,double y,double size)
    {
        var fragment=new TextFragment(text){Position=new Position(x,y)};
        fragment.TextState.Font=font;fragment.TextState.FontSize=(float)size;
        new TextBuilder(page).AppendText(fragment);
    }
    public const int MaximumPdfBytes=20*1024*1024;
    public const int MaximumPayloadBytes=2*1024*1024;
    public const int MaximumFields=1000;
    private static readonly Lazy<HashSet<string>> ReferenceFields=new(()=>
    {
        using var document=Document.Open(TemplateBytes());
        return document.Form!.Fields.Select(f=>f.FullName).OfType<string>().ToHashSet(StringComparer.Ordinal);
    });
    private static readonly HashSet<string> RequiredFields=["CharacterName","ClassLevel","Race ","Background","STR","DEX","CON","INT","WIS","CHA","HPMax"];
    internal static byte[] TemplateBytes()
    {
        using var stream=typeof(PdfCodec).Assembly.GetManifestResourceStream("Herohold.Documents.CharacterSheet.pdf")??throw new InvalidOperationException("The character sheet template was not available when Herohold was built. Place it at templates/pdf/5E_CharacterSheet_Fillable.pdf and rebuild.");
        using var output=new MemoryStream();stream.CopyTo(output);return output.ToArray();
    }
    public static PdfInspection Inspect(byte[] pdf)
    {
        if(pdf is null)return Error("pdf_invalid_type","Choose a PDF file to import.");
        if(pdf.Length>MaximumPdfBytes)return Error("pdf_too_large","The PDF exceeds the supported 20 MB limit.");
        try
        {
            using var document=Document.Open(pdf);
            var fields=new JsonObject();var issues=new List<PdfIssue>();var totalBytes=0;
            var sourceFields=document.Form?.Fields??[];
            if(sourceFields.Length>MaximumFields)return Error("form_too_many_fields","The PDF contains more than 1000 fields.");
            foreach(var field in sourceFields)
            {
                var name=field.FullName;
                if(string.IsNullOrEmpty(name)||name.EnumerateRunes().Count()>256){issues.Add(new("form_field_invalid_name","warning","source","A field with an invalid name was ignored."));continue;}
                if(fields.ContainsKey(name))return Error("form_duplicate_field","The PDF contains ambiguous duplicate field names.");
                var text=Normalize(field.Value??"");
                var size=Encoding.UTF8.GetByteCount(text);totalBytes+=size;
                if(totalBytes>MaximumPayloadBytes)return Error("form_values_too_large","The PDF form values exceed the supported 2 MB text limit.");
                if(size>100_000){issues.Add(new("form_field_value_too_long","warning","source",$"The value in '{name}' is too long and was ignored."));continue;}
                fields[name]=field is CheckboxField or ButtonField?JsonValue.Create(!new[]{"","/off","off","false","0","no"}.Contains(text.ToLowerInvariant())):JsonValue.Create(text);
            }
            var names=sourceFields.Select(f=>f.FullName).OfType<string>().Where(name=>name.Length>0).ToHashSet(StringComparer.Ordinal);
            var signature=Signature(names);var referenceSignature=Signature(ReferenceFields.Value);
            var missing=RequiredFields.Except(names).Order(StringComparer.Ordinal).ToArray();
            var ratio=(double)names.Intersect(ReferenceFields.Value).Count()/ReferenceFields.Value.Count;
            var match=names.SetEquals(ReferenceFields.Value)?"exact":missing.Length==0&&ratio>=.8?"compatible":names.Count==0&&document.Pages.Count>0?"flattened":"unsupported";
            var status=match is "exact" or "compatible"?"review_required":"unsupported";
            if(status=="unsupported")issues.Add(new(match=="flattened"?"flattened_pdf":"unsupported_template","blocker","source","Choose an editable PDF based on the supported character-sheet template."));
            var passthrough=PdfPassthrough.Normalize(fields);
            foreach(var (key,value) in passthrough.ToArray())if(value is null||value is JsonValue scalar&&(scalar.TryGetValue<string>(out var text)&&text.Length==0||scalar.TryGetValue<bool>(out var selected)&&!selected))passthrough.Remove(key);
            var result=new PdfInspection{Status=status,SourceKind=match=="flattened"?"flattened":status=="unsupported"?"other":"compatible_form",Fields=fields,PageCount=document.Pages.Count,TemplateMatch=match,Issues=issues,ExtractedData=PdfFields.Evidence(fields),PassthroughFields=passthrough,
                PayloadInfo=new(){["match"]=match,["signature"]=signature,["reference_signature"]=referenceSignature,["known_field_ratio"]=ratio,["missing_core_fields"]=JsonSerializer.SerializeToNode(missing)}};
            var payloads=document.EmbeddedFiles?.Where(f=>f.Name==PdfEnvelope.AttachmentName||f.UnicodeName==PdfEnvelope.AttachmentName).ToList()??[];
            if(payloads.Count==0)return result;
            if(payloads.Count!=1)throw new PdfFormatException("payload_attachment_ambiguous","The PDF contains more than one builder attachment.");
            var bytes=payloads[0].GetData()??throw new PdfFormatException("payload_invalid_type","The embedded character is not a byte payload.");var envelope=PdfEnvelope.Parse(bytes);var changes=new JsonObject();
            foreach(var (key,value) in envelope["owned_pdf_field_snapshot"]!.AsObject())
                if(Scalar(value)!=Scalar(fields[key]))changes[key]=new JsonObject{["exported"]=value?.DeepClone(),["observed"]=fields[key]?.DeepClone()??JsonValue.Create("")};
            if(changes.Count>0)issues.Add(new("builder_owned_fields_changed","warning","visible_pdf","Builder-owned visible PDF fields changed after export.",changes.DeepClone(),Remedy:"Use the embedded builder state for a lossless import."));
            var info=new JsonObject{["changed_fields"]=changes};
            foreach(var key in new[]{"payload_version","character_schema_version","builder_version","ruleset","exported_at"})info[key]=envelope[key]?.DeepClone();
            return result with{Status=changes.Count==0?"ready":"review_required",SourceKind="embedded_state",Envelope=envelope,EmbeddedCanonical=envelope["character"]!.AsObject().DeepClone().AsObject(),PayloadInfo=info};
        }
        catch(PdfFormatException exception){return Error(exception.Code,exception.Message) with{SourceKind="embedded_state"};}
        catch(InvalidPasswordException){return Error("pdf_encrypted","This PDF is encrypted and cannot be opened without a password.");}
        catch(Exception exception)when(exception is not OutOfMemoryException)
        {
            return Error("pdf_unreadable",$"The PDF could not be read safely:{exception.GetType().Name}.");
        }
    }
    internal static string Normalize(string value)=>value.Replace("\0","").Replace("\r\n","\n").Replace('\r','\n').Trim();
    private static string Scalar(JsonNode? value)=>value is JsonValue scalar&&scalar.TryGetValue<string>(out var text)?Normalize(text):value?.ToJsonString()??"";
    internal static string Signature(IEnumerable<string> fields)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",fields.Order(StringComparer.Ordinal)))));
    internal static PdfInspection Error(string code,string label)=>new(){Status="error",SourceKind="other",Issues=[new(code,"blocker","source",label)]};
}
