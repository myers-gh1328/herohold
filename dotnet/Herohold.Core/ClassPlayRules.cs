using System.Text.Json;
namespace Herohold.Core;

/// <summary>Source ownership and mixed Hit Dice are authoritative only when explicitly present.</summary>
public static class ClassPlayRules
{
    internal static T Copy<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    public static SpellSourceState? SpellSource(CharacterState c,string spellId)
    {
        var owners=c.Play.SpellSources.Where(s=>Owned(s).Contains(spellId,StringComparer.Ordinal)).ToArray();
        if(owners.Length>1)throw new ArgumentException("A spell ownership ID belongs to more than one source.");
        return owners.SingleOrDefault();
    }
    private static IEnumerable<string> Owned(SpellSourceState s)=>s.PreparedSpellIds.Concat(s.KnownSpellIds).Concat(s.SpellbookIds).Concat(s.GrantedSpellIds).Concat(s.Preparation.EligibleSpellIds).Concat(s.Preparation.EligibleCantripSpellIds).Concat(s.Preparation.OwnedCantripSpellIds).Distinct(StringComparer.Ordinal);
    public static CharacterState SourceView(CharacterState c,string sourceId)
    {
        var s=c.Play.SpellSources.SingleOrDefault(s=>s.Id==sourceId)??throw new ArgumentException("Choose an existing spell source.");
        return c with{Play=c.Play with{SpellcastingAbility=s.Ability,Preparation=Copy(s.Preparation),PreparedSpellIds=[..s.PreparedSpellIds],KnownSpellIds=[..s.KnownSpellIds],SpellbookIds=[..s.SpellbookIds],GrantedSpellIds=[..s.GrantedSpellIds],SpellSources=[]}};
    }
    internal static RestPreview PreviewRest(WorkspaceSnapshot w,RestRequest r,Func<int,int>? dice)
    {
        ArgumentNullException.ThrowIfNull(w);ArgumentNullException.ThrowIfNull(r);
        if(r.SourceChanges is null||r.HitDieSpends is null||r.SourceChanges.Count>50||r.HitDieSpends.Count>20)throw new ArgumentException("Rest choices exceed their bounds.");
        var c=w.Character;var explicitSources=c.Play.SpellSources.Count>0;var explicitPools=c.Play.HitDiePools.Count>0;
        if(!explicitSources&&!explicitPools){if(r.SourceChanges.Count>0||r.HitDieSpends.Count>0)throw new ArgumentException("This sheet has no matching source or Hit Die pool.");return RestRules.LegacyPreview(w,r,dice);}
        Validate(c);
        PlayRules.ValidateSourceCharacter(w,r);WorkspaceValidation.Id(r.OperationId);
        if(w.Revision!=r.SourceRevision)throw new WorkspaceConflictException("This rest draft is stale. Reload and start a fresh rest.");
        if(r.SourceChanges.Count>0&&(r.ChangeSpells||r.ChangeCantrips||r.NewPreparedSpellIds.Count>0||r.NewClassCantripSpellIds.Count>0))throw new ArgumentException("Choose source-specific or legacy spell changes, not both.");
        if(r.HitDieSpends.Count>0&&(r.HitDiceToSpend!=0||r.PhysicalHitDice.Count>0))throw new ArgumentException("Choose pool-specific or legacy Hit Dice, not both.");
        var changes=r.SourceChanges.ToList();
        if(explicitSources&&(r.ChangeSpells||r.ChangeCantrips))
        {
            var eligible=c.Play.SpellSources.Where(s=>(!r.ChangeSpells||s.Preparation.CanChangeOnLongRest)&&(!r.ChangeCantrips||s.Preparation.CanChangeCantripsOnLongRest)).ToArray();
            if(eligible.Length!=1)throw new ArgumentException("Choose which spell source to change.");
            changes.Add(new(){SpellSourceId=eligible[0].Id,ChangeSpells=r.ChangeSpells,NewPreparedSpellIds=r.NewPreparedSpellIds,ChangeCantrips=r.ChangeCantrips,NewClassCantripSpellIds=r.NewClassCantripSpellIds});
        }
        if(changes.Select(x=>x.SpellSourceId).Distinct().Count()!=changes.Count)throw new ArgumentException("Change each spell source at most once.");
        var sources=Copy(c.Play.SpellSources);
        foreach(var change in changes)
        {
            var view=SourceView(c,change.SpellSourceId);
            var sourcePreview=RestRules.LegacyPreview(w with{Character=view},r with{SourceChanges=[],HitDieSpends=[],HitDiceToSpend=0,PhysicalHitDice=[],ChangeSpells=change.ChangeSpells,NewPreparedSpellIds=change.NewPreparedSpellIds,ChangeCantrips=change.ChangeCantrips,NewClassCantripSpellIds=change.NewClassCantripSpellIds},null);
            var index=sources.FindIndex(s=>s.Id==change.SpellSourceId);var source=sources[index];
            sources[index]=source with{PreparedSpellIds=sourcePreview.PreparedSpellIds,KnownSpellIds=sourcePreview.KnownSpellIds,Preparation=source.Preparation with{OwnedCantripSpellIds=sourcePreview.OwnedCantripSpellIds}};
        }
        var spends=r.HitDieSpends.ToList();
        if(explicitPools&&(r.HitDiceToSpend!=0||r.PhysicalHitDice.Count>0))
        {
            if(c.Play.HitDiePools.Count!=1)throw new ArgumentException("Choose which Hit Die pool to spend.");
            spends.Add(new(){PoolId=c.Play.HitDiePools[0].Id,Count=r.HitDiceToSpend,PhysicalDice=[..r.PhysicalHitDice]});
        }
        if(spends.Select(s=>s.PoolId).Distinct().Count()!=spends.Count)throw new ArgumentException("Spend each Hit Die pool once per draft.");
        foreach(var spend in spends)
        {
            var pool=c.Play.HitDiePools.SingleOrDefault(p=>p.Id==spend.PoolId)??throw new ArgumentException("Choose an existing Hit Die pool.");
            if(r.Kind!=RestKind.Short||spend.Count<0||spend.Count>pool.Remaining)throw new ArgumentException("Choose available Hit Dice for a Short Rest.");
            if(spend.PhysicalDice is null||r.Mode==DiceMode.Physical&&(spend.PhysicalDice.Count!=spend.Count||spend.PhysicalDice.Any(n=>n<1||n>pool.Sides))||r.Mode==DiceMode.Digital&&spend.PhysicalDice.Count>0)throw new ArgumentException("Enter one valid unmodified result per selected Hit Die.");
        }
        // All source/pool/dice validation completes before any random value is generated.
        var baseline=RestRules.LegacyPreview(w,r with{SourceChanges=[],HitDieSpends=[],HitDiceToSpend=explicitPools?0:r.HitDiceToSpend,PhysicalHitDice=explicitPools?[]:r.PhysicalHitDice,ChangeSpells=explicitSources?false:r.ChangeSpells,NewPreparedSpellIds=explicitSources?[]:r.NewPreparedSpellIds,ChangeCantrips=explicitSources?false:r.ChangeCantrips,NewClassCantripSpellIds=explicitSources?[]:r.NewClassCantripSpellIds},dice);
        var pools=Copy(c.Play.HitDiePools);var results=new List<HitDieSpendResult>();var hp=baseline.CurrentHp;
        foreach(var spend in spends)
        {
            var index=pools.FindIndex(p=>p.Id==spend.PoolId);var pool=pools[index];var values=PlayDice.Roll(new(spend.Count,pool.Sides),r.Mode,spend.PhysicalDice,dice);
            var healing=values.Sum(n=>Math.Max(1,n+CharacterCalculations.Ability(c,Ability.Constitution).Total));hp=Math.Min(c.MaximumHp,hp+healing);
            pools[index]=pool with{Remaining=pool.Remaining-spend.Count};results.Add(new(){PoolId=pool.Id,Dice=values,Healing=healing});
        }
        if(r.Kind==RestKind.Long)pools=pools.Select(p=>p with{Remaining=p.Maximum}).ToList();
        var resources=baseline.Resources;
        if(explicitPools){var i=resources.FindIndex(p=>p.Id=="hitdice");if(i>=0)resources[i]=resources[i] with{Maximum=pools.Sum(p=>p.Maximum),Remaining=pools.Sum(p=>p.Remaining)};}
        return baseline with{CurrentHp=hp,SpellSources=sources,HitDiePools=pools,HitDieSpendResults=results,HitDiceSpent=explicitPools?spends.Sum(s=>s.Count):baseline.HitDiceSpent,HitDice=explicitPools?results.SelectMany(x=>x.Dice).ToList():baseline.HitDice,
            PreparedSpellIds=explicitSources?sources.SelectMany(s=>s.PreparedSpellIds).Distinct().ToList():baseline.PreparedSpellIds,KnownSpellIds=explicitSources?sources.SelectMany(s=>s.KnownSpellIds).Distinct().ToList():baseline.KnownSpellIds};
    }
    internal static void Validate(CharacterState c)
    {
        var p=c.Play;
        if(p.SpellSources is null||p.SpellSources.Count>50||p.HitDiePools is null||p.HitDiePools.Count>20)throw new ArgumentException("Class play state exceeds its bounds.");
        if(p.SpellSources.Select(s=>s.Id).Distinct().Count()!=p.SpellSources.Count||p.HitDiePools.Select(s=>s.Id).Distinct().Count()!=p.HitDiePools.Count)throw new ArgumentException("Source and Hit Die pool IDs must be unique.");
        var owners=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var s in p.SpellSources)
        {
            if(string.IsNullOrWhiteSpace(s.Id)||s.Id.Length>200||s.Id.Any(char.IsControl))throw new ArgumentException("A spell source identity is invalid.");if(s.ClassTrackId is not null)WorkspaceValidation.Id(s.ClassTrackId);
            if(s.Name is null||s.Name.Length>200||s.SourceKind is null||s.SourceKind.Length>100||!Enum.IsDefined(s.Ability)||s.Preparation is null||s.Preparation.MaximumSpellLevel is null or <0 or >9)throw new ArgumentException("A spell source needs a bounded class access policy.");
            var policy=s.Preparation;
            if(policy.PreparedLimit is <0 or >500||policy.ReplacementLimit is <0 or >500||policy.CantripReplacementLimit is <0 or >100)throw new ArgumentException("Source preparation limits are invalid.");
            foreach(var list in new[]{s.PreparedSpellIds,s.KnownSpellIds,s.SpellbookIds,s.GrantedSpellIds,policy.EligibleSpellIds,policy.EligibleCantripSpellIds,policy.OwnedCantripSpellIds})
            {if(list is null||list.Count>5000||list.Distinct().Count()!=list.Count)throw new ArgumentException("Source spell memberships are invalid.");foreach(var id in list)WorkspaceValidation.Id(id);}
            foreach(var id in Owned(s)){if(owners.TryGetValue(id,out var old)&&old!=s.Id)throw new ArgumentException("Each owned spell ID must belong to exactly one source.");owners[id]=s.Id;}
        }
        foreach(var pool in p.HitDiePools)
        {WorkspaceValidation.Id(pool.Id);if(pool.Sides is not(6 or 8 or 10 or 12)||pool.Maximum is <1 or >20||pool.Remaining<0||pool.Remaining>pool.Maximum||pool.ClassTrackIds is null||pool.ClassTrackIds.Count>20||pool.ClassTrackIds.Distinct().Count()!=pool.ClassTrackIds.Count)throw new ArgumentException("A Hit Die pool is invalid.");foreach(var id in pool.ClassTrackIds)WorkspaceValidation.Id(id);}
        if(p.HitDiePools.Sum(p=>p.Maximum)>20)throw new ArgumentException("Total Hit Dice cannot exceed twenty.");
    }
}
