using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Core;
using Herohold.Rules;

namespace Herohold.Web.Services;

/// <summary>
/// A character being created, edited or reviewed: its build, and the play facts it brings when it is not new.
/// The rules settle everything else from these.
/// </summary>
public sealed record CharacterDraft
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public string Id { get; init; } = "draft";
    public BuildFacts Build { get; init; } = new();
    /// <summary>The draft's own items; unset while the class and background starting equipment decides them.</summary>
    public List<EquipmentItem>? Inventory { get; init; }
    /// <summary>Without items of its own: whether the starting equipment packages are included.</summary>
    public bool IncludeDefaultEquipment { get; init; } = true;
    /// <summary>Without items of its own: equipment named beside the packages, and any ability bonuses an item gives.</summary>
    public List<string> CustomEquipment { get; init; } = [];
    public Dictionary<string, Dictionary<Ability, int>> EquipmentBonuses { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Notes brought by an imported character without journal entries.</summary>
    public string Notes { get; init; } = "";
    /// <summary>An edited or imported character's play facts; unset for a new character. Its items are <see cref="Inventory"/>.</summary>
    public CharacterFacts? Facts { get; init; }

    /// <summary>A saved character as a draft: its stored build and play facts.</summary>
    public static CharacterDraft Of(CharacterState character)
    {
        var record = CharacterRecords.FromState(character, 0);
        return new() { Id = character.Id, Build = record.Build, Inventory = [.. character.Equipment], IncludeDefaultEquipment = false, Facts = record.Facts };
    }

    /// <summary>The draft parts of a rules character document; its build and play facts are read separately.</summary>
    public static CharacterDraft FromDocument(JsonObject character) => new()
    {
        Id = character["character_id"] is JsonValue id && id.TryGetValue<string>(out var text) && text.Length > 0 ? text : Guid.NewGuid().ToString("N"),
        Build = BuildFacts.FromCanonical(character),
        Inventory = character["folio_inventory"]?.Deserialize<List<EquipmentItem>>(Json),
        IncludeDefaultEquipment = character["include_default_equipment"] is not JsonValue flag || !flag.TryGetValue<bool>(out var include) || include,
        CustomEquipment = Names(character["custom_equipment"]),
        EquipmentBonuses = character["equipment_modifier_bonuses"] is JsonObject bonuses ? bonuses.Deserialize<Dictionary<string, Dictionary<Ability, int>>>(Json) ?? [] : new(StringComparer.Ordinal),
        Notes = character["notes"] is JsonValue notes && notes.TryGetValue<string>(out var note) ? note : ""
    };

    private static List<string> Names(JsonNode? node) => node is JsonArray array
        ? [.. array.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "").Where(s => s.Length > 0)] : [];
}

/// <summary>A draft as the rules settled it, with the choices it still offers and whether it is complete.</summary>
public sealed record DraftResult
{
    public CharacterDraft Draft { get; init; } = new();
    public DraftSummary Summary { get; init; } = new();
    public List<BuilderChoice> Choices { get; init; } = [];
    public WorkflowValidation Validation { get; init; } = new();
    public ImportReview? Review { get; init; }
}

/// <summary>A level-up in progress: the rules' review, the character before it, and the leveled character.</summary>
public sealed record LevelUpResult
{
    public LevelUpReview Review { get; init; } = new();
    /// <summary>The character's own check before the level-up.</summary>
    public WorkflowValidation Validation { get; init; } = new();
    public DraftSummary Candidate { get; init; } = new();
    public List<BuilderChoice> PendingChoices { get; init; } = [];
    public WorkflowValidation PreviewValidation { get; init; } = new();
}

/// <summary>How an uploaded character compares with what Herohold recalculated.</summary>
public sealed record ImportReview
{
    public bool? CanAdopt { get; init; }
    /// <summary>Whether the uploaded source itself allows adoption, before the build is checked.</summary>
    public bool SourceCanAdopt { get; init; }
    public string SourceKind { get; init; } = "";
    public string Message { get; init; } = "";
    public List<string> DroppedFields { get; init; } = [];
    public List<ImportIssue> Issues { get; init; } = [];
    public List<ImportComparisonView> Comparisons { get; init; } = [];
    public List<KeyValuePair<string, string>> Fields { get; init; } = [];
    /// <summary>A PDF review's recovered selections; the review reruns from these and the original PDF.</summary>
    public ChoiceValue? Selections { get; init; }
}

public sealed record ImportIssue
{
    public string Code { get; init; } = "";
    public string Severity { get; init; } = "";
    public string Label { get; init; } = "";
    public string? Observed { get; init; }
    public string? Expected { get; init; }
    public string Remedy { get; init; } = "";
    public bool Blocks => Severity is "blocker" or "error";
}
