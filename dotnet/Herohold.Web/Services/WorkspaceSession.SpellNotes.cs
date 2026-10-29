using Herohold.Core;
namespace Herohold.Web.Services;
public sealed partial class WorkspaceSession
{
    public Task SetSpellNoteAsync(string spellId,string note,string sourceCharacterId)=>Mutate(c=>c.Id!=sourceCharacterId?throw new WorkspaceConflictException("These spell notes belong to another character."):SpellNoteRules.Set(c,spellId,note));
}
