using System.Security.Cryptography;
using Herohold.Core;
namespace Herohold.Rules;

public enum AbilityGenerationMethod { StandardArray, Roll4d6 }

/// <summary>Explicit score generation and positional assignments. Recalculation never calls the random source.</summary>
public static class AbilityGenerationRules
{
    private static readonly Ability[] Abilities = Enum.GetValues<Ability>();
    private static readonly IReadOnlyList<int> Standard = Array.AsReadOnly(new[] { 15, 14, 13, 12, 10, 8 });
    public static IReadOnlyList<int> StandardArray => Standard;
    public static IReadOnlyList<int> Roll(Func<int>? rollD6 = null)
    {
        rollD6 ??= () => RandomNumberGenerator.GetInt32(1, 7);
        var scores = new int[6];
        for (var score = 0; score < 6; score++)
        {
            var minimum = 6; var sum = 0;
            for (var die = 0; die < 4; die++)
            {
                var value = rollD6(); if (value is < 1 or > 6) throw new ArgumentException("A six-sided die result must be1–6.");
                sum += value; minimum = Math.Min(minimum, value);
            }
            scores[score] = sum - minimum;
        }
        Array.Sort(scores); Array.Reverse(scores); return Array.AsReadOnly(scores);
    }
    public static IReadOnlyDictionary<Ability, int> ResolveAssignments(IReadOnlyList<int> pool, IReadOnlyDictionary<Ability, int> assignments)
    {
        ArgumentNullException.ThrowIfNull(pool); ArgumentNullException.ThrowIfNull(assignments);
        if (pool.Count != 6 || pool.Any(s => s is < 3 or > 18)) throw new ArgumentException("Generated scores must contain six scores3–18.");
        if (assignments.Count != 6 || assignments.Keys.Any(a => !Enum.IsDefined(a)) || assignments.Values.Any(i => i is < 0 or > 5) || assignments.Values.Distinct().Count() != 6)
            throw new ArgumentException("Assign each of the six score positions to a different ability.");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<Ability, int>(Abilities.ToDictionary(a => a, a => pool[assignments[a]]));
    }
    private static BuildFacts Apply(BuildFacts build, IReadOnlyList<int> pool, IReadOnlyDictionary<Ability, int> positions)
    {
        var scores = ResolveAssignments(pool, positions);
        return build with
        {
            AbilityScoreAssignments = Abilities.Where(positions.ContainsKey).ToDictionary(a => a.ToString(), a => positions[a]),
            BaseAbilityScores = Abilities.ToDictionary(a => a, a => scores[a])
        };
    }
    public static BuildFacts Generate(BuildFacts source, AbilityGenerationMethod method, Func<int>? rollD6 = null)
    {
        ArgumentNullException.ThrowIfNull(source); if (!Enum.IsDefined(method)) throw new ArgumentException("Choose a supported score generation method.");
        var pool = method == AbilityGenerationMethod.StandardArray ? StandardArray : Roll(rollD6);
        var generated = source with { AbilityScoreMethod = method == AbilityGenerationMethod.StandardArray ? "Standard array" : "Roll 4d6" };
        if (method == AbilityGenerationMethod.Roll4d6) generated = generated with { RolledAbilityScores = [.. pool] };
        return Apply(generated, pool, Abilities.Select((a, i) => (a, i)).ToDictionary(p => p.a, p => p.i));
    }
    private static IReadOnlyList<int> Pool(BuildFacts source)
    {
        var method = source.AbilityScoreMethod;
        if (method == "Standard array") return StandardArray;
        if (method != "Roll 4d6") throw new ArgumentException("Select a generated score method before assigning scores.");
        if (source.RolledAbilityScores is not { Count: 6 } pool || pool.Any(n => n is not (>= 3 and <= 18))) throw new ArgumentException("Roll six ability scores before assigning them.");
        return [.. pool];
    }
    private static IReadOnlyDictionary<Ability, int> Positions(BuildFacts source)
    {
        if (source.AbilityScoreAssignments is not { Count: 6 } positions) throw new ArgumentException("Assign six distinct score positions.");
        var result = new Dictionary<Ability, int>();
        foreach (var pair in positions)
        {
            if (!Enum.TryParse<Ability>(pair.Key, out var ability) || !Enum.IsDefined(ability) || ability.ToString() != pair.Key) throw new ArgumentException("Ability assignments must use valid ability names and position numbers.");
            result.Add(ability, pair.Value);
        }
        return result;
    }
    public static BuildFacts Swap(BuildFacts source, Ability ability, int position)
    {
        ArgumentNullException.ThrowIfNull(source); if (!Enum.IsDefined(ability) || position is < 0 or > 5) throw new ArgumentException("Choose a valid ability and score position.");
        var pool = Pool(source); var assignments = Positions(source); _ = ResolveAssignments(pool, assignments);
        var updated = assignments.ToDictionary(p => p.Key, p => p.Value); var other = updated.Single(p => p.Value == position).Key;
        updated[other] = updated[ability]; updated[ability] = position;
        return Apply(source, pool, updated);
    }
    public static IReadOnlyList<BuildIssue> Validate(BuildFacts source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var method = source.AbilityScoreMethod;
        if (method is null || method == "Manual entry") return [];
        if (method is not ("Standard array" or "Roll 4d6")) return [new("invalid_ability_method", ["ability_score_method"], "Choose Manual entry, Standard array, or Roll4d6.")];
        IReadOnlyList<int> pool;
        try { pool = Pool(source); }
        catch (ArgumentException) { return [new("missing_ability_roll", ["rolled_ability_scores"], "Roll six scores before assigning them.")]; }
        IReadOnlyDictionary<Ability, int> scores;
        try { scores = ResolveAssignments(pool, Positions(source)); }
        catch (ArgumentException) { return [new("invalid_ability_assignments", ["ability_score_assignments"], "Assign every score position once, including positions with equal values.")]; }
        if (source.BaseAbilityScores is not { Count: 6 } bases || scores.Any(p => bases.GetValueOrDefault(p.Key, -1) != p.Value))
            return [new("ability_assignment_mismatch", ["base_ability_scores"], "Base scores must match the assigned score positions; use Manual entry to enter other scores.")];
        return [];
    }
}
