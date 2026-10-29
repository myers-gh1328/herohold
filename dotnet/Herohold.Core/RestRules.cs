namespace Herohold.Core;

public static class RestRules
{
    public static RestPreview Preview(WorkspaceSnapshot w, RestRequest r, Func<int,int>? dice=null)
    {
        if(w.Character.Combat.EncounterActive)throw new ArgumentException("End the encounter before taking a rest.");
        return ClassPlayRules.PreviewRest(w,r,dice);
    }
    internal static RestPreview LegacyPreview(WorkspaceSnapshot w, RestRequest r, Func<int, int>? dice = null)
    {
        ArgumentNullException.ThrowIfNull(w); ArgumentNullException.ThrowIfNull(r);
        PlayRules.ValidateSourceCharacter(w, r);
        WorkspaceValidation.Id(r.OperationId);
        if (w.Revision != r.SourceRevision) throw new WorkspaceConflictException("This rest draft is stale. Reload and start a fresh rest.");
        if (!Enum.IsDefined(r.Kind) || !Enum.IsDefined(r.Mode)) throw new ArgumentException("Choose a valid rest and dice mode.");
        var c = w.Character;
        if (c.CurrentHp == 0) throw new ArgumentException("A Short or Long Rest must start with at least 1 Hit Point. Resolve recovery from zero HP first.");
        var prepared = Preparation(c, r);
        var (known,owned)=CantripSelection(c,r);
        var resources = c.Resources.Select(x => x with { }).ToList();
        var hp = c.CurrentHp;
        var values = new List<int>();
        if (r.Kind == RestKind.Short)
        {
            var hitDice = resources.SingleOrDefault(x => x.Id == "hitdice");
            if (r.HitDiceToSpend < 0 || r.HitDiceToSpend > (hitDice?.Remaining ?? 0)) throw new ArgumentException("You cannot spend more Hit Dice than remain.");
            if (r.HitDiceToSpend > 0)
            {
                values = PlayDice.Roll(new(r.HitDiceToSpend, c.Play.HitDieSize), r.Mode, r.PhysicalHitDice, dice);
                var con = CharacterCalculations.Ability(c, Ability.Constitution).Total;
                hp = Math.Min(c.MaximumHp, hp + values.Sum(x => Math.Max(1, x + con)));
                var index = resources.FindIndex(x => x.Id == "hitdice");
                resources[index] = hitDice! with { Remaining = hitDice.Remaining - r.HitDiceToSpend };
            }
            else if (r.PhysicalHitDice.Count != 0) throw new ArgumentException("Enter no Hit Die results when spending zero Hit Dice.");
        }
        else
        {
            if (r.HitDiceToSpend != 0 || r.PhysicalHitDice.Count != 0) throw new ArgumentException("A long rest recovers Hit Dice; do not spend them in this draft.");
            hp = c.MaximumHp;
        }
        for (var i = 0; i < resources.Count; i++)
        {
            var pool = resources[i];
            var recover = r.Kind == RestKind.Long ? pool.Id == "hitdice" || pool.Recovery is RecoveryRest.Short or RecoveryRest.Long
                : pool.Id != "hitdice" && pool.Recovery == RecoveryRest.Short;
            if (recover) resources[i] = pool with { Remaining = r.Kind == RestKind.Short && pool.ShortRestRecoveryAmount is { } amount
                ? Math.Min(pool.Maximum, pool.Remaining + amount) : pool.Maximum };
        }
        if(r.ChangeCantrips)prepared.RemoveAll(id=>c.Play.Preparation.OwnedCantripSpellIds.Contains(id)&&!owned.Contains(id));
        return new()
        {
            Exhaustion = r.Kind == RestKind.Long ? Math.Max(0, c.Exhaustion - 1) : c.Exhaustion,
            CurrentHp = hp, MaximumHp = c.MaximumHp, HitDiceSpent = r.HitDiceToSpend, HitDice = values, Resources = resources,
            PreparedSpellIds = prepared, KnownSpellIds=known,OwnedCantripSpellIds=owned,EndsConcentration = r.EndConcentration,
            Note = r.Kind == RestKind.Short ? "Only selected Hit Dice and short-rest pools recover; daily pools stay spent."
                : "Recover HP, all Hit Dice, and eligible daily pools. Existing conditions remain unless explicitly ended."
        };
    }

    public static IReadOnlyList<string> EligiblePreparations(CharacterState c,string sourceId)=>EligiblePreparations(ClassPlayRules.SourceView(c,sourceId));
    public static IReadOnlyList<string> EligibleCantrips(CharacterState c,string sourceId)=>EligibleCantrips(ClassPlayRules.SourceView(c,sourceId));
    public static IReadOnlyList<string> EligiblePreparations(CharacterState c)
    {
        if(c.Play.SpellSources.Count>0){var sources=c.Play.SpellSources.Where(s=>s.Preparation.CanChangeOnLongRest).ToArray();if(sources.Length>1)throw new ArgumentException("Choose a spell source for its eligible preparations.");return sources.Length==0?[]:EligiblePreparations(c,sources[0].Id);}
        var policy=c.Play.Preparation;var ids=policy.EligibleSpellIds.ToHashSet(StringComparer.Ordinal);
        if(policy.FromSpellbook){if(ids.Count==0)ids.UnionWith(c.Play.SpellbookIds);else ids.IntersectWith(c.Play.SpellbookIds);}
        else if(ids.Count==0)ids.UnionWith(c.Play.KnownSpellIds.Concat(c.Play.PreparedSpellIds));
        var maxSlot=policy.MaximumSpellLevel??c.Resources.Where(x=>x.Maximum>0).Select(x=>x.SlotLevel??(x.Id.StartsWith("slot",StringComparison.Ordinal)&&int.TryParse(x.Id.AsSpan(4),out var level)?level:0)).DefaultIfEmpty(0).Max();
        return ids.Where(id=>!c.Play.GrantedSpellIds.Contains(id)).Where(id=>Find(c,id) is {} spell&&spell.Level>0&&spell.Level<=maxSlot).OrderBy(id=>Find(c,id)!.Name,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static IReadOnlyList<string> EligibleCantrips(CharacterState c)
    {
        if(c.Play.SpellSources.Count>0){var sources=c.Play.SpellSources.Where(s=>s.Preparation.CanChangeCantripsOnLongRest).ToArray();if(sources.Length>1)throw new ArgumentException("Choose a spell source for its eligible cantrips.");return sources.Length==0?[]:EligibleCantrips(c,sources[0].Id);}
        return c.Play.Preparation.EligibleCantripSpellIds.Where(id=>Find(c,id)?.Level==0).OrderBy(id=>Find(c,id)!.Name,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static SpellDefinition? Find(CharacterState c,string id)=>c.Play.Spells.FirstOrDefault(s=>s.Id==id)??SpellCatalog.All.FirstOrDefault(s=>s.Id==id);
    private static (List<string> Known,List<string> Owned) CantripSelection(CharacterState c,RestRequest r)
    {
        var policy=c.Play.Preparation;var owned=policy.OwnedCantripSpellIds;
        if(!r.ChangeCantrips)return ([..c.Play.KnownSpellIds],[..owned]);
        if(r.Kind!=RestKind.Long||!policy.CanChangeCantripsOnLongRest||policy.CantripReplacementLimit<1)throw new ArgumentException("This spell source does not allow cantrip replacement on this rest.");
        if(r.NewClassCantripSpellIds is null||r.NewClassCantripSpellIds.Count!=owned.Count||r.NewClassCantripSpellIds.Distinct(StringComparer.Ordinal).Count()!=owned.Count||owned.Any(id=>!c.Play.KnownSpellIds.Contains(id)))throw new ArgumentException("Replace source-owned cantrips while preserving the number known.");
        var eligible=EligibleCantrips(c).ToHashSet(StringComparer.Ordinal);
        if(r.NewClassCantripSpellIds.Any(id=>!eligible.Contains(id)))throw new ArgumentException("Choose cantrips from this source's eligible list.");
        if(owned.Except(r.NewClassCantripSpellIds,StringComparer.Ordinal).Count()>policy.CantripReplacementLimit||r.NewClassCantripSpellIds.Except(owned,StringComparer.Ordinal).Count()>policy.CantripReplacementLimit)throw new ArgumentException($"Replace at most {policy.CantripReplacementLimit} cantrip(s) from this source.");
        var other=c.Play.KnownSpellIds.Except(owned,StringComparer.Ordinal);
        return (other.Concat(r.NewClassCantripSpellIds).Distinct(StringComparer.Ordinal).ToList(),[..r.NewClassCantripSpellIds]);
    }
    private static List<string> Preparation(CharacterState c, RestRequest r)
    {
        if (!r.ChangeSpells) return [.. c.Play.PreparedSpellIds];
        if (r.Kind != RestKind.Long) throw new ArgumentException("Spell preparation changes require a long rest.");
        var policy = c.Play.Preparation;
        if (!policy.CanChangeOnLongRest) throw new ArgumentException("This class or source does not allow long-rest spell changes.");
        if (r.NewPreparedSpellIds is null || r.NewPreparedSpellIds.Count > policy.PreparedLimit || r.NewPreparedSpellIds.Count != r.NewPreparedSpellIds.Distinct(StringComparer.Ordinal).Count())
            throw new ArgumentException("Prepared spells exceed their quota or contain duplicates.");
        var eligible = policy.EligibleSpellIds.ToHashSet(StringComparer.Ordinal);
        if (policy.FromSpellbook)
        {
            var book = c.Play.SpellbookIds.ToHashSet(StringComparer.Ordinal);
            if (eligible.Count == 0) eligible = book;
            else eligible.IntersectWith(book);
        }
        if (eligible.Count == 0 && !policy.FromSpellbook) eligible = c.Play.KnownSpellIds.Concat(c.Play.PreparedSpellIds).ToHashSet(StringComparer.Ordinal);
        var maxSlot = policy.MaximumSpellLevel ?? c.Resources.Where(x => x.Maximum > 0).Select(x => x.SlotLevel ?? (x.Id.StartsWith("slot", StringComparison.Ordinal) && int.TryParse(x.Id.AsSpan(4), out var level) ? level : 0)).DefaultIfEmpty(0).Max();
        foreach (var id in r.NewPreparedSpellIds)
        {
            if (!eligible.Contains(id) || c.Play.GrantedSpellIds.Contains(id)) throw new ArgumentException("Choose eligible ordinary preparations; granted spells remain locked outside this list.");
            var spell = SpellCatalog.Find(c, id);
            if (spell.Level == 0 || spell.Level > maxSlot) throw new ArgumentException("That spell is not eligible for the character's preparation level.");
        }
        if (policy.ReplacementLimit is { } limit)
        {
            var removed = c.Play.PreparedSpellIds.Except(r.NewPreparedSpellIds, StringComparer.Ordinal).Count();
            var added = r.NewPreparedSpellIds.Except(c.Play.PreparedSpellIds, StringComparer.Ordinal).Count();
            if (removed > limit || added > limit || r.NewPreparedSpellIds.Count != c.Play.PreparedSpellIds.Count)
                throw new ArgumentException($"This class allows at most {limit} replacement(s), preserving the number prepared.");
        }
        return [.. r.NewPreparedSpellIds];
    }
}
