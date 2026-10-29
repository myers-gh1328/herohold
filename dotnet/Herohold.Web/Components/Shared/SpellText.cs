using System.Text.RegularExpressions;

namespace Herohold.Web.Components.Shared;

/// <summary>Player-facing spell wording.</summary>
public static partial class SpellText
{
    /// <summary>"1 action" reads as the turn resource it uses ("Action", "Bonus action", "Reaction"); longer times stay as written.</summary>
    public static string CastingTime(string value)
    {
        var text=value.Trim();
        var single=SingleTurnResource().Match(text);
        if(!single.Success)return text;
        var resource=single.Groups[1].Value.ToLowerInvariant() switch{"bonus" or "bonus action"=>"Bonus action","reaction"=>"Reaction",_=>"Action"};
        return resource+single.Groups[2].Value;
    }
    [GeneratedRegex(@"^1\s+(action|bonus action|bonus|reaction)\b(.*)$",RegexOptions.IgnoreCase)]
    private static partial Regex SingleTurnResource();
}
