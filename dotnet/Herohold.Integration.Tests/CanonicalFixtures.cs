using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Rules;

namespace Herohold.Integration.Tests;

/// <summary>Runs typed rules on canonical character fixtures, reading them as the app does.</summary>
internal static class CanonicalFixtures
{
    public static NativeBuildSummary Derive(this CharacterBuildCatalog catalog, JsonObject raw, IReadOnlyList<EquipmentItem> equipment, bool allowUnresolved = false)
        => catalog.Derive(BuildFacts.FromCanonical(raw), equipment, allowUnresolved, ObservedProficiencies.From(raw));
    public static LanguageSummary Languages(JsonObject raw, IReadOnlyList<ChoiceProficiencySource> granted, bool allowUnresolved = false)
    {
        var observed = ObservedProficiencies.From(raw);
        return LanguageRules.Derive(BuildFacts.FromCanonical(raw), granted, allowUnresolved, observed.Languages, observed.PreviousLanguages);
    }
    public static AttackActionSummary Derive(this AttackActionRules rules, JsonObject raw, NativeBuildSummary build, ActiveFeatureSummary features, ClassResourceSummary resources, IReadOnlyList<EquipmentItem> equipment)
        => rules.Derive(BuildFacts.FromCanonical(raw), build, features, resources, equipment);
    private static IReadOnlyList<string> Names(JsonNode? node) => node is JsonArray array ? [.. array.Select(item => item is JsonObject record ? record["name"]?.GetValue<string>() ?? "" : item?.GetValue<string>() ?? "")] : [];
    private static JsonObject? HitPointBlock(JsonObject raw) => raw["hit_points"] switch { null => null, JsonObject block => block, _ => throw new ArgumentException("Canonical HP field hit_points must be an object.") };
    private static int Whole(JsonNode? node) => node is null ? 0 : node is JsonValue value && value.TryGetValue<int>(out var number) ? number : throw new ArgumentException("Canonical HP fields must contain whole numbers.");
    /// <summary>HP inputs from a fixture carrying the origin feats and species traits a calculation found.</summary>
    public static HitPointInput HitPoints(JsonObject raw, int hitDie, int constitutionModifier)
        => HitPointRules.FromBuild(BuildFacts.FromCanonical(raw), hitDie, constitutionModifier, Names(raw["origin_feats"]), Names(raw["species_traits"]), Whole(HitPointBlock(raw)?["current"]), Whole(HitPointBlock(raw)?["temporary"]));
    public static HitPointInput HitPointsOfBuild(JsonObject raw, int hitDie, int constitutionModifier, bool tough = false)
        => HitPointRules.FromBuild(BuildFacts.FromCanonical(raw), hitDie, constitutionModifier, tough, false, 0, Whole(HitPointBlock(raw)?["current"]), Whole(HitPointBlock(raw)?["temporary"]));
    public static ClassChoiceContext ChoiceContext(JsonObject raw, NativeBuildSummary build)
        => ClassChoiceContextFactory.FromBuild(BuildFacts.FromCanonical(raw), build, SpellGrantRecord.FromDocument(raw["spell_grant_details"]), Names(raw["expertise"]));
    public static ActiveFeatureSummary Derive(this ActiveFeatureRules rules, JsonObject raw, NativeBuildSummary build) => rules.Derive(BuildFacts.FromCanonical(raw), build, ChoiceContext(raw, build));
    public static ClassResourceSummary Derive(this ClassResourceRules rules, JsonObject raw, NativeBuildSummary build, ActiveFeatureSummary features)
        => rules.Derive(BuildFacts.FromCanonical(raw), build, features, raw["speed"] is JsonValue speed && speed.TryGetValue<int>(out var walking) ? walking : 30);
    public static SpellGrantSummary Derive(this SpellGrantRules rules, JsonObject raw, IReadOnlyDictionary<Ability, int> finalModifiers)
        => rules.Derive(BuildFacts.FromCanonical(raw), finalModifiers, Names(raw["folio_species_auto_grants"]));
    public static SpeciesSpellGrantSummary Derive(this RawSpeciesSpellCatalog species, JsonObject raw)
        => species.Derive(BuildFacts.FromCanonical(raw), Names(raw["folio_species_auto_grants"]));
    private static readonly System.Text.Json.JsonSerializerOptions Web = new(System.Text.Json.JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
    /// <summary>A fixture character as a typed draft: its build and inventory.</summary>
    public static DraftCharacter Draft(JsonObject raw) => new(BuildFacts.FromCanonical(raw), raw["folio_inventory"]?.Deserialize<List<EquipmentItem>>(Web) ?? []);
    public static ChoiceValue Choice(JsonNode? node) => ChoiceReading.FromCatalog(node);
    /// <summary>A typed option value as the JSON a fixture patch carries.</summary>
    public static JsonNode? Node(ChoiceValue? value) => value is null || value.IsNull ? null : JsonSerializer.SerializeToNode(value);
    public static LevelUpReview Start(this NativeLevelUpRules rules, JsonObject raw, int level) => rules.Start(Draft(raw), level);
    public static LevelUpReview Start(this NativeLevelUpRules rules, JsonObject raw, int level, string className) => rules.Start(Draft(raw), level, className);
    public static LevelUpReview Choose(this NativeLevelUpRules rules, JsonObject raw, LevelUpDraft draft, string id, JsonNode? value) => rules.Choose(Draft(raw), draft, id, Choice(value));
    public static LevelUpReview Preview(this NativeLevelUpRules rules, JsonObject raw, LevelUpDraft draft, LevelUpHpOptions? hp = null) => rules.Preview(Draft(raw), draft, hp);
    public static LevelUpReview Commit(this NativeLevelUpRules rules, JsonObject raw, LevelUpDraft draft, LevelUpHpOptions? hp = null) => rules.Commit(Draft(raw), draft, hp);
    public static IReadOnlyList<ClassEntryOption> EligibleClasses(this NativeLevelUpRules rules, JsonObject raw) => rules.EligibleClasses(Draft(raw));
    public static CreationChoiceContext Creation(JsonObject raw) => new() { Build = BuildFacts.FromCanonical(raw), HasInventory = raw["folio_inventory"] is not null };
    public static Dictionary<string, ChoiceValue> Choices(JsonObject? choices) => choices?.Deserialize<Dictionary<string, ChoiceValue>>() ?? [];
    public static Dictionary<string, ChoiceValue> Choices(IReadOnlyDictionary<string, JsonNode?> choices) => choices.ToDictionary(p => p.Key, p => Choice(p.Value), StringComparer.Ordinal);
    /// <summary>Applies an edit to a fixture: build choices through the typed rules, the draft's own facts to the document.</summary>
    public static JsonObject Apply(this CharacterEditRules edits, JsonObject raw, JsonObject patch)
    {
        var build = new JsonObject(patch.Where(p => !CharacterEditRules.DraftFacts.Contains(p.Key)).Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
        var edited = edits.Apply(BuildFacts.FromCanonical(raw), Choice(build)).ReplaceIn(raw, "fixture");
        foreach (var pair in patch.Where(p => CharacterEditRules.DraftFacts.Contains(p.Key))) edited[pair.Key] = pair.Value?.DeepClone();
        return edited;
    }
    public static IReadOnlyList<BuildIssue> Validate(this ExistingFeatValidationRules rules, JsonObject raw, FeatRuleContext context) => rules.Validate(BuildFacts.FromCanonical(raw), context);
    public static IReadOnlyList<ExistingFeatChoice> DescribeChoices(this ExistingFeatValidationRules rules, JsonObject raw, FeatRuleContext context) => rules.DescribeChoices(BuildFacts.FromCanonical(raw), context);
    public static JsonObject ApplyChoices(this ExistingFeatValidationRules rules, JsonObject raw, string instanceId, JsonObject choices) => rules.ApplyChoices(BuildFacts.FromCanonical(raw), instanceId, Choices(choices)).ReplaceIn(raw, "fixture");
    public static JsonObject NormalizeIdentities(this ExistingFeatValidationRules rules, JsonObject raw) => rules.NormalizeIdentities(BuildFacts.FromCanonical(raw)).ReplaceIn(raw, "fixture");
    /// <summary>Validates a fixture selection: <c>{"choices": {...}, "ability_increases": {...}}</c>.</summary>
    public static FeatSelectionVerdict ValidateSelection(this FeatSelectionRules rules, string featId, FeatRuleContext context, JsonObject selection)
        => rules.ValidateSelection(featId, context, selection["choices"] is JsonObject choices ? Choices(choices) : null,
            selection["ability_increases"] is JsonObject increases ? increases.ToDictionary(p => p.Key, p => p.Value is JsonValue v && v.TryGetValue<int>(out var n) ? n : -1) : null);
    extension(LevelUpReview review)
    {
        /// <summary>The leveled build as a character document, with the maximum HP the rules found.</summary>
        public JsonObject Character
        {
            get
            {
                var doc = review.Build.ToCanonical("review");
                doc["hit_points"] = new JsonObject { ["maximum"] = review.MaximumHp, ["max"] = review.MaximumHp };
                return doc;
            }
        }
    }
    public static IReadOnlyList<FightingStyleSource> Styles(JsonObject raw) => FightingStyleRules.Sources(BuildFacts.FromCanonical(raw));
    public static IReadOnlyList<string> ActiveStyles(JsonObject raw) => FightingStyleRules.ActiveStyles(BuildFacts.FromCanonical(raw));
    public static IReadOnlyList<NativeOriginFeatInstance> OriginFeats(JsonObject raw, JsonObject? background, JsonObject? species)
        => NativeOriginFeatInstances.Derive(BuildFacts.FromCanonical(raw), background is null ? null : CatalogBackground.From(background), species is null ? null : CatalogSpecies.From(species));
}
