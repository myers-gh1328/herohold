using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;

namespace Herohold.Rules;

/// <summary>Private normalized catalog, parsed once into immutable typed records.</summary>
public sealed class ClassProgressionCatalog
{
    private readonly Dictionary<string, CatalogClass> entries;
    public IReadOnlyList<ClassIdentity> Classes { get; }
    /// <summary>The catalog's classes, in catalog order.</summary>
    public IReadOnlyList<CatalogClass> Records { get; }

    private ClassProgressionCatalog(IReadOnlyList<CatalogClass> records)
    {
        if (records.Count is < 1 or > 1000) throw new ArgumentException("The class catalog needs between one and one thousand records.");
        entries = new(StringComparer.OrdinalIgnoreCase);
        var identities = new List<ClassIdentity>();
        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.Name) || record.Name.Length > 200 || !entries.TryAdd(record.Name, record)) throw new ArgumentException("Class names must be nonempty and unique.");
            _ = HitPointRules.FixedHitDieGain(record.HitDie);
            var subclasses = record.Subclasses.Select(s => s.DisplayName).ToList();
            if (subclasses.Count > 1000 || subclasses.Any(name => string.IsNullOrWhiteSpace(name) || name.Length > 200) || subclasses.Distinct(StringComparer.OrdinalIgnoreCase).Count() != subclasses.Count)
                throw new ArgumentException("Subclass names must be nonempty and unique within a class.");
            identities.Add(new(record.Name, record.Source, record.HitDie, subclasses.AsReadOnly()));
        }
        Records = records;
        Classes = identities.AsReadOnly();
    }

    public static ClassProgressionCatalog Parse(string normalizedClassesJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedClassesJson);
        if (Encoding.UTF8.GetByteCount(normalizedClassesJson) > 16 * 1024 * 1024) throw new ArgumentException("The class catalog exceeds its supported size.");
        JsonArray array;
        try { array = JsonNode.Parse(normalizedClassesJson, documentOptions: new() { MaxDepth = 64 }) as JsonArray ?? throw new ArgumentException("The normalized class catalog must be an array."); }
        catch (JsonException ex) { throw new ArgumentException("The normalized class catalog contains invalid JSON.", ex); }
        return new([.. array.Select(item => CatalogClass.From(item as JsonObject ?? throw new ArgumentException("Each class record must be an object.")))]);
    }
    /// <summary>A catalog over classes already read, such as a rules snapshot's.</summary>
    public static ClassProgressionCatalog From(IReadOnlyList<CatalogClass> records) => new(records);

    internal CatalogClass Find(string className)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        return entries.TryGetValue(className, out var entry) ? entry : throw new ArgumentException("That class is not in the normalized catalog.", nameof(className));
    }
}

public static class ClassProgressionRules
{
    public static ClassProgressionSnapshot AtLevel(ClassProgressionCatalog catalog, string className, int level, string subclassName = "")
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (level is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(level), "Only character levels 1–20 are supported.");
        var data = catalog.Find(className);
        var subclass = string.IsNullOrWhiteSpace(subclassName) ? null : data.Subclass(subclassName) ?? throw new ArgumentException("That subclass is not in the selected class.", nameof(subclassName));
        var firstSubclassLevel = data.Progression.Where(pair => pair.Value.Features.Any(feature => feature.Type == "subclass_choice")).Select(pair => pair.Key).DefaultIfEmpty(1).Min();
        var activeSubclass = level >= firstSubclassLevel ? subclass : null;
        var entry = data.Progression.GetValueOrDefault(level);
        var resources = data.Level1Rules.With(entry?.Resources ?? ClassResources.None);
        if (entry is { SpellSlots.Count: > 0 }) resources = resources with { SpellSlots = entry.SpellSlots };
        IReadOnlyList<ProgressionFeature> choices = data.Progression.Count > 0 ? entry?.Features ?? []
            : [.. Features(data.LevelFeatures, data.Level1Features, level, false).Select(name => new ProgressionFeature { Id = Slug(className) + "_" + level + "_" + Slug(name), Type = "automatic_feature", Name = name, Source = className, Level = level })];
        return new()
        {
            ClassName = data.Name, Source = data.Source, Level = level, HitDie = data.HitDie,
            SubclassName = subclass?.DisplayName ?? string.Empty,
            FeaturesAtLevel = Features(data.LevelFeatures, data.Level1Features, level, false).AsReadOnly(), FeaturesUpToLevel = Features(data.LevelFeatures, data.Level1Features, level, true).AsReadOnly(),
            SubclassFeaturesAtLevel = activeSubclass is null ? [] : Features(activeSubclass.LevelFeatures, [], level, false).AsReadOnly(),
            SubclassFeaturesUpToLevel = activeSubclass is null ? [] : Features(activeSubclass.LevelFeatures, [], level, true).AsReadOnly(),
            ChoicesAtLevel = choices, Resources = resources, Spellcasting = ReadSpellcasting(data, activeSubclass, entry, resources, level),
            SubclassGrants = ReadSubclassGrants(activeSubclass, level)
        };
    }

    private static List<string> Features(IReadOnlyDictionary<int, IReadOnlyList<string>> levels, IReadOnlyList<string> level1, int level, bool cumulative)
    {
        var result = new List<string>();
        for (var current = cumulative ? 1 : level; current <= level; current++)
        {
            var names = levels.Count > 0 ? levels.GetValueOrDefault(current) ?? [] : current == 1 ? level1 : [];
            foreach (var name in names) if (!result.Contains(name, StringComparer.Ordinal)) result.Add(name);
        }
        return result;
    }

    private static SpellcastingProfile? ReadSpellcasting(CatalogClass data, CatalogSubclass? subclass, ClassLevelEntry? entry, ClassResources resources, int level)
    {
        CatalogSpellcasting? profile;
        var classCasting = data.Spellcasting;
        if (classCasting is not null)
        {
            profile = classCasting;
            var cantrips = entry?.CantripsKnown ?? 0;
            var prepared = entry?.PreparedSpells ?? 0;
            var known = entry?.KnownSpells ?? 0;
            if (cantrips > 0) profile = profile with { CantripsKnown = cantrips };
            if (data.Name == "Wizard" && known > 0)
            {
                profile = profile with { SpellbookSpells = known, KnownSpells = null };
                if (prepared > 0) profile = profile with { PreparedSpells = prepared };
            }
            else if (prepared > 0) profile = profile with { PreparedSpells = prepared, KnownSpells = null };
            else if (known > 0) profile = profile with { KnownSpells = known, PreparedSpells = null };
            var slots = entry is { SpellSlots.Count: > 0 } ? entry.SpellSlots : resources.SpellSlots;
            if (slots.Count > 0) profile = profile with { SpellSlots = slots };
        }
        else if (subclass is { SpellcastingProgression.Count: > 0 })
        {
            var key = subclass.SpellcastingProgression.Keys.Where(x => x <= level).DefaultIfEmpty(0).Max();
            if (key == 0) return null; // A static fallback must not enable casting before its first explicit threshold.
            profile = subclass.SpellcastingProgression[key];
        }
        else profile = subclass?.Spellcasting;
        if (profile is null) return null;
        var slotsByLevel = new SortedDictionary<int, int>(profile.SpellSlots.ToDictionary());
        var type = classCasting is not null && resources.Count("pact_slot_level") > 0 ? "pact" : profile.Type.Length > 0 ? profile.Type : "standard";
        var active = slotsByLevel.Where(x => x.Value > 0).Select(x => x.Key).ToList();
        var available = type == "pact" && active.Count > 0 ? Enumerable.Range(1, active.Max()).ToList() : active;
        var preparedLimit = profile.PreparedSpells ?? 0;
        var knownLimit = profile.KnownSpells ?? 0;
        var bookLimit = profile.SpellbookSpells ?? 0;
        var cantripLimit = profile.CantripsKnown ?? 0;
        if (new[] { preparedLimit, knownLimit, bookLimit, cantripLimit }.Any(x => x is < 0 or > 5000)) throw new ArgumentException("A class spell quota is outside its supported bounds.");
        return new()
        {
            Ability = profile.Ability, SpellList = profile.SpellList, CastingType = type,
            Slots = new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(slotsByLevel), AvailableSpellLevels = available.AsReadOnly(),
            PreparedLimit = preparedLimit, KnownLimit = knownLimit, SpellbookLimit = bookLimit, CantripLimit = cantripLimit,
            GrantedCantrips = profile.GrantedCantrips
        };
    }

    private static SubclassGrantSummary ReadSubclassGrants(CatalogSubclass? subclass, int level)
    {
        if (subclass is null) return new();
        if (subclass.GrantsByLevel.Count == 0) return new()
        {
            AlwaysPreparedSpells = subclass.AlwaysPreparedSpells, AlwaysKnownSpells = subclass.AlwaysKnownSpells, GrantedCantrips = subclass.GrantedCantrips, Resources = subclass.Resources
        };
        var prepared = new List<string>(); var known = new List<string>(); var cantrips = new List<string>();
        var resources = ClassResources.None; var records = new List<CatalogSpellGrant>(); var recordKeys = new HashSet<(string, int, string, string)>();
        foreach (var pair in subclass.GrantsByLevel.Where(x => x.Key <= level))
        {
            var grant = pair.Value;
            Merge(prepared, grant.AlwaysPreparedSpells);
            Merge(known, grant.AlwaysKnownSpells);
            Merge(cantrips, grant.GrantedCantrips);
            resources = resources.With(grant.Resources);
            foreach (var record in grant.SpellGrants) if (recordKeys.Add((record.Name, record.LevelGained, record.GrantType, record.SubclassId))) records.Add(record);
        }
        return new() { AlwaysPreparedSpells = prepared.AsReadOnly(), AlwaysKnownSpells = known.AsReadOnly(), GrantedCantrips = cantrips.AsReadOnly(), Resources = resources, SpellGrants = records.AsReadOnly() };
    }

    private static void Merge(List<string> into, IReadOnlyList<string> from)
    {
        foreach (var name in from) if (!into.Contains(name, StringComparer.Ordinal)) into.Add(name);
    }

    private static string Slug(string text) => string.Concat(text.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_')).Trim('_');
}
