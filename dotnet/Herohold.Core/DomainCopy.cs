namespace Herohold.Core;

public static class DomainCopy
{
    public static CharacterState Clone(CharacterState c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c with
        {
            Combat = System.Text.Json.JsonSerializer.Deserialize<CombatState>(System.Text.Json.JsonSerializer.Serialize(c.Combat))!,
            AbilityScores = new(c.AbilityScores), SaveProficiencies = [.. c.SaveProficiencies],
            SkillProficiencies = [.. c.SkillProficiencies], Expertise = [.. c.Expertise],
            Equipment = c.Equipment.Select(SheetRules.CloneItem).ToList(),
            Sheet = SheetRules.Clone(c.Sheet),
            Resources = c.Resources.Select(x => x with { }).ToList(), Notes = c.Notes.Select(x => x with { }).ToList(),
            Effects = c.Effects.Select(x => x with { }).ToList(),
            Play = System.Text.Json.JsonSerializer.Deserialize<PlayState>(System.Text.Json.JsonSerializer.Serialize(c.Play))!,
            Build = c.Build?.Copy(),
            Derived = System.Text.Json.JsonSerializer.Deserialize<DerivedSheet>(System.Text.Json.JsonSerializer.Serialize(c.Derived))!,
            StandingAdjustments = new(c.StandingAdjustments),
            StandingAdjustmentTerms = c.StandingAdjustmentTerms.ToDictionary(pair => pair.Key, pair => pair.Value.Select(term => term with { }).ToList()),
            Features = [.. c.Features], Defenses = c.Defenses.Select(x => x with { }).ToList(),
            KnownTarget = c.KnownTarget with { Defenses = c.KnownTarget.Defenses.Select(x => x with { }).ToList() }
        };
    }
}
