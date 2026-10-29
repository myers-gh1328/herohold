using System.Text.Json;
using System.Text.Json.Nodes;
using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Herohold.Browser.Tests;

/// <summary>
/// Fictional upload files built once per test run through the native character workflow:
/// a Fighter (JSON, PDF, and an editable PDF without its embedded record), a Fighter 3 missing its
/// subclass, Wizard 1 and 7, Warlock 5 and Bard 2. Choices take the first eligible option.
/// </summary>
public static class Fixtures
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static string? directory;

    public static async Task<string> PathAsync(string name)
    {
        await Gate.WaitAsync();
        try { directory ??= await CreateAsync(); }
        finally { Gate.Release(); }
        return Path.Combine(directory, name);
    }

    private static async Task<string> CreateAsync()
    {
        var target = Path.Combine(HeroholdHost.Root, ".tmp", "herohold-ui-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(target);
        var rules = new ServiceCollection()
            .AddHeroholdRules(HeroholdHost.Root, Path.Combine(AppContext.BaseDirectory, "pdf-worker", "Herohold.PdfWorker.dll"))
            .BuildServiceProvider().GetRequiredService<ICharacterRules>();
        // Native "create" starts blank; starting fields are applied as an edit.
        DraftResult Create(string patch) => rules.Edit(rules.Create().Draft, Patch(patch));

        var fighter = Create("""
                {"name":"Fictional Astra","class":"Fighter","species":"Human","background":"Soldier",
                 "base_ability_scores":{"Strength":15,"Dexterity":12,"Constitution":14,"Intelligence":10,"Wisdom":10,"Charisma":8},
                 "species_options":{"skill_proficiencies":["Perception"],"origin_feats":["Tough"]},
                 "background_options":{"primary_ability":"Strength","secondary_ability":"Constitution","tool_proficiencies":["Dice Set"]},
                 "class_options":{"class_skills":["Acrobatics","Survival"],"fighting_style":"Defense"},
                 "custom_equipment":["Longsword"],"weapon_masteries":["Longsword","Longbow","Dagger"],"origin_languages":["Elvish","Dwarvish"]}
                """);
        if (!fighter.Validation.Complete) throw new InvalidOperationException("The fixture Fighter is incomplete: " + string.Join("; ", fighter.Validation.Missing));
        var fighterCharacter = Full(rules.Adopt(fighter.Draft));
        var exportedFighter = NativeFacts(await ExportAsync(rules, fighterCharacter));
        await Write(target, "fighter.json", exportedFighter);
        var pdf = await rules.ExportAsync(fighterCharacter, "pdf");
        await File.WriteAllBytesAsync(Path.Combine(target, "fighter.pdf"), pdf);
        await File.WriteAllBytesAsync(Path.Combine(target, "fighter-editable.pdf"), WithoutEmbeddedRecord(pdf));

        // A recoverable import: a valid Fighter 3 whose subclass has been removed.
        var missing = fighterCharacter;
        foreach (var level in new[] { 2, 3 })
            missing = LevelUp(rules, missing, level, choice => choice.Id.Contains("subclass") && Eligible(choice).Any(v => v.Text == "Champion") ? ChoiceValue.Of("Champion") : null);
        var missingDocument = await ExportAsync(rules, missing);
        missingDocument["name"] = "Fictional Missing Subclass";
        missingDocument["subclass"] = "";
        if (rules.ImportJson(missingDocument.DeepClone().AsObject()).Validation.Complete) throw new InvalidOperationException("The missing-subclass fixture must need review.");
        await Write(target, "fighter3-missing-subclass.json", NativeFacts(missingDocument));

        foreach (var (className, level) in new[] { ("Wizard", 1), ("Warlock", 5), ("Wizard", 7), ("Bard", 2) })
        {
            var draft = CompleteLevelOne(rules, Create, className);
            if (className == "Bard")
            {
                draft = rules.Edit(draft.Draft, Patch("""{"species_options":{"skill_proficiencies":["Perception"],"origin_feats":["Tough"]},"class_options":{"class_skills":["Arcana","Performance","Persuasion"]}}"""));
                if (!draft.Validation.Complete) throw new InvalidOperationException("The fixture Bard is incomplete.");
            }
            var character = rules.Adopt(draft.Draft);
            for (var next = 2; next <= level; next++)
                character = LevelUp(rules, character, next, choice => null, className == "Wizard" ? "Fireball" : null);
            if (className == "Wizard" && level == 7)
            {
                var edited = rules.Edit(CharacterDraft.Of(character), Patch("""{"name":"Fictional Fireball Wizard","spells":{"prepared":["Fireball"]}}"""), character);
                if (!edited.Validation.Complete) throw new InvalidOperationException("The fixture Fireball Wizard is incomplete.");
                character = rules.Adopt(edited.Draft, character);
            }
            await Write(target, className == "Wizard" && level == 7 ? "wizard7.json" : className.ToLowerInvariant() + ".json", NativeFacts(await ExportAsync(rules, Full(character))));
        }
        return target;
    }

    private static ChoiceValue Patch(string json) => JsonSerializer.Deserialize<ChoiceValue>(json)!;
    private static CharacterState Full(CharacterState c) => c with { CurrentHp = c.MaximumHp };
    private static async Task<JsonObject> ExportAsync(ICharacterRules rules, CharacterState c)
        => JsonNode.Parse(await rules.ExportAsync(c, "json"))!.AsObject();

    private static IEnumerable<ChoiceValue> Eligible(BuilderChoice choice) => choice.Options.Where(o => !o.Disabled).Select(o => o.Value);

    private static DraftResult CompleteLevelOne(ICharacterRules rules, Func<string, DraftResult> create, string className)
    {
        var result = create(JsonSerializer.Serialize(new { name = "Fictional " + className, @class = className, species = "Human", background = "Soldier" }));
        for (var i = 0; i < 100; i++)
        {
            var choice = result.Choices.FirstOrDefault(c => c.Required && c.Status != "resolved");
            if (choice is null) break;
            var options = Eligible(choice).ToList();
            var value = choice.Type == "single" && options.Count > 0 ? options[0]
                : choice.Type == "multi" && options.Count >= choice.Choose ? ChoiceValue.ListOf(options.Take(choice.Choose))
                : throw new InvalidOperationException("Fixture needs an exposed choice: " + choice.Id);
            result = rules.Edit(result.Draft, choice.Path, value);
        }
        if (!result.Validation.Complete) throw new InvalidOperationException($"Fictional {className} is incomplete: {string.Join("; ", result.Validation.Missing)}");
        return result;
    }

    private static CharacterState LevelUp(ICharacterRules rules, CharacterState character, int targetLevel, Func<BuilderChoice, ChoiceValue?> preferred, string? preferredSpell = null)
    {
        var result = rules.StartLevelUp(character, targetLevel);
        for (var i = 0; i < 100; i++)
        {
            var choice = result.PendingChoices.FirstOrDefault(c => c.Status != "resolved");
            if (choice is null) break;
            ChoiceValue value;
            try { value = preferred(choice) ?? PendingValue(choice, preferredSpell, character); }
            catch (Exception error) when (error is NullReferenceException or InvalidOperationException or ArgumentOutOfRangeException) { throw new InvalidOperationException("Unhandled level-up choice: " + choice.Id + " (" + choice.Type + ")", error); }
            result = rules.ChooseLevelUp(character, result.Review.Draft, choice.Id, value);
        }
        return rules.CommitLevelUp(character, result.Review.Draft, new());
    }

    private static ChoiceValue PendingValue(BuilderChoice choice, string? preferredSpell, CharacterState character)
    {
        var values = Eligible(choice).ToList();
        switch (choice.Type)
        {
            case "single": return values[0];
            case "multi":
                var preferredFirst = values.OrderBy(v => v.Text == preferredSpell ? 0 : 1).ToList();
                return ChoiceValue.ListOf(preferredFirst.Take(choice.Choose));
            case "asi_or_feat":
                // Raise the first ability that stays within its maximum.
                var name = Enum.GetValues<Ability>().First(a => character.AbilityScores[a] + 2 <= 20);
                return ChoiceValue.Record([new("mode", ChoiceValue.Of("asi")), new("ability_increases", ChoiceValue.Record([new(name.ToString(), ChoiceValue.Of(2))]))]);
            default: throw new InvalidOperationException("Fixture progression needs an exposed choice: " + choice.Id);
        }
    }

    /// <summary>Explicit fictional facts for native pickers; nothing is auto-assigned by the product.</summary>
    private static JsonObject NativeFacts(JsonObject character)
    {
        var result = character.DeepClone().AsObject();
        result["origin_languages"] = new JsonArray("Elvish", "Dwarvish");
        (result["background_options"] as JsonObject ?? (JsonObject)(result["background_options"] = new JsonObject()))["tool_proficiencies"] = new JsonArray("Dice Set");
        if (result["class"]?.GetValue<string>() == "Bard")
            (result["class_options"] as JsonObject ?? (JsonObject)(result["class_options"] = new JsonObject()))["tool_proficiencies"] = new JsonArray("Flute", "Lute", "Drum");
        return result;
    }

    private static byte[] WithoutEmbeddedRecord(byte[] pdf)
    {
        using var input = new MemoryStream(pdf);
        using var document = new Aspose.Pdf.Document(input);
        document.EmbeddedFiles!.Delete();
        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }

    private static Task Write(string folder, string name, JsonObject character) =>
        File.WriteAllTextAsync(Path.Combine(folder, name), character.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
}
