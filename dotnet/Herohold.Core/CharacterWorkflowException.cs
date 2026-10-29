namespace Herohold.Core;
/// <summary>A character request the rules refuse, with a stable code for the reason.</summary>
public class CharacterWorkflowException(string code,string message,Exception? inner=null):Exception(message,inner)
{
    public string Code { get; } = code;
}
