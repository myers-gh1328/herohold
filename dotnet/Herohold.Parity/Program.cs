using System.Text.Json;
using System.Text.Json.Serialization;
using Herohold.Core;

if (args.Length == 3 && args[0] == "--rules")
    return await NativeRulesComparison.RunAsync(args[1], args[2]);
if (args.Length == 2 && args[0] == "--derivations")
    return await DerivationComparison.RunAsync(args[1]);
if (args.Length == 2 && args[0] == "--defenses")
    return await DefenseComparison.RunAsync(args[1]);
if (args.Length == 3 && args[0] == "--catalog")
    return await CatalogComparison.RunAsync(args[1], args[2]);
if (args.Length == 3 && args[0] == "--class-choices")
    return await ClassChoiceComparison.RunAsync(args[1], args[2]);
if (args.Length == 3 && args[0] == "--feat-choices")
    return await FeatChoiceComparison.RunAsync(args[1], args[2]);
if (args.Length == 3 && args[0] == "--creation-choices")
    return await CreationChoiceComparison.RunAsync(args[1], args[2]);

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: Herohold.Parity <fictional-calculation-fixtures.json>");
    return 2;
}

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
json.Converters.Add(new JsonStringEnumConverter());
try
{
    var document = JsonSerializer.Deserialize<FixtureDocument>(await File.ReadAllTextAsync(args[0]), json)
        ?? throw new InvalidDataException("Fixture document is empty.");
    if (document.SchemaVersion != 1 || document.Fixtures.Count == 0)
        throw new InvalidDataException("Expected schema version 1 and at least one calculation fixture.");
    if (document.Fixtures.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() != document.Fixtures.Count)
        throw new InvalidDataException("Fixture IDs must be unique.");

    var results = new List<object>();
    foreach (var fixture in document.Fixtures)
    {
        var c = fixture.Character;
        var before = JsonSerializer.Serialize(c, json);
        var modifiers = Enum.GetValues<Ability>().ToDictionary(ability => ability.ToString(), ability => Checked(CharacterCalculations.Ability(c, ability)));
        var result = new
        {
            abilityModifiers = modifiers,
            proficiencyBonus = CharacterCalculations.ProficiencyBonus(c.Level),
            skillBonus = Checked(CharacterCalculations.Skill(c, fixture.Skill.Name, fixture.Skill.Ability)),
            saveBonus = Checked(CharacterCalculations.Save(c, fixture.SaveAbility)),
            attackBonus = Checked(CharacterCalculations.Attack(c, fixture.Attack.Ability, fixture.Attack.Proficient, fixture.Attack.FlatBonus)),
            initiativeBonus = Checked(CharacterCalculations.Initiative(c))
        };
        // Repeated inspection must not compound bonuses or mutate HP/resources.
        foreach (var ability in Enum.GetValues<Ability>())
            if (Checked(CharacterCalculations.Ability(c, ability)) != modifiers[ability.ToString()])
                throw new InvalidDataException($"{fixture.Id}: repeated calculation changed its total.");
        if (!StringComparer.Ordinal.Equals(before, JsonSerializer.Serialize(c, json)))
            throw new InvalidDataException($"{fixture.Id}: calculation mutated its character input.");
        results.Add(new { id = fixture.Id, result });
    }
    Console.WriteLine(JsonSerializer.Serialize(results, json));
    return 0;
}
catch (Exception error) when (error is IOException or JsonException or InvalidDataException or ArgumentException or NotImplementedException)
{
    Console.Error.WriteLine($"Calculation comparison failed: {error.Message}");
    return 1;
}

static int Checked(Calculation calculation)
{
    if (calculation.Terms.Sum(term => term.Value) != calculation.Total)
        throw new InvalidDataException("Calculation breakdown terms do not sum to the returned total.");
    return calculation.Total;
}

internal sealed record FixtureDocument
{
    public int SchemaVersion { get; init; }
    public List<CalculationFixture> Fixtures { get; init; } = [];
}
internal sealed record CalculationFixture
{
    public string Id { get; init; } = string.Empty;
    public CharacterState Character { get; init; } = new();
    public SkillSelection Skill { get; init; } = new();
    public Ability SaveAbility { get; init; }
    public AttackSelection Attack { get; init; } = new();
}
internal sealed record SkillSelection
{
    public string Name { get; init; } = string.Empty;
    public Ability Ability { get; init; }
}
internal sealed record AttackSelection
{
    public Ability Ability { get; init; }
    public bool Proficient { get; init; }
    public int FlatBonus { get; init; }
}
