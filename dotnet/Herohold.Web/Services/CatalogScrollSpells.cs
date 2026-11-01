using Herohold.Core;
using Herohold.Rules;
namespace Herohold.Web.Services;

/// <summary>Scroll spells from the rules catalog; a 2024 spell is preferred over an older one of the same name.</summary>
public sealed class CatalogScrollSpells(RulesCatalogSnapshot catalog):IScrollSpells
{
    private readonly Dictionary<string,CatalogSpell> byName=catalog.SpellRecords.OrderBy(s=>s.Source=="XPHB"?0:1)
        .GroupBy(s=>s.Name,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
    public ScrollSpell? Find(string name)=>byName.TryGetValue(name.Trim(),out var spell)?new(WorkspaceProjection.MapSpell(spell,"Spell Scroll"),spell.Classes):null;
}
