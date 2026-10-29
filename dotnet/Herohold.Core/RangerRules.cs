namespace Herohold.Core;

/// <summary>Reviewed 2024 Ranger features used by native play resolution.</summary>
public static class RangerRules
{
    public static bool IsGloomStalker(CharacterState c)
    {
        if(c.Build?.ClassTracks is {} tracks)
            return tracks.Any(t=>t.ClassName=="Ranger"&&t.Subclass=="Gloom Stalker"&&t.Level>=3);
        return ClassLevel(c)>=3&&c.ClassName.Equals("Ranger",StringComparison.OrdinalIgnoreCase)&&c.Subclass.Equals("Gloom Stalker",StringComparison.OrdinalIgnoreCase);
    }
    public static int ClassLevel(CharacterState c)
    {
        if(c.Build?.ClassLevels is {Count:>0} levels)
        {
            var level=levels.FirstOrDefault(pair=>pair.Key.Equals("Ranger",StringComparison.OrdinalIgnoreCase)).Value;
            return level is >=1 and <=20?level:0;
        }
        return c.ClassName.Equals("Ranger",StringComparison.OrdinalIgnoreCase)?c.Level:0;
    }
    public static DiceFormula DreadfulStrikeDamage(CharacterState c)
        => new(2, ClassLevel(c) >= 11 ? 8 : 6);
    public static DiceFormula HuntersMarkDamage(CharacterState c)
        => new(1, ClassLevel(c) >= 20 ? 10 : 6);
}
