namespace Herohold.Core;

public static partial class PlayRules
{
    public static CharacterState RetargetHuntersMark(WorkspaceSnapshot w, HunterMarkRetargetRequest r)
    {
        if (AlreadyCompleted(w, r)) return DomainCopy.Clone(w.Character);
        if (string.IsNullOrWhiteSpace(r.Target) || r.Target.Length > 200) throw new ArgumentException("Name the new marked creature within sight and the spell's range.");
        if (!r.PreviousTargetAtZeroHp) throw new ArgumentException("Confirm that the previous marked creature dropped to zero HP before moving Hunter’s Mark.");
        var c = w.Character;
        if (string.IsNullOrWhiteSpace(c.Play.HunterTarget) || !c.Effects.Any(effect => effect.Concentration && effect.Source == "spell:hunters-mark"))
            throw new ArgumentException("Moving Hunter’s Mark requires your existing marked target and active concentration.");
        if (c.Play.HunterTarget.Equals(r.Target.Trim(), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose a new marked creature.");
        var after = CombatRules.PrepareRetarget(c,r);
        after = after with { Play = after.Play with { HunterTarget = r.Target.Trim() } };
        return Commit(c, after, r, "Move Hunter’s Mark with a Bonus Action");
    }
    public static CharacterState Cast(WorkspaceSnapshot w, CastRequest r)
    {
        if (AlreadyCompleted(w, r)) return DomainCopy.Clone(w.Character);
        if (!Enum.IsDefined(r.Mode)) throw new ArgumentException("Choose digital or physical dice.");
        var c = w.Character;
        var definition = SpellCatalog.Find(c, r.SpellId);
        if (r.Ritual && (!definition.Ritual || definition.Level == 0 || r.SlotLevel != definition.Level))
            throw new ArgumentException("Only eligible ritual spells can be cast as rituals, at their base level.");
        if (definition.RitualOnly && !r.Ritual) throw new ArgumentException("This source grants ritual casting only.");
        var owner=ClassPlayRules.SpellSource(c,r.SpellId);
        var access=owner is null?c:ClassPlayRules.SourceView(c,owner.Id);
        if(c.Play.SpellSources.Count>0&&owner is null)throw new ArgumentException("That spell has no recorded casting source.");
        var wizardBookRitual = r.Ritual && definition.Ritual && (owner?.Preparation.FromSpellbook??c.ClassName.Equals("Wizard", StringComparison.OrdinalIgnoreCase)) && access.Play.SpellbookIds.Contains(r.SpellId);
        if (!(wizardBookRitual || definition.Level == 0 && access.Play.KnownSpellIds.Contains(r.SpellId) || access.Play.PreparedSpellIds.Contains(r.SpellId) || access.Play.GrantedSpellIds.Contains(r.SpellId)
            || !access.Play.Preparation.CanChangeOnLongRest && access.Play.KnownSpellIds.Contains(r.SpellId)))
            throw new ArgumentException("That spell is not currently prepared, granted, or known for casting.");
        var preview = SpellRules.Preview(c, r.SpellId, r.SlotLevel);
        if (r.Target is null || r.Target.Length > 200) throw new ArgumentException("The target name is too long.");
        if (preview.Trigger == "ranged-weapon-hit" && !r.QualifyingHit)
            throw new ArgumentException("Confirm the qualifying ranged-weapon hit before casting this spell.");
        if (preview.Concentration && !r.ConfirmConcentration)
            throw new ArgumentException("Confirm that you will maintain concentration; a new concentration spell replaces the old one.");
        var castingConcentration = SpellRules.RequiresCastingConcentration(definition, r.Ritual);
        if (castingConcentration && (c.CurrentHp == 0 || c.Effects.Any(effect => WorkspaceRules.IsIncapacitatingCondition(effect.Name))))
            throw new ArgumentException("Casting this spell requires Concentration, which cannot be maintained while incapacitated.");
        var after = CombatRules.PrepareCast(c,r);
        var noPayment = string.IsNullOrEmpty(r.PaymentResourceId) || r.PaymentResourceId == "none";
        if (r.SlotLevel == 0)
        {
            if (!noPayment) throw new ArgumentException("Cantrips do not spend spell slots.");
        }
        else if (noPayment)
        {
            if (r.SlotLevel != definition.Level || !(r.Ritual && definition.Ritual || definition.AtWill && access.Play.GrantedSpellIds.Contains(r.SpellId)))
                throw new ArgumentException("Free at-will or ritual casting requires an eligible source and its base spell level.");
        }
        else
        {
            if (r.Ritual) throw new ArgumentException("Ritual casting does not spend a spell slot or feature use.");
            var pool = after.Resources.SingleOrDefault(x => x.Id == r.PaymentResourceId) ?? throw new ArgumentException("Choose an available spell slot or eligible feature use.");
            if (!string.IsNullOrEmpty(definition.GrantResourceId) && pool.Id == definition.GrantResourceId)
            {
                if (!access.Play.GrantedSpellIds.Contains(r.SpellId) || r.SlotLevel != definition.Level)
                    throw new ArgumentException("That granted spell's free use applies only to its base-level effect.");
            }
            else if (pool.Id == "hunters")
            {
                if (definition.Id != "hunters-mark" || preview.Resolution != "marked-attack-hit" || r.SlotLevel != 1) throw new ArgumentException("Free Hunter’s Mark casts use the base first-level effect only.");
            }
            else
            {
                if (!definition.AllowsSlotCasting) throw new ArgumentException("This spell source does not allow spell-slot casting.");
                var level = pool.SlotLevel ?? (pool.Id.StartsWith("slot", StringComparison.Ordinal) && int.TryParse(pool.Id.AsSpan(4), out var parsed) ? parsed : -1);
                if (level != r.SlotLevel) throw new ArgumentException("The selected resource is not a spell slot of that level.");
            }
            after = WorkspaceRules.AdjustResource(after, pool.Id, -1);
        }
        if (preview.Resolution == "marked-attack-hit" && string.IsNullOrWhiteSpace(r.Target)) throw new ArgumentException("Name the marked target before confirming Hunter’s Mark.");
        if (castingConcentration && !preview.Concentration)
            after = after with { Effects = after.Effects.Where(effect => !effect.Concentration).ToList(), Play = after.Play with { HunterTarget = string.Empty } };
        if (preview.Concentration)
        {
            after = WorkspaceRules.AddEffect(after, new() { Id = Guid.NewGuid().ToString("N"), Name = preview.Name, Source = preview.Resolution == "marked-attack-hit" ? "spell:hunters-mark" : "spell:" + preview.SpellId,
                Note = preview.DurationText + ". End on concentration loss or duration expiry.", Concentration = true });
            after = after with { Play = after.Play with { HunterTarget = preview.Resolution == "marked-attack-hit" ? r.Target.Trim() : string.Empty } };
        }
        after = after with { Play = after.Play with { LastCast = new() { Id = r.OperationId, Preview = preview, Mode = r.Mode, Ritual = r.Ritual, Target = r.Target.Trim(), PaymentResourceId = r.PaymentResourceId } } };
        after=CombatRules.FinalizeCast(after,r);
        return Commit(c, after, r, "Cast " + preview.Name);
    }

    public static CharacterState ResolveCastEffect(WorkspaceSnapshot w, CastEffectRequest r, Func<int, int>? dice = null)
    {
        if (AlreadyCompleted(w, r)) return DomainCopy.Clone(w.Character);
        var cast = w.Character.Play.LastCast;
        if (cast is null || cast.Id != r.CastId || cast.EffectTotal is not null) throw new ArgumentException("That cast is no longer awaiting an effect roll.");
        if(cast.Preview.AttackRollCount is>0)throw new ArgumentException("Roll each spell attack and confirm its hit before resolving its per-hit damage.");
        if (cast.Preview.Dice is null || cast.Preview.Resolution == "marked-attack-hit") throw new ArgumentException("This spell has no immediate numeric effect to roll; resolve its rules at the table.");
        var values = PlayDice.Roll(cast.Preview.Dice, cast.Mode, r.PhysicalDice, dice);
        var after = DomainCopy.Clone(w.Character);
        after = after with { Play = after.Play with { LastCast = cast with { InspirationWindowId=r.OperationId,EffectDice = values, EffectTotal = Math.Max(0, values.Sum() + cast.Preview.Modifier) } } };
        return RecordCompleted(w.Character, after, r);
    }
}
