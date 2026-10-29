using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Herohold.Core;

/// <summary>
/// Everything a player owns about one character: build choices and play facts. Every other value
/// (scores, proficiencies, AC, attacks, spells, slots, descriptions) is recalculated by the rules
/// when the character is loaded, so it is never stored.
/// </summary>
public sealed record CharacterRecord
{
    public const string DefaultOwnerId = "owner";
    public string Id { get; init; } = string.Empty;
    public string OwnerId { get; init; } = DefaultOwnerId;
    public long Revision { get; init; }
    public BuildFacts Build { get; init; } = new();
    public CharacterFacts Facts { get; init; } = new();
    /// <summary>Rejects an id that is not a safe character id.</summary>
    public static void ValidateId(string id) => WorkspaceValidation.Id(id);
}

/// <summary>Play facts that only the table can know. Derived resource and Hit Dice values keep only what was spent.</summary>
public sealed record CharacterFacts
{
    public int CurrentHp { get; init; }
    public int TemporaryHp { get; init; }
    public int Turn { get; init; } = 1;
    public int Exhaustion { get; init; }
    public List<EquipmentItem> Equipment { get; init; } = [];
    public List<JournalNote> Notes { get; init; } = [];
    public List<ActiveEffect> Effects { get; init; } = [];
    public CombatState Combat { get; init; } = new();
    public CharacterSheetState Sheet { get; init; } = new();
    public KnownTargetState KnownTarget { get; init; } = new();
    /// <summary>Defenses the player added; build defenses are recalculated.</summary>
    public List<DamageDefense> Defenses { get; init; } = [];
    /// <summary>Manual pools in full. For rules-owned pools only the id, kind, maximum and remaining uses: the rules keep what was spent when the maximum changes.</summary>
    public List<ResourcePool> Resources { get; init; } = [];
    /// <summary>Only the id, maximum and remaining dice; sizes and new maximums come from the class levels.</summary>
    public List<HitDiePool> HitDiePools { get; init; } = [];
    public Dictionary<string, string> SpellNotes { get; init; } = new(StringComparer.Ordinal);
    public bool DreadUsedThisTurn { get; init; }
    public string HunterTarget { get; init; } = string.Empty;
    public RollOutcome? LastRoll { get; init; }
    public CastOutcome? LastCast { get; init; }
    public List<string> CompletedOperationIds { get; init; } = [];
    public List<PlayUndoEntry> UndoHistory { get; init; } = [];
}

/// <summary>A catalog-defined choice: a name, a list of names, a number, a flag, or named parts of those.</summary>
[JsonConverter(typeof(ChoiceValueConverter))]
public sealed record ChoiceValue
{
    public string? Text { get; init; }
    public int? Number { get; init; }
    public bool? Flag { get; init; }
    public List<ChoiceValue>? Items { get; init; }
    public Dictionary<string, ChoiceValue>? Fields { get; init; }
    public bool IsNull => Text is null && Number is null && Flag is null && Items is null && Fields is null;

    public static ChoiceValue Of(string text) => new() { Text = text };
    public static ChoiceValue Of(int number) => new() { Number = number };
    public static ChoiceValue Of(bool flag) => new() { Flag = flag };
    public static ChoiceValue ListOf(IEnumerable<ChoiceValue> items) => new() { Items = [.. items] };
    public static ChoiceValue Record(IEnumerable<KeyValuePair<string, ChoiceValue>> fields) => new() { Fields = new(fields, StringComparer.Ordinal) };

    /// <summary>An independent copy, sharing no lists or maps with this value.</summary>
    public ChoiceValue Copy() => this with { Items = Items?.Select(item => item.Copy()).ToList(), Fields = Fields?.ToDictionary(p => p.Key, p => p.Value.Copy(), StringComparer.Ordinal) };

    /// <summary>Whether two values hold the same choice; named parts compare regardless of order.</summary>
    public static bool Same(ChoiceValue? a, ChoiceValue? b)
    {
        if (a is null || a.IsNull) return b is null || b.IsNull;
        if (b is null || b.IsNull) return false;
        if (a.Items is not null || b.Items is not null)
            return a.Items is { } x && b.Items is { } y && x.Count == y.Count && x.Zip(y).All(pair => Same(pair.First, pair.Second));
        if (a.Fields is not null || b.Fields is not null)
            return a.Fields is { } x && b.Fields is { } y && x.Count == y.Count && x.All(pair => y.TryGetValue(pair.Key, out var other) && Same(pair.Value, other));
        return a.Text == b.Text && a.Number == b.Number && a.Flag == b.Flag;
    }

    /// <summary>The named part at <paramref name="path"/>, if there is one.</summary>
    public ChoiceValue? At(IReadOnlyList<string> path)
    {
        var current = this;
        foreach (var key in path) if (current.Fields?.GetValueOrDefault(key) is { } next) current = next; else return null;
        return current;
    }

    /// <summary>A copy with the named part at <paramref name="path"/> set, creating parts along the way.</summary>
    public ChoiceValue With(IReadOnlyList<string> path, ChoiceValue value)
    {
        if (path.Count == 0) return value;
        var fields = new Dictionary<string, ChoiceValue>(Fields ?? [], StringComparer.Ordinal);
        fields[path[0]] = (fields.GetValueOrDefault(path[0]) ?? new()).With([.. path.Skip(1)], value);
        return new() { Fields = fields };
    }

    /// <summary>The value as a person reads it: names joined, named parts labelled.</summary>
    public string Display => Text ?? Number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? (Flag is { } f ? (f ? "Yes" : "No") : null)
        ?? (Items is { } items ? string.Join(", ", items.Select(i => i.Display)) : null)
        ?? (Fields is { } named ? string.Join(" · ", named.Select(p => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(p.Key.Replace('_', ' ')) + ": " + p.Value.Display)) : "");
}

public sealed class ChoiceValueConverter : JsonConverter<ChoiceValue>
{
    public override bool HandleNull => true;
    public override ChoiceValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null: return new();
            case JsonTokenType.String: return new() { Text = reader.GetString() };
            case JsonTokenType.True or JsonTokenType.False: return new() { Flag = reader.GetBoolean() };
            case JsonTokenType.Number:
                if (!reader.TryGetInt32(out var number)) throw new JsonException("Choice numbers must be whole numbers.");
                return new() { Number = number };
            case JsonTokenType.StartArray:
                var items = new List<ChoiceValue>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) items.Add(Read(ref reader, typeToConvert, options));
                return new() { Items = items };
            case JsonTokenType.StartObject:
                var fields = new Dictionary<string, ChoiceValue>(StringComparer.Ordinal);
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    var name = reader.GetString()!;
                    reader.Read();
                    fields[name] = Read(ref reader, typeToConvert, options);
                }
                return new() { Fields = fields };
            default: throw new JsonException("Unsupported choice value.");
        }
    }

    public override void Write(Utf8JsonWriter writer, ChoiceValue value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else if (value.Text is not null) writer.WriteStringValue(value.Text);
        else if (value.Number is { } number) writer.WriteNumberValue(number);
        else if (value.Flag is { } flag) writer.WriteBooleanValue(flag);
        else if (value.Items is not null)
        {
            writer.WriteStartArray();
            foreach (var item in value.Items) Write(writer, item, options);
            writer.WriteEndArray();
        }
        else if (value.Fields is not null)
        {
            writer.WriteStartObject();
            foreach (var (name, field) in value.Fields) { writer.WritePropertyName(name); Write(writer, field, options); }
            writer.WriteEndObject();
        }
        else writer.WriteNullValue();
    }
}

public sealed record ClassTrackFact
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("class_name")] public string ClassName { get; init; } = string.Empty;
    [JsonPropertyName("level")] public int Level { get; init; }
    [JsonPropertyName("rules_source")] public string? RulesSource { get; init; }
    [JsonPropertyName("subclass")] public string? Subclass { get; init; }
    [JsonPropertyName("choices")] public Dictionary<string, ChoiceValue> Choices { get; init; } = new(StringComparer.Ordinal);
}

public sealed record BuildSourceFact
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("class_name")] public string? ClassName { get; init; }
    [JsonPropertyName("level")] public int? Level { get; init; }
    [JsonPropertyName("transaction_id")] public string? TransactionId { get; init; }
}

public sealed record FeatFact
{
    [JsonPropertyName("instance_id")] public string InstanceId { get; init; } = string.Empty;
    /// <summary>Old saves may store a null id; it reads as absent.</summary>
    [JsonPropertyName("feat_id")] public string FeatId { get; init => field = value ?? string.Empty; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("repeatable")] public bool? Repeatable { get; init; }
    [JsonPropertyName("source")] public BuildSourceFact Source { get; init; } = new();
    [JsonPropertyName("level_taken")] public int? LevelTaken { get; init; }
    [JsonPropertyName("choices")] public Dictionary<string, ChoiceValue> Choices { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Whether the feat's ability increase is already part of the imported base scores.</summary>
    [JsonPropertyName("folio_ability_increase_in_base")] public bool? AbilityIncreaseInBase { get; init; }
    /// <summary>Choices an earlier version recorded that this feat no longer offers, kept for reference.</summary>
    [JsonPropertyName("folio_legacy_choice_history")] public List<Dictionary<string, ChoiceValue>>? LegacyChoiceHistory { get; init; }
}

public sealed record AbilityScoreAdjustmentFact
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("source_type")] public string SourceType { get; init; } = string.Empty;
    [JsonPropertyName("source_id")] public string? SourceId { get; init; }
    [JsonPropertyName("instance_id")] public string? InstanceId { get; init; }
    [JsonPropertyName("source_feat")] public string? SourceFeat { get; init; }
    [JsonPropertyName("source_name")] public string? SourceName { get; init; }
    [JsonPropertyName("source_class")] public string? SourceClass { get; init; }
    [JsonPropertyName("source_level")] public int? SourceLevel { get; init; }
    [JsonPropertyName("source")] public BuildSourceFact? Source { get; init; }
    [JsonPropertyName("level_up_choice_id")] public string? LevelUpChoiceId { get; init; }
    [JsonPropertyName("feat_instance_id")] public string? FeatInstanceId { get; init; }
    [JsonPropertyName("adjustments")] public Dictionary<Ability, int> Adjustments { get; init; } = [];
    /// <summary>The score cap for this increase; the rules use 20 when it is absent.</summary>
    [JsonPropertyName("maximum")] public int? Maximum { get; init; }
}

public sealed record SpellPicks
{
    [JsonPropertyName("cantrips")] public List<string>? Cantrips { get; init; }
    [JsonPropertyName("known")] public List<string>? Known { get; init; }
    [JsonPropertyName("prepared")] public List<string>? Prepared { get; init; }
    [JsonPropertyName("spellbook")] public List<string>? Spellbook { get; init; }
}

public sealed record PresentationFacts
{
    [JsonPropertyName("primary_weapon")] public string PrimaryWeapon { get; init; } = string.Empty;
    [JsonPropertyName("featured_cantrip")] public string FeaturedCantrip { get; init; } = string.Empty;
    [JsonPropertyName("weapon_wielding_modes")] public Dictionary<string, string> WeaponWieldingModes { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>The player's current pick for a changeable feature. Its options and timing are recalculated.</summary>
public sealed record FeatureConfigurationFact
{
    [JsonPropertyName("feature_id")] public string FeatureId { get; init; } = string.Empty;
    [JsonPropertyName("current_value")] public string CurrentValue { get; init; } = string.Empty;
}

public sealed record HpRollFact
{
    [JsonPropertyName("level")] public int Level { get; init; }
    [JsonPropertyName("value")] public int Value { get; init; }
    /// <summary>How older saves recorded the roll ("rolled", "fixed"); Herohold keeps it as written.</summary>
    [JsonPropertyName("mode")] public string? Mode { get; init; }
}

public sealed record HpGainFact
{
    [JsonPropertyName("character_level")] public int CharacterLevel { get; init; }
    [JsonPropertyName("class_name")] public string ClassName { get; init; } = string.Empty;
    [JsonPropertyName("method")] public string Method { get; init; } = string.Empty;
    [JsonPropertyName("base_gain")] public int BaseGain { get; init; }
}

public sealed record ResolvedChoiceFact
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("value")] public ChoiceValue Value { get; init; } = new();
}

public sealed record LevelUpFact
{
    [JsonPropertyName("class")] public string ClassName { get; init; } = string.Empty;
    [JsonPropertyName("from_level")] public int FromLevel { get; init; }
    [JsonPropertyName("to_level")] public int ToLevel { get; init; }
    [JsonPropertyName("transaction_id")] public string TransactionId { get; init; } = string.Empty;
    [JsonPropertyName("hp_increase")] public int HpIncrease { get; init; }
    [JsonPropertyName("automatic_features")] public List<string> AutomaticFeatures { get; init; } = [];
    [JsonPropertyName("resolved_choices")] public List<ResolvedChoiceFact> ResolvedChoices { get; init; } = [];
    [JsonPropertyName("source")] public string? Source { get; init; }
}

public sealed record ImportOverrideFacts
{
    [JsonPropertyName("maximum_hp_adjustment")] public int MaximumHpAdjustment { get; init; }
}

public sealed record ImportProvenanceFacts
{
    [JsonPropertyName("warnings")] public List<string> Warnings { get; init; } = [];
    [JsonPropertyName("source_kind")] public string? SourceKind { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("imported_at")] public string? ImportedAt { get; init; }
    [JsonPropertyName("ability_history_reconstructed")] public bool? AbilityHistoryReconstructed { get; init; }
    [JsonPropertyName("hp_history_reconstructed")] public bool? HpHistoryReconstructed { get; init; }
}

/// <summary>Proficiencies and defenses the player entered by hand. The rules add the build-derived ones.</summary>
public sealed record ManualBuildFacts
{
    [JsonPropertyName("tool_proficiencies")] public List<string> ToolProficiencies { get; init; } = [];
    [JsonPropertyName("armor_proficiencies")] public List<string> ArmorProficiencies { get; init; } = [];
    [JsonPropertyName("weapon_proficiencies")] public List<string> WeaponProficiencies { get; init; } = [];
    [JsonPropertyName("damage_resistances")] public List<string> DamageResistances { get; init; } = [];
    [JsonPropertyName("damage_immunities")] public List<string> DamageImmunities { get; init; } = [];
    [JsonPropertyName("damage_vulnerabilities")] public List<string> DamageVulnerabilities { get; init; } = [];
    [JsonPropertyName("condition_immunities")] public List<string> ConditionImmunities { get; init; } = [];
}

public sealed record JournalCategoryFacts
{
    [JsonPropertyName("version")] public int Version { get; init; } = 1;
    [JsonPropertyName("categories")] public Dictionary<string, string> Categories { get; init; } = new(StringComparer.Ordinal);
}

public sealed record CustomJournalFacts
{
    [JsonPropertyName("version")] public int Version { get; init; } = 1;
    [JsonPropertyName("categories")] public List<string> Categories { get; init; } = [];
    [JsonPropertyName("notes")] public Dictionary<string, string> Notes { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>
/// The player's build choices. Property names are the rules' character keys, so a build converts to
/// and from the rules input exactly; any key not listed here is recalculated and is not stored.
/// </summary>
public sealed record BuildFacts
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("class")] public string ClassName { get; init; } = string.Empty;
    [JsonPropertyName("subclass")] public string Subclass { get; init; } = string.Empty;
    [JsonPropertyName("level")] public int Level { get; init; } = 1;
    [JsonPropertyName("species")] public string Species { get; init; } = string.Empty;
    [JsonPropertyName("background")] public string Background { get; init; } = string.Empty;
    [JsonPropertyName("class_levels")] public Dictionary<string, int>? ClassLevels { get; init; }
    [JsonPropertyName("class_options")] public Dictionary<string, ChoiceValue>? ClassOptions { get; init; }
    [JsonPropertyName("species_options")] public Dictionary<string, ChoiceValue>? SpeciesOptions { get; init; }
    [JsonPropertyName("background_options")] public Dictionary<string, ChoiceValue>? BackgroundOptions { get; init; }
    [JsonPropertyName("origin_feat_choices")] public Dictionary<string, ChoiceValue>? OriginFeatChoices { get; init; }
    [JsonPropertyName("folio_origin_feat_choices")] public Dictionary<string, ChoiceValue>? HeroholdOriginFeatChoices { get; init; }
    [JsonPropertyName("folio_origin_choice_owners")] public Dictionary<string, ChoiceValue>? OriginChoiceOwners { get; init; }
    [JsonPropertyName("base_ability_scores")] public Dictionary<Ability, int>? BaseAbilityScores { get; init; }
    [JsonPropertyName("ability_score_method")] public string? AbilityScoreMethod { get; init; }
    [JsonPropertyName("ability_score_assignments")] public Dictionary<string, int>? AbilityScoreAssignments { get; init; }
    [JsonPropertyName("rolled_ability_scores")] public List<int>? RolledAbilityScores { get; init; }
    [JsonPropertyName("ability_score_adjustments")] public List<AbilityScoreAdjustmentFact>? AbilityScoreAdjustments { get; init; }
    [JsonPropertyName("general_feats")] public List<FeatFact>? Feats { get; init; }
    [JsonPropertyName("origin_languages")] public List<string>? OriginLanguages { get; init; }
    [JsonPropertyName("spells")] public SpellPicks? Spells { get; init; }
    [JsonPropertyName("spellbook_spells")] public List<string>? SpellbookSpells { get; init; }
    [JsonPropertyName("weapon_masteries")] public List<string>? WeaponMasteries { get; init; }
    [JsonPropertyName("equipment_migration_pending")] public bool? EquipmentMigrationPending { get; init; }
    [JsonPropertyName("presentation_preferences")] public PresentationFacts? Presentation { get; init; }
    [JsonPropertyName("feature_configurations")] public List<FeatureConfigurationFact>? FeatureConfigurations { get; init; }
    [JsonPropertyName("hp_rolls")] public List<HpRollFact>? HpRolls { get; init; }
    [JsonPropertyName("hp_history")] public List<HpGainFact>? HpHistory { get; init; }
    [JsonPropertyName("level_history")] public List<LevelUpFact>? LevelHistory { get; init; }
    [JsonPropertyName("completed_level_up_transactions")] public List<string>? CompletedLevelUpTransactions { get; init; }
    [JsonPropertyName("import_overrides")] public ImportOverrideFacts? ImportOverrides { get; init; }
    [JsonPropertyName("import_provenance")] public ImportProvenanceFacts? ImportProvenance { get; init; }
    [JsonPropertyName("pdf_passthrough_fields")] public Dictionary<string, ChoiceValue>? PdfPassthroughFields { get; init; }
    [JsonPropertyName("migration_warnings")] public List<string>? MigrationWarnings { get; init; }
    [JsonPropertyName("folio_class_tracks")] public List<ClassTrackFact>? ClassTracks { get; init; }
    [JsonPropertyName("folio_class_tracks_version")] public int? ClassTracksVersion { get; init; }
    [JsonPropertyName("initial_class_track_id")] public string? InitialClassTrackId { get; init; }
    [JsonPropertyName("folio_journal")] public JournalCategoryFacts? Journal { get; init; }
    [JsonPropertyName("folio_journal_custom")] public CustomJournalFacts? CustomJournal { get; init; }
    [JsonPropertyName("folio_journal_archived")] public List<string>? ArchivedJournalNotes { get; init; }
    [JsonPropertyName("folio_journal_cleanup_v1")] public bool? JournalCleanupV1 { get; init; }
    /// <summary>Sheet values brought in by an import and used while the sheet has no value of its own.</summary>
    [JsonPropertyName("coins")] public ChoiceValue? ImportedCoins { get; init; }
    [JsonPropertyName("currency")] public ChoiceValue? ImportedCurrency { get; init; }
    [JsonPropertyName("experience_points")] public ChoiceValue? ImportedExperiencePoints { get; init; }
    [JsonPropertyName("xp")] public ChoiceValue? ImportedXp { get; init; }
    [JsonPropertyName("heroic_inspiration")] public ChoiceValue? ImportedHeroicInspiration { get; init; }
    [JsonPropertyName("movement_modes")] public Dictionary<string, ChoiceValue>? ImportedMovementModes { get; init; }
    /// <summary>Hand-entered build facts; stored from the <c>manual</c> part of the rules' fact sources.</summary>
    [JsonPropertyName("manual_facts")] public ManualBuildFacts? ManualFacts { get; init; }
    [JsonPropertyName("manual_languages")] public List<string>? ManualLanguages { get; init; }
    [JsonPropertyName("manual_skills")] public List<string>? ManualSkills { get; init; }
    [JsonPropertyName("manual_expertise")] public List<string>? ManualExpertise { get; init; }
    [JsonPropertyName("manual_saves")] public List<string>? ManualSaves { get; init; }
    /// <summary>The manual facts' own names; in a rules character they live in the fact-source sidecars instead.</summary>
    private static readonly string[] ManualKeys = ["manual_facts", "manual_languages", "manual_skills", "manual_expertise", "manual_saves"];

    private static readonly JsonSerializerOptions Json = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>The character keys this record stores. Every other key is recalculated by the rules.</summary>
    public static readonly IReadOnlySet<string> StoredKeys = typeof(BuildFacts).GetProperties()
        .Select(p => p.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false).OfType<JsonPropertyNameAttribute>().FirstOrDefault()?.Name)
        .OfType<string>().Except(ManualKeys).Concat(["folio_build_fact_sources", "folio_language_sources", "folio_native_build_sources"]).ToHashSet(StringComparer.Ordinal);

    /// <summary>Reads the stored keys of a rules character. A stored key in an unexpected shape is rejected, never dropped.</summary>
    public static BuildFacts FromCanonical(JsonObject canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        var stored = new JsonObject();
        foreach (var (key, value) in canonical)
            if (StoredKeys.Contains(key) && key is not ("folio_build_fact_sources" or "folio_language_sources" or "folio_native_build_sources") && value is not null)
                stored[key] = value.DeepClone();
        // A calculated character's feature configurations are full rules records; only the chosen value is the player's.
        if (stored["feature_configurations"] is JsonArray configurations)
            stored["feature_configurations"] = new JsonArray([.. configurations.Select(c => c is JsonObject record
                ? new JsonObject(record.Where(p => p.Key is "feature_id" or "current_value").Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())))
                : c?.DeepClone())]);
        BuildFacts facts;
        try { facts = stored.Deserialize<BuildFacts>(Json) ?? new(); }
        catch (JsonException error) { throw new ArgumentException("The character build has a value Herohold cannot store: " + error.Message, error); }
        var manual = canonical["folio_build_fact_sources"]?["manual"];
        var native = canonical["folio_native_build_sources"];
        try
        {
            return facts with
            {
                BaseAbilityScores = facts.BaseAbilityScores is { } scores ? Enum.GetValues<Ability>().Where(scores.ContainsKey).ToDictionary(a => a, a => scores[a]) : null,
                ManualFacts = manual?.Deserialize<ManualBuildFacts>(Json),
                ManualLanguages = canonical["folio_language_sources"]?["manual"]?.Deserialize<List<string>>(Json),
                ManualSkills = native?["skills"]?.Deserialize<List<string>>(Json),
                ManualExpertise = native?["expertise"]?.Deserialize<List<string>>(Json),
                ManualSaves = native?["saves"]?.Deserialize<List<string>>(Json)
            };
        }
        catch (JsonException error) { throw new ArgumentException("The character's hand-entered build facts cannot be stored: " + error.Message, error); }
    }

    /// <summary>
    /// <paramref name="canonical"/> with its build replaced by this one; recalculated values and play sidecars are kept
    /// for the rules to recalculate around.
    /// </summary>
    public JsonObject ReplaceIn(JsonObject canonical, string characterId)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        var document = canonical.DeepClone().AsObject();
        foreach (var key in StoredKeys) document.Remove(key);
        var build = ToCanonical(characterId);
        // The source sidecars hold the hand-entered lists (part of the build) beside what the last
        // calculation derived, which the next calculation reads to tell the two apart.
        foreach (var (key, manual) in SidecarManualKeys)
        {
            if (canonical[key] is not JsonObject previous) continue;
            var kept = new JsonObject(previous.Where(p => !manual.Contains(p.Key)).Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
            if (build[key] is JsonObject owned) foreach (var (field, value) in owned) kept[field] = value?.DeepClone();
            if (kept.Count > 0) build[key] = kept;
        }
        foreach (var (key, value) in build) document[key] = value?.DeepClone();
        return document;
    }
    private static readonly (string Key, string[] Manual)[] SidecarManualKeys =
        [("folio_build_fact_sources", ["manual"]), ("folio_language_sources", ["manual"]), ("folio_native_build_sources", ["skills", "expertise", "saves"])];

    /// <summary>The build as one value addressed by its stored names, for choices that name their place in the build.</summary>
    public ChoiceValue AsChoiceTree() => JsonSerializer.Deserialize<ChoiceValue>(ToCanonical("build").ToJsonString())!;

    /// <summary>The build a choice tree describes; the reverse of <see cref="AsChoiceTree"/>.</summary>
    public static BuildFacts FromChoiceTree(ChoiceValue tree) => FromCanonical(JsonSerializer.SerializeToNode(tree)!.AsObject());

    /// <summary>An independent copy, sharing no lists or maps with this build.</summary>
    public BuildFacts Copy() => FromCanonical(ToCanonical("copy"));

    /// <summary>The rules input for this build. The rules recalculate everything else from it.</summary>
    public JsonObject ToCanonical(string characterId)
    {
        var canonical = JsonSerializer.SerializeToNode(this, Json)!.AsObject();
        foreach (var key in ManualKeys) canonical.Remove(key);
        canonical["schema_version"] = 5;
        canonical["character_id"] = characterId;
        if (ManualFacts is not null) canonical["folio_build_fact_sources"] = new JsonObject { ["manual"] = JsonSerializer.SerializeToNode(ManualFacts, Json) };
        if (ManualLanguages is not null) canonical["folio_language_sources"] = new JsonObject { ["manual"] = JsonSerializer.SerializeToNode(ManualLanguages, Json) };
        if (ManualSkills is not null || ManualExpertise is not null || ManualSaves is not null)
        {
            var native = new JsonObject();
            if (ManualSkills is not null) native["skills"] = JsonSerializer.SerializeToNode(ManualSkills, Json);
            if (ManualExpertise is not null) native["expertise"] = JsonSerializer.SerializeToNode(ManualExpertise, Json);
            if (ManualSaves is not null) native["saves"] = JsonSerializer.SerializeToNode(ManualSaves, Json);
            canonical["folio_native_build_sources"] = native;
        }
        return canonical;
    }
}
