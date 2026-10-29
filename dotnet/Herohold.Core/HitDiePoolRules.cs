namespace Herohold.Core;

internal static class HitDiePoolRules
{
    public static CharacterState Adjust(CharacterState before,string poolId,int delta)
    {
        var c=DomainCopy.Clone(before);var index=c.Play.HitDiePools.FindIndex(p=>p.Id==poolId);
        if(index<0)throw new ArgumentException("Choose an existing Hit Die pool.");
        var pool=c.Play.HitDiePools[index];var remaining=(long)pool.Remaining+delta;
        if(remaining<0||remaining>pool.Maximum)throw new ArgumentOutOfRangeException(nameof(delta),"Hit Dice must remain between zero and that pool's maximum.");
        c.Play.HitDiePools[index]=pool with{Remaining=(int)remaining};return SynchronizeTotal(c);
    }
    public static CharacterState SynchronizeTotal(CharacterState c)
    {
        if(c.Play.HitDiePools.Count==0)return c;
        var resources=c.Resources.Select(r=>r with{}).ToList();var index=resources.FindIndex(r=>r.Id=="hitdice");
        var pool=(index<0?new ResourcePool{Id="hitdice",Name="Hit Dice",SourceKind="build"}:resources[index]) with{Maximum=c.Play.HitDiePools.Sum(p=>p.Maximum),Remaining=c.Play.HitDiePools.Sum(p=>p.Remaining)};
        if(index<0)resources.Add(pool);else resources[index]=pool;return c with{Resources=resources};
    }
}
