using Herohold.Core;

namespace Herohold.Rules;

/// <summary>Single-class HP from recorded unmodified Hit Dice. No implicit healing or rest.</summary>
public static class HitPointRules
{
    /// <summary>Consumes resolved feature identities; source selection remains the catalog adapter's responsibility.</summary>
    /// <summary>
    /// HP inputs for a calculated character: its build, the origin feats and species traits the rules found for it,
    /// and the current and temporary HP it has in play.
    /// </summary>
    public static HitPointInput FromBuild(BuildFacts build, int hitDie, int constitutionModifier, IEnumerable<string> originFeats, IEnumerable<string> speciesTraits, int currentHp = 0, int temporaryHp = 0)
    {
        ArgumentNullException.ThrowIfNull(build);
        var feats=(build.Feats??[]).Select(f=>f.Name).ToArray();
        var origins=originFeats.Concat(ChoiceReading.Names(build.BackgroundOptions.Get("origin_feats"))).Concat(ChoiceReading.Names(build.SpeciesOptions.Get("origin_feats"))).Concat(feats);
        var additional=feats.Contains("Boon of Fortitude")?40:0;
        var sorcerer=ClassTrackRules.Read(build).Tracks.FirstOrDefault(t=>t.ClassName=="Sorcerer");
        if(sorcerer is {Level:>=3,SubclassName:"Draconic Sorcery"})additional+=sorcerer.Level;
        return FromBuild(build,hitDie,constitutionModifier,origins.Contains("Tough"),speciesTraits.Contains("Dwarven Toughness"),additional,currentHp,temporaryHp);
    }

    public static int FixedHitDieGain(int hitDie)
    {
        ValidateDie(hitDie);
        return hitDie / 2 + 1;
    }

    public static HitPointSummary Calculate(HitPointInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ClassName);
        ValidateDie(input.HitDie);
        if (input.ClassName.Length > 200 || input.Level is < 1 or > 20 || input.ConstitutionModifier is < -100 or > 100
            || input.CurrentHp is < 0 or > 100000 || input.TemporaryHp is < 0 or > 100000
            || input.AdditionalMaximumBonus is < -100000 or > 100000 || input.ImportedMaximumAdjustment is < -100000 or > 100000)
            throw new ArgumentOutOfRangeException(nameof(input), "Hit point inputs exceed their supported bounds.");
        if (input.History is null || input.History.Count > 20) throw new ArgumentException("HP history must contain at most twenty levels.");
        var history = new Dictionary<int, HitPointHistoryEntry>();
        foreach (var entry in input.History)
        {
            if (entry is null || entry.ClassName is null || !(input.ClassHitDice.Count>0?input.ClassHitDice.ContainsKey(entry.ClassName):entry.ClassName.Equals(input.ClassName, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("HP history belongs to another class or contains a missing entry.");
            if (entry.CharacterLevel < 1 || entry.CharacterLevel > input.Level || !history.TryAdd(entry.CharacterLevel, entry with { }))
                throw new ArgumentException("HP history contains a duplicate or out-of-range character level.");
            var entryDie=input.ClassHitDice.TryGetValue(entry.ClassName,out var classDie)?classDie:input.HitDie;
            if (!Enum.IsDefined(entry.Method) || entry.BaseGain < 1 || entry.BaseGain > entryDie
                || entry.CharacterLevel == 1 && entry.Method != HitPointMethod.Maximum
                || entry.Method == HitPointMethod.Maximum && (entry.CharacterLevel != 1 || entry.BaseGain != entryDie)
                || entry.Method == HitPointMethod.Fixed && entry.BaseGain != FixedHitDieGain(entryDie))
                throw new ArgumentException("An HP history method or unmodified die result is invalid.");
        }
        if(input.ClassHitDice.Count>1&&(history.Count!=input.Level||input.ClassLevels.Any(p=>history.Values.Count(h=>h.ClassName==p.Key)!=p.Value)||history[1].ClassName!=input.ClassName))throw new ArgumentException("Multiclass HP history must identify the initial class and one contribution for every class level.");
        for (var level = 1; level <= input.Level; level++)
            history.TryAdd(level, new(level, input.ClassName, level == 1 ? HitPointMethod.Maximum : HitPointMethod.Fixed,
                level == 1 ? input.HitDie : FixedHitDieGain(input.HitDie)));
        var ordered = history.Values.OrderBy(x => x.CharacterLevel).ToList();
        var contributions = ordered.Select(entry =>
        {
            var gain = Math.Max(1, entry.BaseGain + input.ConstitutionModifier);
            var tough = input.Tough ? 2 : 0;
            var species = input.DwarvenToughness ? 1 : 0;
            return new HitPointContribution(entry.CharacterLevel, entry.Method, entry.BaseGain, input.ConstitutionModifier, gain, tough, species, gain + tough + species);
        }).ToList();
        var calculated = contributions.Sum(x => x.Total) + input.AdditionalMaximumBonus;
        var maximum = Math.Max(1, calculated + input.ImportedMaximumAdjustment);
        if (maximum > 100000) throw new ArgumentOutOfRangeException(nameof(input), "Calculated maximum HP exceeds the supported limit.");
        return new()
        {
            Maximum = maximum, CurrentHp = Math.Min(input.CurrentHp, maximum), TemporaryHp = input.TemporaryHp,
            CalculatedBeforeImportAdjustment = calculated, ImportAdjustment = input.ImportedMaximumAdjustment,
            History = ordered.AsReadOnly(), Contributions = contributions.AsReadOnly()
        };
    }

    public static HitPointInput FromBuild(BuildFacts build, int hitDie, int constitutionModifier, bool tough = false, bool dwarvenToughness = false, int additionalMaximumBonus = 0, int currentHp = 0, int temporaryHp = 0)
    {
        ArgumentNullException.ThrowIfNull(build);
        var classLevels = build.ClassLevels ?? [];
        var mixed=classLevels.Count>1;
        if(mixed&&!ClassTrackRules.Read(build).CanCalculate)throw new ArgumentException("Resolve source-owned class tracks before calculating mixed-class HP.");
        var history = new List<HitPointHistoryEntry>();
        if (build.HpHistory is { } entries)
        {
            if (entries.Count > 20) throw new ArgumentException("Canonical HP history is invalid or too large.");
            foreach (var entry in entries)
            {
                var method = entry.Method.ToLowerInvariant() switch
                {
                    "maximum" => HitPointMethod.Maximum,
                    "fixed" => HitPointMethod.Fixed,
                    "rolled" => HitPointMethod.Rolled,
                    _ => throw new ArgumentException("Canonical HP history uses an unsupported method.")
                };
                history.Add(new(entry.CharacterLevel, entry.ClassName, method, entry.BaseGain));
            }
        }
        return new()
        {
            ClassName = build.ClassName, Level = build.Level, HitDie = hitDie, ConstitutionModifier = constitutionModifier,
            ClassHitDice=mixed?classLevels.ToDictionary(p=>p.Key,p=>MulticlassRules.HitDie(p.Key)):new Dictionary<string,int>(),ClassLevels=mixed?new Dictionary<string,int>(classLevels):new Dictionary<string,int>(),
            History = history.AsReadOnly(), Tough = tough, DwarvenToughness = dwarvenToughness, AdditionalMaximumBonus = additionalMaximumBonus,
            ImportedMaximumAdjustment = build.ImportOverrides?.MaximumHpAdjustment ?? 0,
            CurrentHp = currentHp, TemporaryHp = temporaryHp
        };
    }

    private static void ValidateDie(int hitDie)
    {
        if (hitDie is not (4 or 6 or 8 or 10 or 12)) throw new ArgumentOutOfRangeException(nameof(hitDie), "Use a supported Hit Die: d4, d6, d8, d10, or d12.");
    }
}
