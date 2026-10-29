using System.Text.Json.Nodes;
using Herohold.Core;
namespace Herohold.Rules;
public sealed record SpellGrantMetadata
{
    public string Id { get; init; } = "";
    public string SpellName { get; init; } = "";
    public Ability Ability { get; init; }
    public string? ResourceId { get; init; }
    public int MaxUses { get; init; }
    public string Recovery { get; init; } = "none";
    public bool AllowsSlots { get; init; }
    public int BaseLevel { get; init; }
    public string Source { get; init; } = "";
    public bool AtWill { get; init; }
    public bool RitualOnly { get; init; }
}
public sealed record SpellGrantSourceIdentity(string GrantId,string SourceId,string SourceKind,string SourceName,Ability Ability);
public sealed record SpeciesSpellGrantSummary
{
    public IReadOnlyList<SpellGrantMetadata> Metadata { get; init; } = [];
    public IReadOnlyList<string> SelectedSpells { get; init; } = [];
    public IReadOnlyList<string> AutomaticSpells { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
public sealed record SpellGrantSummary
{
    /// <summary>The granted spells with their sources, before payment metadata.</summary>
    public IReadOnlyList<SpellGrantRecord> Records { get; init; } = [];
    public IReadOnlyList<SpellGrantMetadata> Metadata { get; init; } = [];
    public IReadOnlyList<SpellGrantSourceIdentity> Sources { get; init; } = [];
    public IReadOnlyList<string> GrantedSpellNames { get; init; } = [];
    public IReadOnlyList<string> SpeciesSpells { get; init; } = [];
    public IReadOnlyList<string> AutomaticSpeciesSpells { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Where a granted spell comes from. Written in key order, the fields that are set form the grant's saved identity
/// (see <see cref="SpellGrantIdentity"/>), so each source sets exactly the fields it names.
/// </summary>
public sealed record SpellGrantSource
{
    public string Type { get; init; } = "";
    public string? SourceName { get; init; }
    public string? FeatureName { get; init; }
    public string? ClassName { get; init; }
    public string? SubclassName { get; init; }
    public string? SubclassId { get; init; }
    /// <summary>The rules book of a subclass source (for example "XPHB").</summary>
    public string? Book { get; init; }
    public string? InstanceId { get; init; }
    public string? FeatId { get; init; }
    public int? Level { get; init; }
    public int? LevelGained { get; init; }
    /// <summary>The name a sheet shows for the source.</summary>
    public string Label => SourceName ?? FeatureName ?? SubclassName ?? (Type.Length > 0 ? Type : "Spell grant");
}
/// <summary>How a granted spell may be cast: free uses and their recharge, whether slots may pay for it, at will or as a ritual only.</summary>
public sealed record GrantCastingRules
{
    public int FreeCasts { get; init; }
    public string Recharge { get; init; } = "none";
    public bool? CanUseSpellSlots { get; init; }
    public bool? UsesSpellSlots { get; init; }
    public bool? UsesPactSlots { get; init; }
    public bool AtWillLowestLevel { get; init; }
    public bool RitualOnly { get; init; }
    public bool AllowsSlots => (CanUseSpellSlots ?? UsesSpellSlots ?? true) && (UsesPactSlots ?? true);
}
/// <summary>A spell a class, subclass, feat or origin grants.</summary>
public sealed record SpellGrantRecord
{
    public string Name { get; init; } = "";
    public SpellGrantSource Source { get; init; } = new();
    public string SpellcastingAbility { get; init; } = "";
    public string GrantType { get; init; } = "";
    public bool AlwaysPrepared { get; init; }
    public GrantCastingRules Rules { get; init; } = new();

    /// <summary>The granted spells a character document showed (its "spell_grant_details"), as far as they name an owner.</summary>
    public static IReadOnlyList<SpellGrantRecord> FromDocument(JsonNode? details)
    {
        static string Text(JsonNode? n)=>n is JsonValue v&&v.TryGetValue<string>(out var text)?text:"";
        static string? Named(JsonNode? n)=>n is JsonValue v&&v.TryGetValue<string>(out var text)?text:null;
        return [..(details as JsonArray??[]).OfType<JsonObject>().Select(d=>new SpellGrantRecord{Name=Text(d["name"]),GrantType=Text(d["grant_type"]),AlwaysPrepared=d["always_prepared"] is JsonValue p&&p.TryGetValue<bool>(out var prepared)&&prepared,
            Source=d["source"] is JsonObject s?new(){Type=Text(s["type"]),SourceName=Named(s["source_name"]),FeatureName=Named(s["feature_name"]),InstanceId=Named(s["instance_id"])}:new()})];
    }
}
