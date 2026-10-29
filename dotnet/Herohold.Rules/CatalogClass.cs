using System.Globalization;
using System.Text.Json.Nodes;
using Herohold.Core;
namespace Herohold.Rules;

/// <summary>A class as the rules catalog describes it, read once when the catalog loads.</summary>
public sealed record CatalogClass
{
    public string Name { get; init; } = "";
    public string Source { get; init; } = "";
    public int HitDie { get; init; }
    public IReadOnlyList<string> PrimaryAbility { get; init; } = [];
    public IReadOnlyList<string> SavingThrowProficiencies { get; init; } = [];
    public CatalogPick SkillChoices { get; init; } = new(0, []);
    public CatalogPick ToolChoices { get; init; } = new(0, []);
    public IReadOnlyList<string> ArmorProficiencies { get; init; } = [];
    public IReadOnlyList<string> WeaponProficiencies { get; init; } = [];
    public IReadOnlyList<string> ToolProficiencies { get; init; } = [];
    public IReadOnlyList<string> Level1Features { get; init; } = [];
    /// <summary>Feature names gained at each class level.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<string>> LevelFeatures { get; init; } = new Dictionary<int, IReadOnlyList<string>>();
    public IReadOnlyDictionary<string, string> FeatureDescriptions { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, IReadOnlyList<CatalogRelatedFeature>> FeatureRelatedFeatures { get; init; } = new Dictionary<string, IReadOnlyList<CatalogRelatedFeature>>();
    public IReadOnlyList<string> ImportWarnings { get; init; } = [];
    public IReadOnlyDictionary<int, ClassLevelEntry> Progression { get; init; } = new Dictionary<int, ClassLevelEntry>();
    public IReadOnlyList<CatalogSubclass> Subclasses { get; init; } = [];
    public ClassResources Level1Rules { get; init; } = ClassResources.None;
    public ClassOptionCatalog Options { get; init; } = new();
    public CatalogSpellcasting? Spellcasting { get; init; }
    public IReadOnlyList<string> StartingEquipment { get; init; } = [];
    public IReadOnlyList<EquipmentPackage> EquipmentPackages { get; init; } = [];
    public string RawRef { get; init; } = "";

    /// <summary>A subclass by its name or short name, ignoring case.</summary>
    public CatalogSubclass? Subclass(string name) => string.IsNullOrWhiteSpace(name) ? null
        : Subclasses.FirstOrDefault(s => name.Equals(s.Name, StringComparison.OrdinalIgnoreCase) || name.Equals(s.ShortName, StringComparison.OrdinalIgnoreCase));
    /// <summary>A subclass by its exact name.</summary>
    public CatalogSubclass? SubclassNamed(string name) => Subclasses.FirstOrDefault(s => s.Name == name);

    public static CatalogClass From(JsonObject c) => new()
    {
        Name = CatalogText.Text(c["name"]), Source = CatalogText.Text(c["source"]), HitDie = CatalogText.Integer(c["hit_die"]) ?? 0,
        PrimaryAbility = CatalogText.Strings(c["primary_ability"]), SavingThrowProficiencies = CatalogText.Strings(c["saving_throw_proficiencies"]),
        SkillChoices = CatalogText.Pick(c["skill_choices"]), ToolChoices = CatalogText.Pick(c["tool_choices"]),
        ArmorProficiencies = CatalogText.Strings(c["armor_proficiencies"]), WeaponProficiencies = CatalogText.Strings(c["weapon_proficiencies"]), ToolProficiencies = CatalogText.Strings(c["tool_proficiencies"]),
        Level1Features = CatalogText.Strings(c["level_1_features"]), LevelFeatures = CatalogText.Levels(c["level_features"], CatalogText.Strings),
        FeatureDescriptions = CatalogText.Map(c["feature_descriptions"]), FeatureRelatedFeatures = CatalogRelatedFeature.Map(c["feature_related_features"]),
        ImportWarnings = CatalogText.Strings(c["import_warnings"]), Progression = CatalogText.Levels(c["progression"], ClassLevelEntry.From),
        Subclasses = [.. CatalogText.Records(c["subclasses"], "Each subclass record must be an object.").Select(CatalogSubclass.From)],
        Level1Rules = ClassResources.From(c["level_1_rules"]), Options = ClassOptionCatalog.From(c["class_options"]),
        Spellcasting = CatalogSpellcasting.From(c["spellcasting"]), StartingEquipment = CatalogText.Strings(c["starting_equipment"]),
        EquipmentPackages = [.. CatalogText.Records(c["equipment_packages"], "Each equipment package must be an object.").Select(p => new EquipmentPackage(CatalogText.Text(p["name"]), CatalogText.Strings(p["items"])))],
        RawRef = CatalogText.Text(c["raw_ref"])
    };
}

/// <summary>A subclass as the rules catalog describes it.</summary>
public sealed record CatalogSubclass
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string ShortName { get; init; } = "";
    public string Source { get; init; } = "";
    public IReadOnlyDictionary<int, IReadOnlyList<string>> LevelFeatures { get; init; } = new Dictionary<int, IReadOnlyList<string>>();
    public IReadOnlyDictionary<string, string> FeatureDescriptions { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, IReadOnlyList<CatalogRelatedFeature>> FeatureRelatedFeatures { get; init; } = new Dictionary<string, IReadOnlyList<CatalogRelatedFeature>>();
    public IReadOnlyList<string> ImportWarnings { get; init; } = [];
    public SubclassOptionCatalog Options { get; init; } = new();
    /// <summary>Spellcasting that starts or changes at a subclass level (for example Eldritch Knight).</summary>
    public IReadOnlyDictionary<int, CatalogSpellcasting> SpellcastingProgression { get; init; } = new Dictionary<int, CatalogSpellcasting>();
    public IReadOnlyDictionary<int, SubclassLevelGrant> GrantsByLevel { get; init; } = new Dictionary<int, SubclassLevelGrant>();
    public CatalogSpellcasting? Spellcasting { get; init; }
    public IReadOnlyList<string> AlwaysPreparedSpells { get; init; } = [];
    public IReadOnlyList<string> AlwaysKnownSpells { get; init; } = [];
    public IReadOnlyList<string> GrantedCantrips { get; init; } = [];
    public ClassResources Resources { get; init; } = ClassResources.None;

    /// <summary>The name a subclass is shown by: its name, else its short name.</summary>
    public string DisplayName => Name.Length > 0 ? Name : ShortName;

    public static CatalogSubclass From(JsonObject s) => new()
    {
        Id = CatalogText.Text(s["id"]), Name = CatalogText.Text(s["name"]), ShortName = CatalogText.Text(s["short_name"]), Source = CatalogText.Text(s["source"]),
        LevelFeatures = CatalogText.Levels(s["level_features"], CatalogText.Strings), FeatureDescriptions = CatalogText.Map(s["feature_descriptions"]),
        FeatureRelatedFeatures = CatalogRelatedFeature.Map(s["feature_related_features"]), ImportWarnings = CatalogText.Strings(s["import_warnings"]),
        Options = SubclassOptionCatalog.From(s["subclass_options"]),
        SpellcastingProgression = CatalogText.Levels(s["spellcasting_progression"], n => CatalogSpellcasting.From(n) ?? throw new ArgumentException("A subclass spellcasting threshold must describe spellcasting.")),
        GrantsByLevel = CatalogText.Levels(s["grants_by_level"], SubclassLevelGrant.From), Spellcasting = CatalogSpellcasting.From(s["spellcasting"]),
        AlwaysPreparedSpells = CatalogText.Strings(s["always_prepared_spells"]), AlwaysKnownSpells = CatalogText.Strings(s["always_known_spells"]),
        GrantedCantrips = CatalogText.Strings(s["granted_cantrips"]), Resources = ClassResources.From(s["resources"])
    };
}

/// <summary>One class level of the progression table: the features it lists and the values that change at it.</summary>
public sealed record ClassLevelEntry
{
    public IReadOnlyList<ProgressionFeature> Features { get; init; } = [];
    public ClassResources Resources { get; init; } = ClassResources.None;
    public IReadOnlyDictionary<int, int> SpellSlots { get; init; } = new Dictionary<int, int>();
    public int? CantripsKnown { get; init; }
    public int? PreparedSpells { get; init; }
    public int? KnownSpells { get; init; }
    public int? SpellbookSpellsAdded { get; init; }

    public static ClassLevelEntry From(JsonNode? n) => n switch
    {
        JsonArray features => new() { Features = ProgressionFeature.List(features) },
        JsonObject e => new()
        {
            Features = ProgressionFeature.List(e["features"]), Resources = ClassResources.From(e["resources"]), SpellSlots = CatalogText.Slots(e["spell_slots"]),
            CantripsKnown = CatalogText.Integer(e["cantrips_known"]), PreparedSpells = CatalogText.Integer(e["prepared_spells"]), KnownSpells = CatalogText.Integer(e["known_spells"]),
            SpellbookSpellsAdded = CatalogText.Integer(e["spellbook_spells_added"])
        },
        _ => throw new ArgumentException("A class progression level must be an object or a feature list.")
    };
}

/// <summary>A feature or choice the progression table lists at a level: an automatic feature, a subclass choice or an Ability Score Improvement.</summary>
public sealed record ProgressionFeature
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "";
    public string Name { get; init; } = "";
    public string Source { get; init; } = "";
    public int Level { get; init; }
    public bool Required { get; init; }
    public IReadOnlyList<string> Options { get; init; } = [];

    internal static IReadOnlyList<ProgressionFeature> List(JsonNode? n) => [.. CatalogText.Records(n, "Each progression feature must be an object.").Select(f => new ProgressionFeature
    {
        Id = CatalogText.Text(f["id"]), Type = CatalogText.Text(f["type"]), Name = CatalogText.Text(f["name"]), Source = CatalogText.Text(f["source"]),
        Level = CatalogText.Integer(f["level"]) ?? 0, Required = CatalogText.Flag(f["required"]), Options = CatalogText.Strings(f["options"])
    })];
}

/// <summary>A class's counted resources (Rages, Channel Divinity…), dice (Martial Arts, Sneak Attack…) and spell slots by slot level.</summary>
public sealed record ClassResources
{
    public static readonly ClassResources None = new();
    public IReadOnlyDictionary<string, int> Counts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, string> Dice { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<int, int> SpellSlots { get; init; } = new Dictionary<int, int>();
    public int Count(string key) => Counts.GetValueOrDefault(key);

    /// <summary>These resources with another table's values laid over them.</summary>
    public ClassResources With(ClassResources over) => new()
    {
        Counts = Merge(Counts, over.Counts), Dice = Merge(Dice, over.Dice), SpellSlots = over.SpellSlots.Count > 0 ? over.SpellSlots : SpellSlots
    };
    private static Dictionary<TKey, TValue> Merge<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> under, IReadOnlyDictionary<TKey, TValue> over) where TKey : notnull
    {
        var merged = new Dictionary<TKey, TValue>(under); foreach (var pair in over) merged[pair.Key] = pair.Value; return merged;
    }

    public static ClassResources From(JsonNode? n)
    {
        if (n is null) return None;
        if (n is not JsonObject table) throw new ArgumentException("A class resource table must be an object.");
        var counts = new Dictionary<string, int>(StringComparer.Ordinal); var dice = new Dictionary<string, string>(StringComparer.Ordinal); IReadOnlyDictionary<int, int> slots = new Dictionary<int, int>();
        foreach (var pair in table)
        {
            if (pair.Key == "spell_slots") slots = CatalogText.Slots(pair.Value);
            else if (pair.Value is JsonValue v && v.TryGetValue<int>(out var count)) counts[pair.Key] = count;
            else if (pair.Value is JsonValue t && t.TryGetValue<string>(out var die)) dice[pair.Key] = die;
            else throw new ArgumentException("A class resource value must be a whole number or text.");
        }
        return new() { Counts = counts, Dice = dice, SpellSlots = slots };
    }
}

/// <summary>A spellcasting profile: a class's own, a subclass's, or one threshold of a subclass's progression.</summary>
public sealed record CatalogSpellcasting
{
    public Ability Ability { get; init; }
    public string SpellList { get; init; } = "";
    public string Type { get; init; } = "";
    public int? CantripsKnown { get; init; }
    public int? KnownSpells { get; init; }
    public int? PreparedSpells { get; init; }
    public int? SpellbookSpells { get; init; }
    public int? MaximumSpellLevel { get; init; }
    public IReadOnlyList<string> GrantedCantrips { get; init; } = [];
    public IReadOnlyDictionary<int, int> SpellSlots { get; init; } = new Dictionary<int, int>();

    /// <summary>The profile, or null when the catalog has none (null or an empty object).</summary>
    public static CatalogSpellcasting? From(JsonNode? n)
    {
        if (n is null) return null;
        if (n is not JsonObject p) throw new ArgumentException("A spellcasting profile must be an object.");
        if (p.Count == 0) return null;
        if (!Enum.TryParse<Ability>(CatalogText.Text(p["ability"]), ignoreCase: true, out var ability) || !Enum.IsDefined(ability)) throw new ArgumentException("The spellcasting profile has an unsupported ability.");
        return new()
        {
            Ability = ability, SpellList = CatalogText.Text(p["spell_list"]), Type = CatalogText.Text(p["type"]),
            CantripsKnown = CatalogText.Integer(p["cantrips_known"]), KnownSpells = CatalogText.Integer(p["known_spells"]), PreparedSpells = CatalogText.Integer(p["prepared_spells"]),
            SpellbookSpells = CatalogText.Integer(p["spellbook_spells"]), MaximumSpellLevel = CatalogText.Integer(p["maximum_spell_level"]),
            GrantedCantrips = CatalogText.Strings(p["granted_cantrips"]), SpellSlots = CatalogText.Slots(p["spell_slots"])
        };
    }
}

/// <summary>What a subclass grants from one level on: spells, cantrips, resources and spell grant records.</summary>
public sealed record SubclassLevelGrant
{
    public IReadOnlyList<string> AlwaysPreparedSpells { get; init; } = [];
    public IReadOnlyList<string> AlwaysKnownSpells { get; init; } = [];
    public IReadOnlyList<string> GrantedCantrips { get; init; } = [];
    public ClassResources Resources { get; init; } = ClassResources.None;
    public IReadOnlyList<CatalogSpellGrant> SpellGrants { get; init; } = [];

    public static SubclassLevelGrant From(JsonNode? n) => n is JsonObject g ? new()
    {
        AlwaysPreparedSpells = CatalogText.Strings(g["always_prepared_spells"]), AlwaysKnownSpells = CatalogText.Strings(g["always_known_spells"]),
        GrantedCantrips = CatalogText.Strings(g["granted_cantrips"]), Resources = ClassResources.From(g["resources"]),
        SpellGrants = [.. CatalogText.Records(g["spell_grants"], "A subclass spell grant record is malformed.").Select(CatalogSpellGrant.From)]
    } : throw new ArgumentException("A subclass grant level must be an object.");
}

/// <summary>A spell a subclass grants, with how it counts.</summary>
public sealed record CatalogSpellGrant
{
    public string Name { get; init; } = "";
    public int LevelGained { get; init; }
    public string GrantType { get; init; } = "";
    public bool AlwaysPrepared { get; init; }
    public bool CountsAgainstLimit { get; init; }
    public string SpellcastingAbility { get; init; } = "";
    public string SourceType { get; init; } = "";
    public string SourceBook { get; init; } = "";
    public string SubclassId { get; init; } = "";
    public string SubclassName { get; init; } = "";

    public static CatalogSpellGrant From(JsonObject g)
    {
        var source = g["source"] is null ? new JsonObject() : g["source"] as JsonObject ?? throw new ArgumentException("A subclass spell grant source must be an object.");
        return new()
        {
            Name = CatalogText.Text(g["name"]), LevelGained = CatalogText.Integer(g["level_gained"]) ?? 0, GrantType = CatalogText.Text(g["grant_type"]),
            AlwaysPrepared = CatalogText.Flag(g["always_prepared"]), CountsAgainstLimit = CatalogText.Flag(g["counts_against_limit"]), SpellcastingAbility = CatalogText.Text(g["spellcasting_ability"]),
            SourceType = CatalogText.Text(source["type"]), SourceBook = CatalogText.Text(source["source"]), SubclassId = CatalogText.Text(source["subclass_id"]), SubclassName = CatalogText.Text(source["subclass_name"])
        };
    }
}

/// <summary>A feature listed beside another (for example the Battle Master maneuvers under Combat Superiority).</summary>
public sealed record CatalogRelatedFeature
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int? Level { get; init; }
    public string Source { get; init; } = "";
    public string Cost { get; init; } = "";
    public string Description { get; init; } = "";

    internal static IReadOnlyDictionary<string, IReadOnlyList<CatalogRelatedFeature>> Map(JsonNode? n)
    {
        if (n is null) return new Dictionary<string, IReadOnlyList<CatalogRelatedFeature>>();
        if (n is not JsonObject map) throw new ArgumentException("Related features must be grouped by feature.");
        return map.ToDictionary(p => p.Key, p => (IReadOnlyList<CatalogRelatedFeature>)[.. CatalogText.Records(p.Value, "A related feature must be an object.").Select(r => new CatalogRelatedFeature
        {
            Id = CatalogText.Text(r["id"]), Name = CatalogText.Text(r["name"]), Level = CatalogText.Integer(r["level"]), Source = CatalogText.Text(r["source"]),
            Cost = CatalogText.Text(r["cost"]), Description = CatalogText.Text(r["description"])
        })], StringComparer.Ordinal);
    }
}

/// <summary>A pick from a list, optionally with full records for each option (invocations, maneuvers). <see cref="FromReference"/> names another list when the catalog gives one instead of options.</summary>
public sealed record CatalogOptionPick
{
    public int Choose { get; init; }
    public IReadOnlyList<string> From { get; init; } = [];
    public string FromReference { get; init; } = "";
    public IReadOnlyList<CatalogOptionRecord> Options { get; init; } = [];

    internal static CatalogOptionPick? Read(JsonNode? n)
    {
        if (n is null) return null;
        if (n is not JsonObject o) throw new ArgumentException("A class option pick must be an object.");
        var from = o["from"];
        return new()
        {
            Choose = CatalogText.Integer(o["choose"]) ?? 0, From = from is JsonArray ? CatalogText.Strings(from) : [], FromReference = from is JsonArray ? "" : CatalogText.Text(from),
            Options = [.. CatalogText.Records(o["options"], "A class option record must be an object.").Select(CatalogOptionRecord.From)]
        };
    }
}

/// <summary>One option of a pick, with its prerequisites.</summary>
public sealed record CatalogOptionRecord
{
    public string Name { get; init; } = "";
    public string Source { get; init; } = "";
    public int? Page { get; init; }
    public string Description { get; init; } = "";
    public string RawRef { get; init; } = "";
    public IReadOnlyList<string> PrerequisiteLabels { get; init; } = [];
    public int? MinimumWarlockLevel { get; init; }
    public IReadOnlyList<string> RequiredInvocations { get; init; } = [];
    public bool RequiresDamageCantrip { get; init; }
    /// <summary>How far Herohold supports the option, by aspect (selection, sheet grants, active play…).</summary>
    public IReadOnlyDictionary<string, string> Support { get; init; } = new Dictionary<string, string>();

    internal static CatalogOptionRecord From(JsonObject r)
    {
        var prerequisites = r["prerequisites"] is null ? new JsonObject() : r["prerequisites"] as JsonObject ?? throw new ArgumentException("Option prerequisites must be an object.");
        return new()
        {
            Name = CatalogText.Text(r["name"]), Source = CatalogText.Text(r["source"]), Page = CatalogText.Integer(r["page"]), Description = CatalogText.Text(r["description"]), RawRef = CatalogText.Text(r["raw_ref"]),
            PrerequisiteLabels = CatalogText.Strings(r["prerequisite_labels"]), MinimumWarlockLevel = CatalogText.Integer(prerequisites["minimum_warlock_level"]),
            RequiredInvocations = CatalogText.Strings(prerequisites["required_invocations"]), RequiresDamageCantrip = CatalogText.Flag(prerequisites["requires_damage_cantrip"]),
            Support = CatalogText.Map(r["support"])
        };
    }
}

/// <summary>The class-level option lists the catalog defines.</summary>
public sealed record ClassOptionCatalog
{
    public IReadOnlyList<string> FightingStyle { get; init; } = [];
    public IReadOnlyList<string> DivineOrder { get; init; } = [];
    public IReadOnlyList<string> PrimalOrder { get; init; } = [];
    public CatalogOptionPick? EldritchInvocations { get; init; }
    public CatalogOptionPick? Expertise { get; init; }

    private static readonly HashSet<string> Known = ["fighting_style", "divine_order", "primal_order", "eldritch_invocations", "expertise"];
    public static ClassOptionCatalog From(JsonNode? n)
    {
        if (n is null) return new();
        if (n is not JsonObject o) throw new ArgumentException("Class options must be an object.");
        if (o.Select(p => p.Key).FirstOrDefault(k => !Known.Contains(k)) is { } unknown) throw new ArgumentException("The class option '" + unknown + "' is not supported.");
        return new()
        {
            FightingStyle = CatalogText.Strings(o["fighting_style"]), DivineOrder = CatalogText.Strings(o["divine_order"]), PrimalOrder = CatalogText.Strings(o["primal_order"]),
            EldritchInvocations = CatalogOptionPick.Read(o["eldritch_invocations"]), Expertise = CatalogOptionPick.Read(o["expertise"])
        };
    }
}

/// <summary>The subclass-level option lists the catalog defines.</summary>
public sealed record SubclassOptionCatalog
{
    public CatalogOptionPick? LoreSkills { get; init; }
    public CatalogOptionPick? Maneuvers { get; init; }
    public IReadOnlyList<string>? ManeuverSaveAbility { get; init; }
    public CatalogOptionPick? StudentOfWarSkill { get; init; }
    public CatalogOptionPick? StudentOfWarTool { get; init; }
    public CatalogOptionPick? HuntersPrey { get; init; }
    public CatalogOptionPick? PrimalCompanion { get; init; }
    public StructuredSpellChoice? StructuredSpellChoice { get; init; }
    public SavantSpellRule? SavantSpells { get; init; }
    /// <summary>The savant spell choice at each Wizard level, when the catalog sets one per level.</summary>
    public IReadOnlyDictionary<int, SavantSpellRule> SavantSpellChoicesByLevel { get; init; } = new Dictionary<int, SavantSpellRule>();

    private static readonly HashSet<string> Known = ["lore_skills", "maneuvers", "maneuver_save_ability", "student_of_war_skill", "student_of_war_tool", "hunters_prey", "primal_companion", "structured_spell_choice", "savant_spells", "savant_spell_choices_by_level"];
    public static SubclassOptionCatalog From(JsonNode? n)
    {
        if (n is null) return new();
        if (n is not JsonObject o) throw new ArgumentException("Subclass options must be an object.");
        if (o.Select(p => p.Key).FirstOrDefault(k => !Known.Contains(k)) is { } unknown) throw new ArgumentException("The subclass option '" + unknown + "' is not supported.");
        return new()
        {
            LoreSkills = CatalogOptionPick.Read(o["lore_skills"]), Maneuvers = CatalogOptionPick.Read(o["maneuvers"]),
            ManeuverSaveAbility = o["maneuver_save_ability"] is null ? null : CatalogText.Strings(o["maneuver_save_ability"]),
            StudentOfWarSkill = CatalogOptionPick.Read(o["student_of_war_skill"]), StudentOfWarTool = CatalogOptionPick.Read(o["student_of_war_tool"]),
            HuntersPrey = CatalogOptionPick.Read(o["hunters_prey"]), PrimalCompanion = CatalogOptionPick.Read(o["primal_companion"]),
            StructuredSpellChoice = Herohold.Rules.StructuredSpellChoice.From(o["structured_spell_choice"]),
            SavantSpells = o["savant_spells"] is null ? null : SavantSpellRule.From(o["savant_spells"]),
            SavantSpellChoicesByLevel = CatalogText.Levels(o["savant_spell_choices_by_level"], SavantSpellRule.From)
        };
    }
}

/// <summary>A choice between named groups of granted spells (for example a Circle of the Land terrain).</summary>
public sealed record StructuredSpellChoice
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public int Choose { get; init; }
    public bool Required { get; init; }
    public IReadOnlyList<StructuredSpellOption> Options { get; init; } = [];

    internal static StructuredSpellChoice? From(JsonNode? n)
    {
        if (n is null) return null;
        if (n is not JsonObject o) throw new ArgumentException("A structured spell choice must be an object.");
        return new()
        {
            Id = CatalogText.Text(o["id"]), Name = CatalogText.Text(o["name"]), Type = CatalogText.Text(o["type"]), Choose = CatalogText.Integer(o["choose"]) ?? 0, Required = CatalogText.Flag(o["required"]),
            Options = [.. CatalogText.Records(o["options"], "A structured spell option must be an object.").Select(r => new StructuredSpellOption
            {
                Id = CatalogText.Text(r["id"]), Name = CatalogText.Text(r["name"]), GrantedSpells = CatalogText.Strings(r["granted_spells"]),
                GrantedSpellsByLevel = CatalogText.Levels(r["granted_spells_by_level"], CatalogText.Strings)
            })]
        };
    }
}
public sealed record StructuredSpellOption
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public IReadOnlyList<string> GrantedSpells { get; init; } = [];
    public IReadOnlyDictionary<int, IReadOnlyList<string>> GrantedSpellsByLevel { get; init; } = new Dictionary<int, IReadOnlyList<string>>();
}

/// <summary>A Wizard savant's free spellbook spells: how many, from which school, up to which spell level.</summary>
public sealed record SavantSpellRule
{
    public int? Choose { get; init; }
    public string Label { get; init; } = "";
    public string SpellClass { get; init; } = "";
    public IReadOnlyList<int> SpellLevels { get; init; } = [];
    public string School { get; init; } = "";
    public int? MaximumSpellLevel { get; init; }

    internal static SavantSpellRule From(JsonNode? n)
    {
        if (n is not JsonObject o) throw new ArgumentException("A savant spell rule must be an object.");
        var from = o["from"] is null ? new JsonObject() : o["from"] as JsonObject ?? throw new ArgumentException("A savant spell query must be an object.");
        return new()
        {
            Choose = CatalogText.Integer(o["choose"]), Label = CatalogText.Text(o["label"]), SpellClass = CatalogText.Text(from["class"]),
            SpellLevels = [.. (from["levels"] is null ? [] : from["levels"] as JsonArray ?? throw new ArgumentException("Savant spell levels must be a list.")).Select(l => CatalogText.Integer(l) ?? 0)],
            School = CatalogText.Text(from["school"]), MaximumSpellLevel = CatalogText.Integer(o["maximum_spell_level"])
        };
    }
}
