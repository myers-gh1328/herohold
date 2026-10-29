namespace Herohold.Core;

public static partial class PlayRules
{
    public static CharacterState AdjustHitDiePool(WorkspaceSnapshot w,HitDiePoolAdjustmentRequest r)
    {
        if(AlreadyCompleted(w,r))return DomainCopy.Clone(w.Character);
        WorkspaceValidation.Id(r.PoolId);
        return Commit(w.Character,HitDiePoolRules.Adjust(w.Character,r.PoolId,r.Delta),r,"Adjust Hit Dice "+r.PoolId);
    }
    public static CharacterState AdjustResource(WorkspaceSnapshot w, ResourceAdjustmentRequest r)
    {
        if (AlreadyCompleted(w, r)) return DomainCopy.Clone(w.Character);
        if(r.ResourceId=="hitdice"&&w.Character.Play.HitDiePools.Count>0)
        {
            if(w.Character.Play.HitDiePools.Count!=1)throw new ArgumentException("Choose which Hit Die pool to adjust; different die sizes are tracked separately.");
            return Commit(w.Character,HitDiePoolRules.Adjust(w.Character,w.Character.Play.HitDiePools[0].Id,r.Delta),r,"Adjust Hit Dice");
        }
        return Commit(w.Character, WorkspaceRules.AdjustResource(w.Character, r.ResourceId, r.Delta), r, "Adjust " + r.ResourceId);
    }
    public static CharacterState AdjustHp(WorkspaceSnapshot w, HpAdjustmentRequest r)
    {
        if (AlreadyCompleted(w, r)) return DomainCopy.Clone(w.Character);
        return Commit(w.Character, WorkspaceRules.AdjustHp(w.Character, r.Delta), r, "Adjust current HP");
    }
    public static CharacterState Rest(WorkspaceSnapshot w, RestRequest r, Func<int, int>? dice = null)
    {
        if (AlreadyCompleted(w, r)) return DomainCopy.Clone(w.Character);
        var preview = RestRules.Preview(w, r, dice);
        var c = DomainCopy.Clone(w.Character);
        c = c with
        {
            Exhaustion = r.Kind == RestKind.Long ? Math.Max(0, c.Exhaustion - 1) : c.Exhaustion,
            CurrentHp = preview.CurrentHp, TemporaryHp = r.Kind == RestKind.Long ? 0 : c.TemporaryHp,
            Resources = preview.Resources.Select(x => x with { }).ToList(),
            Effects = r.EndConcentration ? c.Effects.Where(x => !x.Concentration).ToList() : c.Effects,
            Play = c.Play with { SpellSources=ClassPlayRules.Copy(preview.SpellSources),HitDiePools=ClassPlayRules.Copy(preview.HitDiePools),KnownSpellIds=[..preview.KnownSpellIds],Preparation=c.Play.Preparation with{OwnedCantripSpellIds=[..preview.OwnedCantripSpellIds]},PreparedSpellIds = [.. preview.PreparedSpellIds], HunterTarget = r.EndConcentration ? string.Empty : c.Play.HunterTarget,
                LastCast = null, LastRoll = null, DreadUsedThisTurn = false }
        };
        return Commit(w.Character, c, r, r.Kind + " rest");
    }
    private static bool AlreadyCompleted(WorkspaceSnapshot w, PlayRequest r)
    {
        ArgumentNullException.ThrowIfNull(w); ArgumentNullException.ThrowIfNull(r);
        ValidateSourceCharacter(w, r);
        WorkspaceValidation.Id(r.OperationId);
        if (w.Character.Play.CompletedOperationIds.Contains(r.OperationId, StringComparer.Ordinal)) return true;
        if (w.Revision != r.SourceRevision) throw new WorkspaceConflictException("This play action is stale. Reload before trying again.");
        return false;
    }

    internal static void ValidateSourceCharacter(WorkspaceSnapshot w, PlayRequest r)
    {
        if (r.SourceCharacterId is null) throw new ArgumentException("The play action's source character is missing.");
        if (r.SourceCharacterId.Length == 0) return; // Compatibility for pre-identity internal callers; new UI always supplies the captured ID.
        WorkspaceValidation.Id(r.SourceCharacterId);
        if (!r.SourceCharacterId.Equals(w.Character.Id, StringComparison.Ordinal))
            throw new WorkspaceConflictException("This action belongs to another character. Reopen the action for the currently selected character.");
    }

    private static CharacterState RecordCompleted(CharacterState before, CharacterState after, PlayRequest request)
        => after with { Play = after.Play with { CompletedOperationIds = [.. before.Play.CompletedOperationIds.Append(request.OperationId).TakeLast(100)] } };

    private static CharacterState Commit(CharacterState before, CharacterState after, PlayRequest request, string description)
    {
        var history = before.Play.UndoHistory.Append(new PlayUndoEntry
        {
            OperationId = request.OperationId, Description = description,
            Before = Capture(before), After = Capture(after)
        }).TakeLast(20).ToList();
        return after with { Play = after.Play with { UndoHistory = history, CompletedOperationIds = [.. before.Play.CompletedOperationIds.Append(request.OperationId).TakeLast(100)] } };
    }
}
