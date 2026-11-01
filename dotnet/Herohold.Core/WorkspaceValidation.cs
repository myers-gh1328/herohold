using System.Text.RegularExpressions;

namespace Herohold.Core;

internal static partial class WorkspaceValidation
{
    [GeneratedRegex(@"\A[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}\z", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdPattern();

    internal static void Id(string id)
    {
        if (id is null || !SafeIdPattern().IsMatch(id))
            throw new ArgumentException("IDs must contain 1–64 letters, numbers, hyphens, or underscores and start with a letter or number.", nameof(id));
    }

    internal static void Character(CharacterState c)
    {
        ArgumentNullException.ThrowIfNull(c);
        Id(c.Id);
        Text(c.Name, 200, true); Text(c.Species, 200); Text(c.ClassName, 200); Text(c.Subclass, 200);
        if (c.Exhaustion is < 0 or > 6 || c.Level is < 1 or > 20 || c.MaximumHp is < 1 or > 100000 || c.CurrentHp < 0 || c.CurrentHp > c.MaximumHp
            || c.TemporaryHp is < 0 or > 100000 || c.ArmorClass is < 0 or > 100 || c.Speed is < 0 or > 10000 || c.Turn < 1)
            throw new ArgumentException("The character has invalid level, HP, armor, speed, or turn values.");
        if (c.AbilityScores is null || c.AbilityScores.Count != 6 || Enum.GetValues<Ability>().Any(a => !c.AbilityScores.TryGetValue(a, out var score) || score is < 1 or > 30))
            throw new ArgumentException("The character needs all six ability scores between 1 and 30.");
        if (c.SaveProficiencies is null || c.SaveProficiencies.Any(a => !Enum.IsDefined(a))
            || c.SkillProficiencies is null || c.Expertise is null || c.Expertise.Any(x => !c.SkillProficiencies.Contains(x, StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException("The character has invalid proficiencies or Expertise.");
        foreach (var skill in c.SkillProficiencies.Concat(c.Expertise)) Text(skill, 100, true);
        if (c.Equipment is null || c.Resources is null || c.Notes is null || c.Effects is null
            || c.Equipment.Count > 500 || c.Resources.Count > 100 || c.Notes.Count > 1000 || c.Effects.Count > 100)
            throw new ArgumentException("Workspace collection limits were exceeded or a collection is missing.");
        Unique(c.Equipment.Select(x => x?.Id)); Unique(c.Resources.Select(x => x?.Id)); Unique(c.Notes.Select(x => x?.Id)); Unique(c.Effects.Select(x => x?.Id));
        foreach (var item in c.Equipment)
        {
            Text(item.Name, 200, true); Text(item.Description, 10000); Text(item.CatalogItemId, 200); Text(item.ScrollSpell, 200);
            if (item.Quantity is < 0 or > 100000 || !item.Carried&&(item.Equipped||item.Attuned) || item.Quantity == 0 && (item.Equipped || item.Attuned)
                || item.Attuned && !item.RequiresAttunement || item.ModifierBonuses is null
                || item.ModifierBonuses.Any(x => !Enum.IsDefined(x.Key) || x.Value is < -30 or > 30))
                throw new ArgumentException("An inventory quantity, activation, or modifier is invalid.");
        }
        if (c.Equipment.Count(x => x.Attuned) > 3) throw new ArgumentException("Only three items can be attuned.");
        foreach (var resource in c.Resources)
        {
            Text(resource.Name, 200, true);
            if(resource.SourceId is not null)Text(resource.SourceId,200,true);
            if (resource.Maximum is < 0 or > 100000 || resource.Remaining < 0 || resource.Remaining > resource.Maximum || resource.SourceKind is not ("manual" or "build" or "grant") || !Enum.IsDefined(resource.Recovery) || resource.SlotLevel is < 1 or > 9 || resource.ShortRestRecoveryAmount is < 1 or > 100000)
                throw new ArgumentException("A resource is outside its allowed bounds.");
        }
        foreach (var note in c.Notes) { Text(note.Title, 200, true); Text(note.Text, 12000, true); }
        JournalRules.ValidateMetadata(c);
        foreach (var effect in c.Effects)
        {
            Text(effect.Name, 200, true); Text(effect.Source, 500); Text(effect.Note, 2000);
            if (effect.RemainingRounds is <= 0 or > 10000) throw new ArgumentException("Effect rounds must be positive or a manual duration.");
        }
        if (c.StandingAdjustments is null || c.StandingAdjustments.Count > 40 || c.StandingAdjustments.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != c.StandingAdjustments.Count
            || c.StandingAdjustments.Any(x => x.Value is < -100 or > 100 || !AdjustmentKey(x.Key))) throw new ArgumentException("Standing character adjustments contain an unsupported key or value.");
        StandingTerms(c);
        if (c.Features is null || c.Features.Count > 1000 || c.Features.Any(x => x is null || x.Length > 20000)) throw new ArgumentException("Feature descriptions are invalid or too large.");
        if (c.KnownTarget is null || c.KnownTarget.Knowledge is not ("unknown" or "recorded" or "none")) throw new ArgumentException("Target knowledge must be unknown, recorded, or none.");
        Text(c.KnownTarget.Name, 200);
        Defenses(c.Defenses); Defenses(c.KnownTarget.Defenses);
        if (c.KnownTarget.Knowledge is "unknown" or "none" && c.KnownTarget.Defenses.Count != 0) throw new ArgumentException("Unknown or confirmed-no-defense targets cannot contain recorded defenses.");
        SheetRules.Validate(c);
        AdventureRules.Validate(c);
        CombatRules.Validate(c);
        PlayValidation.Validate(c);
        if (c.Effects.Any(x => x.Concentration) && c.Effects.Any(x => WorkspaceRules.IsIncapacitatingCondition(x.Name))) throw new ArgumentException("Concentration cannot coexist with an incapacitating condition.");
        if (c.Effects.Count(x => x.Concentration) > 1) throw new ArgumentException("Only one concentration effect can be active.");
    }

    private static bool AdjustmentKey(string? key)
    {
        if (key is null || key.Length > 100) return false;
        if (key.Equals("initiative", StringComparison.OrdinalIgnoreCase)) return true;
        var split = key.Split(':', 2);
        if (split.Length != 2) return false;
        if (split[0].Equals("ability", StringComparison.OrdinalIgnoreCase) || split[0].Equals("save", StringComparison.OrdinalIgnoreCase))
            return Enum.TryParse<Ability>(split[1], ignoreCase: true, out var ability) && Enum.IsDefined(ability);
        return split[0].Equals("skill", StringComparison.OrdinalIgnoreCase) && new[] { "Acrobatics", "Animal Handling", "Arcana", "Athletics", "Deception", "History", "Insight", "Intimidation", "Investigation", "Medicine", "Nature", "Perception", "Performance", "Persuasion", "Religion", "Sleight of Hand", "Stealth", "Survival" }.Contains(split[1], StringComparer.OrdinalIgnoreCase);
    }

    internal static void StandingTerms(CharacterState c)
    {
        if(c.StandingAdjustmentTerms is null||c.StandingAdjustmentTerms.Count>40
            ||c.StandingAdjustmentTerms.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=c.StandingAdjustmentTerms.Count)
            throw new ArgumentException("Standing source terms are invalid or too large.");
        foreach(var pair in c.StandingAdjustmentTerms)
        {
            if(!AdjustmentKey(pair.Key)||pair.Value is null||pair.Value.Count is <1 or >20
                ||pair.Value.Any(term=>term is null||string.IsNullOrWhiteSpace(term.Label)||term.Label.Length>200||term.Value is <-100 or >100))
                throw new ArgumentException("Standing source terms contain an unsupported source.");
            var amount=c.StandingAdjustments.FirstOrDefault(value=>value.Key.Equals(pair.Key,StringComparison.OrdinalIgnoreCase));
            if(amount.Key is null||pair.Value.Sum(term=>(long)term.Value)!=amount.Value)
                throw new ArgumentException("Standing source terms must sum exactly to their adjustment.");
        }
    }

    private static void Defenses(List<DamageDefense>? defenses)
    {
        if (defenses is null || defenses.Count > 100) throw new ArgumentException("Defense lists are invalid or too large.");
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var defense in defenses)
        {
            if (defense is null || defense.Kind is not ("resistance" or "immunity" or "vulnerability" or "conditionImmunity")) throw new ArgumentException("That defense kind is not supported.");
            Text(defense.Type, 100, true); Text(defense.Source, 500); Text(defense.Note, 2000);
            if (!keys.Add(defense.Kind + ":" + defense.Type.Trim() + ":" + defense.Source.Trim())) throw new ArgumentException("Duplicate defenses do not stack.");
        }
    }

    private static void Text(string? text, int maximum, bool required = false)
    {
        if (text is null || text.Length > maximum || required && string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("A workspace text field is missing or exceeds its allowed length.");
    }

    private static void Unique(IEnumerable<string?> values)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in values)
        {
            Id(id!);
            if (!ids.Add(id!)) throw new ArgumentException("Workspace IDs must be unique within each collection.");
        }
    }
}
