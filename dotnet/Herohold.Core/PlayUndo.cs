namespace Herohold.Core;

public static partial class PlayRules
{
    public static CharacterState Undo(WorkspaceSnapshot w, UndoRequest r)
    {
        if (AlreadyCompleted(w, r)) return DomainCopy.Clone(w.Character);
        var c = DomainCopy.Clone(w.Character);
        if (c.Play.UndoHistory.Count == 0) throw new ArgumentException("There is no play action to undo.");
        var entry = c.Play.UndoHistory[^1];
        var before = entry.Before;
        var after = entry.After ?? throw new WorkspaceConflictException("This older action has no safe undo checkpoint. The current character has been preserved.");
        var explicitHitDice=c.Play.HitDiePools.Count>0&&before.HitDiePools?.Count>0&&after.HitDiePools?.Count>0;
        if(c.Play.HitDiePools.Count>0&&!explicitHitDice&&!Equals(before.Resources.FirstOrDefault(p=>p.Id=="hitdice"),after.Resources.FirstOrDefault(p=>p.Id=="hitdice")))throw new WorkspaceConflictException("This older Hit Dice adjustment lacks pool provenance; the current counters have been preserved.");
        var candidate = c with
        {
            Combat=RestoreCombat(c.Combat,before.Combat,after.Combat),
            CurrentHp = Restore("current HP", c.CurrentHp, before.CurrentHp, after.CurrentHp),
            Sheet = c.Sheet with{
                DeathSaves=before.DeathSaves is null||after.DeathSaves is null?c.Sheet.DeathSaves:Restore("death saves",c.Sheet.DeathSaves,before.DeathSaves,after.DeathSaves),
                HeroicInspiration=before.HeroicInspiration is null||after.HeroicInspiration is null?c.Sheet.HeroicInspiration:Restore("Heroic Inspiration",SheetRules.HasHeroicInspiration(c),before.HeroicInspiration.Value,after.HeroicInspiration.Value)},
            TemporaryHp = Restore("temporary HP", c.TemporaryHp, before.TemporaryHp, after.TemporaryHp),
            Exhaustion = Restore("exhaustion", c.Exhaustion, before.Exhaustion, after.Exhaustion),
            Turn = Restore("turn", c.Turn, before.Turn, after.Turn),
            Resources = RestoreEntries("resource", c.Resources, explicitHitDice?before.Resources.Where(p=>p.Id!="hitdice").ToList():before.Resources, explicitHitDice?after.Resources.Where(p=>p.Id!="hitdice").ToList():after.Resources, x => x.Id),
            Effects = RestoreEntries("effect", c.Effects, before.Effects, after.Effects, x => x.Id),
            Play = c.Play with
            {
                SpellSources=RestoreJsonList("spell source",c.Play.SpellSources,before.SpellSources,after.SpellSources),
                HitDiePools=RestoreJsonList("Hit Die pool",c.Play.HitDiePools,before.HitDiePools,after.HitDiePools),
                KnownSpellIds=c.Play.SpellSources.Count>0?c.Play.KnownSpellIds:before.KnownSpellIds is null||after.KnownSpellIds is null?c.Play.KnownSpellIds:RestorePreparations(c.Play.KnownSpellIds,before.KnownSpellIds,after.KnownSpellIds),
                Preparation=c.Play.Preparation with{OwnedCantripSpellIds=before.OwnedCantripSpellIds is null||after.OwnedCantripSpellIds is null?c.Play.Preparation.OwnedCantripSpellIds:RestorePreparations(c.Play.Preparation.OwnedCantripSpellIds,before.OwnedCantripSpellIds,after.OwnedCantripSpellIds)},
                PreparedSpellIds = c.Play.SpellSources.Count>0?c.Play.PreparedSpellIds:RestorePreparations(c.Play.PreparedSpellIds, before.PreparedSpellIds, after.PreparedSpellIds),
                DreadUsedThisTurn = Restore("Dreadful Strike turn use", c.Play.DreadUsedThisTurn, before.DreadUsedThisTurn, after.DreadUsedThisTurn),
                HunterTarget = Restore("marked target", c.Play.HunterTarget, before.HunterTarget, after.HunterTarget),
                LastRoll = before.ReceiptsCaptured||after.ReceiptsCaptured?RestoreReceipt("roll",c.Play.LastRoll,before.LastRoll,after.LastRoll):null,
                LastCast = before.ReceiptsCaptured||after.ReceiptsCaptured?RestoreReceipt("spell effect",c.Play.LastCast,before.LastCast,after.LastCast):null,
                UndoHistory = c.Play.UndoHistory.Take(c.Play.UndoHistory.Count - 1).ToList(),
                CompletedOperationIds = [.. c.Play.CompletedOperationIds.Append(r.OperationId).TakeLast(100)]
            }
        };
        if(candidate.Play.SpellSources.Count>0)candidate=candidate with{Play=candidate.Play with{PreparedSpellIds=candidate.Play.SpellSources.SelectMany(s=>s.PreparedSpellIds).Distinct().ToList(),KnownSpellIds=candidate.Play.SpellSources.SelectMany(s=>s.KnownSpellIds).Distinct().ToList()}};
        if(candidate.Play.HitDiePools.Count>0)candidate=HitDiePoolRules.SynchronizeTotal(candidate);
        try { WorkspaceValidation.Character(candidate); }
        catch (ArgumentException ex) { throw new WorkspaceConflictException("Undo conflicts with the current character: " + ex.Message); }
        return candidate;
    }

    private static PlayTrackedState Capture(CharacterState c) => new()
    {
        ReceiptsCaptured=c.Combat.EncounterActive||c.Play.LastCast?.Preview.AttackRollCount is>0,LastRoll=ClassPlayRules.Copy(c.Play.LastRoll),LastCast=ClassPlayRules.Copy(c.Play.LastCast),
        Combat=ClassPlayRules.Copy(c.Combat),
        SpellSources=ClassPlayRules.Copy(c.Play.SpellSources),HitDiePools=ClassPlayRules.Copy(c.Play.HitDiePools),
        CurrentHp = c.CurrentHp, TemporaryHp = c.TemporaryHp, Exhaustion = c.Exhaustion, Turn = c.Turn,
        DeathSaves=c.Sheet.DeathSaves with{},HeroicInspiration=SheetRules.HasHeroicInspiration(c),
        Resources = c.Resources.Select(x => x with { }).ToList(), Effects = c.Effects.Select(x => x with { }).ToList(),
        KnownSpellIds=[..c.Play.KnownSpellIds],OwnedCantripSpellIds=[..c.Play.Preparation.OwnedCantripSpellIds],PreparedSpellIds = [.. c.Play.PreparedSpellIds], DreadUsedThisTurn = c.Play.DreadUsedThisTurn, HunterTarget = c.Play.HunterTarget
    };

    private static T? RestoreReceipt<T>(string label,T? current,T? before,T? after) where T:class
    {
        var b=System.Text.Json.JsonSerializer.Serialize(before);var a=System.Text.Json.JsonSerializer.Serialize(after);var value=System.Text.Json.JsonSerializer.Serialize(current);
        if(a==b)return current;
        static string Id(T? item)=>item switch{RollOutcome roll=>roll.Id,CastOutcome cast=>cast.Id,_=>""};
        var originalReceipt=Id(after).Length>0&&Id(before)!=Id(after)&&Id(current)==Id(after);
        if(value!=a&&value!=b&&!originalReceipt)throw new WorkspaceConflictException($"A later {label} result overlaps this action and has been preserved.");
        return ClassPlayRules.Copy(before);
    }

    private static CombatState RestoreCombat(CombatState current,CombatState? before,CombatState? after)
    {
        if(before is null||after is null)return current;
        var b=System.Text.Json.JsonSerializer.Serialize(before);var a=System.Text.Json.JsonSerializer.Serialize(after);var value=System.Text.Json.JsonSerializer.Serialize(current);
        if(a==b)return current;
        if(value!=a&&value!=b)throw new WorkspaceConflictException("A later combat change overlaps this action and has been preserved.");
        return ClassPlayRules.Copy(before);
    }

    private static List<T> RestoreJsonList<T>(string label,List<T> current,List<T>? before,List<T>? after)
    {
        if(before is null||after is null)return ClassPlayRules.Copy(current);
        string Key(T item)=>item switch{SpellSourceState source=>source.Id,HitDiePool pool=>pool.Id,_=>throw new ArgumentException("Unsupported undo snapshot.")};
        string Value(T? item)=>System.Text.Json.JsonSerializer.Serialize(item);
        var result=ClassPlayRules.Copy(current);var old=before.ToDictionary(Key);var next=after.ToDictionary(Key);
        foreach(var id in old.Keys.Union(next.Keys))
        {
            old.TryGetValue(id,out var b);next.TryGetValue(id,out var a);if(Value(b)==Value(a))continue;
            var index=result.FindIndex(item=>Key(item)==id);var value=index<0?default:result[index];
            if(Value(value)!=Value(a)&&Value(value)!=Value(b))throw new WorkspaceConflictException($"A later {label} {id} change overlaps this action and has been preserved.");
            if(b is null){if(index>=0)result.RemoveAt(index);}else if(index>=0)result[index]=ClassPlayRules.Copy(b);else result.Add(ClassPlayRules.Copy(b));
        }
        return result;
    }

    private static T Restore<T>(string label, T current, T before, T after)
    {
        var comparer = EqualityComparer<T>.Default;
        if (comparer.Equals(before, after)) return current;
        if (!comparer.Equals(current, after) && !comparer.Equals(current, before))
            throw new WorkspaceConflictException($"A later {label} change overlaps this action. It has been preserved; resolve that change before undoing.");
        return before;
    }

    private static List<T> RestoreEntries<T>(string label, List<T> current, List<T> before, List<T> after, Func<T, string> id) where T : class
    {
        var oldById = before.ToDictionary(id, StringComparer.Ordinal);
        var newById = after.ToDictionary(id, StringComparer.Ordinal);
        var currentById = current.ToDictionary(id, StringComparer.Ordinal);
        var result = current.ToList();
        foreach (var key in oldById.Keys.Union(newById.Keys, StringComparer.Ordinal))
        {
            oldById.TryGetValue(key, out var oldItem);
            newById.TryGetValue(key, out var newItem);
            if (Equals(oldItem, newItem)) continue;
            currentById.TryGetValue(key, out var currentItem);
            var restored = Restore(label + " " + key, currentItem, oldItem, newItem);
            var index = result.FindIndex(x => id(x) == key);
            if (restored is null) { if (index >= 0) result.RemoveAt(index); }
            else if (index >= 0) result[index] = restored;
            else result.Add(restored);
        }
        return result;
    }

    private static List<string> RestorePreparations(List<string> current, List<string> before, List<string> after)
    {
        static bool Same(List<string> x, List<string> y) => x.Count == y.Count && x.ToHashSet(StringComparer.Ordinal).SetEquals(y);
        if (Same(before, after)) return [.. current];
        if (!Same(current, after) && !Same(current, before)) throw new WorkspaceConflictException("A later spell preparation change overlaps this action and has been preserved.");
        return [.. before];
    }
}
