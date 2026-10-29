using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;
namespace Herohold.Web.Services;
public static partial class WorkspaceProjection
{
    private static void WriteClassPlaySources(JsonObject doc,CharacterState c)
    {
        if(c.Play.SpellSources.Count==0)return;
        foreach(var source in c.Play.SpellSources.Where(s=>s.SourceKind=="class"&&s.ClassTrackId is not null))
        {
            var track=(doc["folio_class_tracks"] as JsonArray)?.OfType<JsonObject>().SingleOrDefault(t=>Text(t["id"])==source.ClassTrackId);
            if(track is null)continue;
            var choices=track["choices"] as JsonObject;
            if(choices is null){choices=new();track["choices"]=choices;}
            var spells=choices["spells"] as JsonObject;
            if(spells is null){spells=new();choices["spells"]=spells;}
            JsonArray Names(IEnumerable<string> ids)=>new(ids.Except(source.GrantedSpellIds).Select(id=>JsonValue.Create(c.Play.Spells.FirstOrDefault(s=>s.Id==id)?.Name??id)).ToArray());
            spells["prepared"]=Names(source.PreparedSpellIds);spells["known"]=Names(source.KnownSpellIds.Where(id=>c.Play.Spells.FirstOrDefault(s=>s.Id==id)?.Level!=0));spells["cantrips"]=Names(source.KnownSpellIds.Where(id=>c.Play.Spells.FirstOrDefault(s=>s.Id==id)?.Level==0));spells["spellbook"]=Names(source.SpellbookIds);
        }
    }
}
