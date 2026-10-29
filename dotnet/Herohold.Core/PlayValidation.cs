using System.Text.Json;
using System.Text.Json.Nodes;

namespace Herohold.Core;

internal static class PlayValidation
{
    internal static void Validate(CharacterState c)
    {
        ClassPlayRules.Validate(c);
        var p = c.Play ?? throw new ArgumentException("Play state is missing.");
        if (!Enum.IsDefined(p.SpellcastingAbility) || p.HitDieSize is not (4 or 6 or 8 or 10 or 12)) throw new ArgumentException("Spellcasting ability or Hit Die size is invalid.");
        IdList(p.PreparedSpellIds, 500); IdList(p.KnownSpellIds, 5000); IdList(p.SpellbookIds, 5000); IdList(p.GrantedSpellIds, 500); IdList(p.CompletedOperationIds, 100);
        if (p.Spells is null || p.Spells.Count > 5000 || p.Spells.Any(x => x is null) || p.Spells.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != p.Spells.Count)
            throw new ArgumentException("Spell definitions are invalid or duplicated.");
        if(p.SpellNotes is null||p.SpellNotes.Count>5000)throw new ArgumentException("Personal spell notes are invalid.");
        foreach(var note in p.SpellNotes){WorkspaceValidation.Id(note.Key);Text(note.Value,4000);}
        foreach (var spell in p.Spells)
        {
            if(spell is null)throw new ArgumentException("Spell definitions cannot be null.");
            if(!Enum.IsDefined(spell.AttackPattern)||spell.AttackRollKind is not (null or "ranged" or "melee" or "mixed")||spell.AttackPattern!=SpellAttackPattern.Unmodeled&&spell.AttackRollKind is not ("ranged" or "melee"))throw new ArgumentException("Spell attack capability metadata is invalid.");
            WorkspaceValidation.Id(spell.Id);
            Text(spell.Name, 200); Text(spell.Description, 50000); Text(spell.Source, 200); Text(spell.CastingTime, 500); Text(spell.Range, 500);
            Text(spell.CastingSource,200);Text(spell.RulesSource,200);Text(spell.School,100);Text(spell.MaterialDescription,4000);Text(spell.DurationDescription,1000);
            if(spell.Components is null||spell.Components.Count>3||spell.Components.Distinct(StringComparer.Ordinal).Count()!=spell.Components.Count||spell.Components.Any(c=>c is not ("V" or "S" or "M"))||spell.MaterialCostCp is <0 or >1000000000)throw new ArgumentException("Spell component metadata is invalid.");
            if (spell.Level is < 0 or > 9 || !Enum.IsDefined(spell.EffectKind) || !Enum.IsDefined(spell.Scaling) || spell.SaveAbility is { } ability && !Enum.IsDefined(ability)
                || spell.DicePerSlot is < 0 or > 20 || spell.RadiusFt is < 0 or > 100000 || spell.RadiusPerSlot is < 0 or > 10000 || spell.DurationAmount is < 0 or > 100000)
                throw new ArgumentException("Spell execution metadata is outside its limits.");
            if (spell.CastingAbility is { } castingAbility && !Enum.IsDefined(castingAbility)) throw new ArgumentException("A granted spell has an invalid casting ability.");
            if (spell.GrantResourceId is not null) WorkspaceValidation.Id(spell.GrantResourceId);
            if (spell.Dice is not null) PlayDice.ValidateFormula(spell.Dice);
            Text(spell.DurationUnit, 100); Text(spell.Resolution, 100); Text(spell.Trigger, 100);
            if (spell.DamageType is not null) Text(spell.DamageType, 100);
            if (spell.CharacterLevelScaling is null || spell.CharacterLevelScaling.Count > 20 || spell.CharacterLevelScaling.Any(x => x is null || x.MinimumCharacterLevel is < 1 or > 20 || x.Dice is null)
                || spell.CharacterLevelScaling.Select(x => x.MinimumCharacterLevel).Distinct().Count() != spell.CharacterLevelScaling.Count)
                throw new ArgumentException("Spell character-level scaling is invalid.");
            foreach (var threshold in spell.CharacterLevelScaling) PlayDice.ValidateFormula(threshold.Dice);
            if (spell.DurationThresholds is null || spell.DurationThresholds.Count > 9 || spell.DurationThresholds.Any(x => x is null || x.MinimumSlotLevel is < 1 or > 9 || x.Amount is < 0 or > 100000 || x.Unit is null || x.Unit.Length > 100))
                throw new ArgumentException("Spell duration thresholds are invalid.");
        }
        var policy = p.Preparation ?? throw new ArgumentException("Preparation policy is missing.");
        IdList(policy.EligibleSpellIds, 5000);IdList(policy.OwnedCantripSpellIds,100);IdList(policy.EligibleCantripSpellIds,5000);
        if(policy.CantripReplacementLimit is <0 or >100)throw new ArgumentException("Cantrip replacement limit is invalid.");
        if (policy.PreparedLimit is < 0 or > 500 || policy.ReplacementLimit is < 0 or > 500) throw new ArgumentException("Preparation quotas are invalid.");
        Text(p.HunterTarget, 200);
        if (p.UndoHistory is null || p.UndoHistory.Count > 20) throw new ArgumentException("Play undo history is too large.");
        foreach (var entry in p.UndoHistory)
        {
            if (entry is null || entry.Before is null) throw new ArgumentException("Play undo history contains missing state.");
            WorkspaceValidation.Id(entry.OperationId); Text(entry.Description, 500);
            foreach (var old in entry.After is null ? new[] { entry.Before } : new[] { entry.Before, entry.After })
            {
                if (old.Exhaustion is < 0 or > 6 || old.CurrentHp is < 0 or > 100000 || old.TemporaryHp is < 0 or > 100000 || old.Turn < 1 || old.Resources is null || old.Resources.Count > 100 || old.Effects is null || old.Effects.Count > 100)
                    throw new ArgumentException("Play undo history contains invalid bounds.");
                IdList(old.KnownSpellIds??[],5000);IdList(old.OwnedCantripSpellIds??[],100);IdList(old.PreparedSpellIds, 500); Text(old.HunterTarget, 200);
                if (old.Resources.Select(x => x?.Id).Distinct(StringComparer.Ordinal).Count() != old.Resources.Count || old.Effects.Select(x => x?.Id).Distinct(StringComparer.Ordinal).Count() != old.Effects.Count)
                    throw new ArgumentException("Undo checkpoint identifiers must be unique.");
                foreach (var resource in old.Resources)
                {
                    if (resource is null) throw new ArgumentException("An undo resource is missing.");
                    WorkspaceValidation.Id(resource.Id); Text(resource.Name, 200);
                    if (resource.Maximum is < 0 or > 100000 || resource.Remaining < 0 || resource.Remaining > resource.Maximum || resource.SourceKind is not ("manual" or "build" or "grant") || !Enum.IsDefined(resource.Recovery) || resource.SlotLevel is < 1 or > 9 || resource.ShortRestRecoveryAmount is < 1 or > 100000)
                        throw new ArgumentException("An undo resource has invalid values.");
                }
                foreach (var effect in old.Effects)
                {
                    if (effect is null) throw new ArgumentException("An undo effect is missing.");
                    WorkspaceValidation.Id(effect.Id); Text(effect.Name, 200); Text(effect.Source, 500); Text(effect.Note, 2000);
                    if (effect.RemainingRounds is <= 0 or > 10000) throw new ArgumentException("An undo effect has invalid rounds.");
                }
            }
        }
        if (p.LastRoll is { } roll)
        {
            WorkspaceValidation.Id(roll.Id);if(roll.InspirationWindowId is {} rollWindow)WorkspaceValidation.Id(rollWindow);if(roll.BonusDieSides is <2 or >100)throw new ArgumentException("Recorded bonus die size is invalid.");Text(roll.Name, 200);
            if (!Enum.IsDefined(roll.AttackKind)||!Enum.IsDefined(roll.Kind) || !Enum.IsDefined(roll.Mode) || !Enum.IsDefined(roll.Advantage) || roll.D20 is null || roll.D20.Count is < 1 or > 2 || roll.D20.Any(x => x is < 1 or > 20)
                || !roll.D20.Contains(roll.ChosenD20) || roll.BonusDice is null || roll.BonusDice.Count > 100 || roll.BonusDice.Any(x => x is < 1 or > 100)
                || roll.StandingBonus is null || roll.StandingBonus.Terms is null || roll.StandingBonus.Terms.Count > 510 || roll.StandingBonus.Terms.Any(x => x is null || x.Label is null || x.Label.Length > 500 || x.Value is < -100000 or > 100000)
                || roll.StandingBonus.Total != roll.StandingBonus.Terms.Sum(x => x.Value) || roll.FlatBonus is < -100 or > 100 || roll.Total != roll.ChosenD20 + roll.StandingBonus.Total + roll.FlatBonus + roll.BonusDice.Sum())
                throw new ArgumentException("The recorded roll is invalid.");
            if (roll.Advantage == RollAdvantage.Normal && !roll.LuckyUsed && roll.D20.Count != 1
                || roll.Advantage != RollAdvantage.Normal && roll.D20.Count != 2
                || roll.LuckyUsed && (roll.D20.Count != 2 || roll.Advantage == RollAdvantage.Disadvantage)
                || roll.ChosenD20 != (roll.Advantage == RollAdvantage.Advantage ? roll.D20.Max() : roll.Advantage == RollAdvantage.Disadvantage ? roll.D20.Min() : roll.D20[0]))
                throw new ArgumentException("The recorded roll's dice do not match its advantage or Lucky state.");
            if (roll.Damage is null || roll.Damage.Count > 10) throw new ArgumentException("Recorded damage is invalid.");
            foreach (var term in roll.Damage)
            {
                if (term is null || term.Dice is null || term.Dice.Count > 100 || term.Dice.Any(x => x is < 1 or > 100) || term.Modifier is < -100 or > 100 || term.Total != Math.Max(0, term.Dice.Sum() + term.Modifier)) throw new ArgumentException("Recorded damage dice are invalid.");
                if(term.DieSides is <2 or >100)throw new ArgumentException("Recorded damage die size is invalid.");
                Text(term.Name, 200); Text(term.DamageType, 100);
            }
        }
        if (p.LastCast is { } cast)
        {
            WorkspaceValidation.Id(cast.Id);if(cast.InspirationWindowId is {} castWindow)WorkspaceValidation.Id(castWindow);Text(cast.Target, 200); Text(cast.PaymentResourceId, 100);
            if (!Enum.IsDefined(cast.Mode) || cast.Preview is null || cast.EffectDice is null || cast.EffectDice.Count > 100 || cast.EffectDice.Any(x => x is < 1 or > 100))
                throw new ArgumentException("The recorded cast is invalid.");
            var preview = cast.Preview;
            if(preview.AttackRollCount is <0 or >20||preview.AttackRollKind is not (null or "ranged" or "melee" or "mixed")||preview.AttackRollCount is >0&&preview.AttackRollKind is not ("ranged" or "melee"))throw new ArgumentException("Recorded spell attack capability is invalid.");
            WorkspaceValidation.Id(preview.SpellId); Text(preview.Name, 200); Text(preview.FormulaText, 200); Text(preview.Note, 2000); Text(preview.DurationText, 200); Text(preview.Resolution, 100); Text(preview.Trigger, 100);
            if (preview.SlotLevel is < 0 or > 9 || !Enum.IsDefined(preview.EffectKind) || preview.Modifier is < -100 or > 100 || preview.SaveAbility is { } save && !Enum.IsDefined(save)) throw new ArgumentException("Recorded cast metadata is invalid.");
            if (preview.CastingModifierCalculation is not null) Calculation(preview.CastingModifierCalculation);
            if (preview.SaveDcCalculation is not null)
            {
                Calculation(preview.SaveDcCalculation);
                if (preview.SaveDcCalculation.Total != preview.SaveDc) throw new ArgumentException("The recorded spell save DC does not match its breakdown.");
            }
            if (preview.Dice is not null) PlayDice.ValidateFormula(preview.Dice);
            if (cast.EffectTotal is { } total && (preview.Dice is null || cast.EffectDice.Count != preview.Dice.Count || cast.EffectDice.Any(x => x > preview.Dice.Sides) || total != Math.Max(0, cast.EffectDice.Sum() + preview.Modifier)))
                throw new ArgumentException("Recorded spell effect results are invalid.");
        }
        if (c.Build is not null)
        {
            var build = c.Build.ToCanonical(c.Id);
            ValidateNode(build, 0);
            if (System.Text.Encoding.UTF8.GetByteCount(build.ToJsonString()) > 1500000) throw new ArgumentException("The structured builder payload is too large.");
        }
    }

    private static void Calculation(Calculation calculation)
    {
        if (calculation.Terms is null || calculation.Terms.Count > 510 || calculation.Terms.Any(x => x is null || x.Label is null || x.Label.Length > 500 || x.Value is < -100000 or > 100000)
            || calculation.Total != calculation.Terms.Sum(x => x.Value)) throw new ArgumentException("A recorded calculation breakdown is invalid.");
    }

    private static void ValidateNode(JsonNode? node, int depth)
    {
        if (depth > 24) throw new ArgumentException("The structured builder payload is too deeply nested.");
        if (node is JsonObject obj) foreach (var child in obj) ValidateNode(child.Value, depth + 1);
        else if (node is JsonArray array) foreach (var child in array) ValidateNode(child, depth + 1);
    }

    private static void IdList(List<string>? ids, int maximum)
    {
        if (ids is null || ids.Count > maximum || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count) throw new ArgumentException("A play identifier list is missing, too large, or duplicated.");
        foreach (var id in ids) WorkspaceValidation.Id(id);
    }
    private static void Text(string? text, int maximum)
    {
        if (text is null || text.Length > maximum) throw new ArgumentException("A play text field is missing or too long.");
    }
}
