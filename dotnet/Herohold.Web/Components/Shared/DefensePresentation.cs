namespace Herohold.Web.Components.Shared;

public static class DefensePresentation
{
    public static string Label(string kind)=>kind switch
    {
        "resistance"=>"Resistance","immunity"=>"Immunity","vulnerability"=>"Vulnerability","conditionImmunity"=>"Condition immunity",_=>"Recorded defense"
    };
}
