using Herohold.Core;
namespace Herohold.Web.Services;
public sealed partial class WorkspaceSession
{
    public Task AddNoteAsync(string title,string text,JournalCategory category,string sourceCharacterId)=>Mutate(c=>
    {RequireJournalSource(c,sourceCharacterId);return JournalRules.Add(c,title,text,category);});
    public Task EditJournalNoteAsync(string noteId,string title,string text,JournalCategory category,string sourceCharacterId)=>Mutate(c=>
    {RequireJournalSource(c,sourceCharacterId);return JournalRules.Edit(c,noteId,title,text,category);});
    public Task MoveJournalNoteAsync(string noteId,JournalCategory category,string sourceCharacterId)=>Mutate(c=>
    {RequireJournalSource(c,sourceCharacterId);return JournalRules.Move(c,noteId,category);});
    public Task CreateJournalCategoryAsync(string name,string sourceCharacterId)=>Mutate(c=>
    {RequireJournalSource(c,sourceCharacterId);return JournalRules.CreateCategory(c,name);});
    public Task AddNoteAsync(string title,string text,string category,string sourceCharacterId)=>Mutate(c=>
    {RequireJournalSource(c,sourceCharacterId);return JournalRules.Add(c,title,text,category);});
    public Task EditJournalNoteAsync(string noteId,string title,string text,string category,string sourceCharacterId)=>Mutate(c=>
    {RequireJournalSource(c,sourceCharacterId);return JournalRules.Edit(c,noteId,title,text,category);});
    public Task MoveJournalNoteAsync(string noteId,string category,string sourceCharacterId)=>Mutate(c=>
    {RequireJournalSource(c,sourceCharacterId);return JournalRules.Move(c,noteId,category);});
    public Task ArchiveJournalNoteAsync(string noteId,bool archived,string sourceCharacterId)=>Mutate(c=>
    {RequireJournalSource(c,sourceCharacterId);return JournalRules.Archive(c,noteId,archived);});
    private static void RequireJournalSource(CharacterState character,string sourceCharacterId)
    {
        if(character.Id!=sourceCharacterId)throw new WorkspaceConflictException("This journal entry belongs to the previously opened character. Reopen it before saving.");
    }
}
