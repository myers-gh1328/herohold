namespace Herohold.Core;

public enum JournalCategory { Notes, Quests, Friends, Foes, Locations, Lore }
public sealed record NamedJournalEntry(JournalNote Note,string Category);
public sealed record JournalEntry(JournalNote Note,JournalCategory Category);

/// <summary>Journal organization is kept in the build's optional journal facts, preserving the strict note/workspace shape.</summary>
public static class JournalRules
{
    public static IReadOnlyList<JournalCategory> Categories {get;}=Array.AsReadOnly(Enum.GetValues<JournalCategory>());
    private sealed record CustomJournal(List<string> Categories,Dictionary<string,string> Notes);
    private static string CheckedName(string name)
    {
        if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Enter a category name.");
        var trimmed=name.Trim();if(trimmed.Length>48||trimmed.Any(char.IsControl))throw new ArgumentException("Category names allow up to 48 characters and no control characters.");
        return trimmed;
    }
    private static CustomJournal ReadCustom(CharacterState character)
    {
        var result=new CustomJournal([],new(StringComparer.Ordinal));
        if(character.Build?.CustomJournal is not {} metadata)return result;
        if(metadata.Version!=1||metadata.Categories is not {Count:<=50} names||metadata.Notes is not {Count:<=1000} notes)
            throw new ArgumentException("Custom journal organization needs supported, bounded category metadata.");
        var used=new HashSet<string>(Categories.Select(c=>c.ToString()),StringComparer.OrdinalIgnoreCase);
        foreach(var name in names)
        {
            if(name is null||CheckedName(name)!=name||!used.Add(name))throw new ArgumentException("Custom category names must be unique, trimmed and different from the starter categories.");
            result.Categories.Add(name);
        }
        foreach(var pair in notes)
        {
            WorkspaceValidation.Id(pair.Key);
            if(pair.Value is not {} name||!result.Categories.Contains(name,StringComparer.Ordinal))throw new ArgumentException("A journal entry names an unavailable custom category.");
            result.Notes.Add(pair.Key,name);
        }
        return result;
    }
    public static IReadOnlyList<string> CategoryNames(CharacterState character)=>Categories.OrderBy(c=>c==JournalCategory.Notes?99:(int)c).Select(c=>c.ToString()).Concat(ReadCustom(character).Categories).ToList().AsReadOnly();
    public static IReadOnlyList<NamedJournalEntry> NamedEntries(CharacterState character)
    {var custom=ReadCustom(character);return Entries(character).Select(entry=>new NamedJournalEntry(entry.Note,custom.Notes.GetValueOrDefault(entry.Note.Id,entry.Category.ToString()))).ToList().AsReadOnly();}
    private static CharacterState WriteCustom(CharacterState character,CustomJournal custom)
    {
        if(custom.Categories.Count==0&&character.Build?.CustomJournal is null)return character;
        return character with{Build=(character.Build??new()) with{CustomJournal=new(){Version=1,Categories=[..custom.Categories],Notes=new(custom.Notes,StringComparer.Ordinal)}}};
    }
    public static CharacterState CreateCategory(CharacterState character,string name)
    {
        ValidateMetadata(character);name=CheckedName(name);
        if(CategoryNames(character).Contains(name,StringComparer.OrdinalIgnoreCase))throw new ArgumentException("That category already exists. Choose another name.");
        var custom=ReadCustom(character);if(custom.Categories.Count>=50)throw new ArgumentException("A journal allows up to 50 custom categories.");
        custom.Categories.Add(name);var updated=WriteCustom(DomainCopy.Clone(character),custom);WorkspaceRules.Validate(updated);return updated;
    }
    private static CharacterState Assign(CharacterState character,string noteId,string category)
    {
        var name=CategoryNames(character).FirstOrDefault(n=>n.Equals(CheckedName(category),StringComparison.OrdinalIgnoreCase))??throw new ArgumentException("Choose an existing journal category or create one first.");
        var custom=ReadCustom(character);custom.Notes.Remove(noteId);
        var starter=Enum.TryParse<JournalCategory>(name,out var value)&&Enum.IsDefined(value)&&value.ToString()==name;
        if(!starter)custom.Notes[noteId]=name;
        return WriteCustom(SetCategory(character,noteId,starter?value:JournalCategory.Notes),custom);
    }
    public static CharacterState Add(CharacterState character,string title,string text,string category)
    {ValidateMetadata(character);var updated=WorkspaceRules.AddNote(character,title,text);updated=Assign(updated,updated.Notes[^1].Id,category);WorkspaceRules.Validate(updated);return updated;}
    public static CharacterState Edit(CharacterState character,string noteId,string title,string text,string category)
    {
        ValidateMetadata(character);WorkspaceValidation.Id(noteId);ArgumentException.ThrowIfNullOrWhiteSpace(title);ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if(title.Length>200||text.Length>12000)throw new ArgumentException("Notes allow a 200-character title and 12,000-character text.");
        var updated=DomainCopy.Clone(character);var index=updated.Notes.FindIndex(note=>note.Id==noteId);
        if(index<0)throw new ArgumentException("This journal entry is no longer available.");
        updated.Notes[index]=updated.Notes[index] with{Title=title.Trim(),Text=text.Trim()};updated=Assign(updated,noteId,category);WorkspaceRules.Validate(updated);return updated;
    }
    public static CharacterState Move(CharacterState character,string noteId,string category)
    {
        ValidateMetadata(character);WorkspaceValidation.Id(noteId);var updated=DomainCopy.Clone(character);
        if(!updated.Notes.Any(note=>note.Id==noteId))throw new ArgumentException("This journal entry is no longer available.");
        updated=Assign(updated,noteId,category);WorkspaceRules.Validate(updated);return updated;
    }
    private static Dictionary<string,JournalCategory> Read(CharacterState character)
    {
        ArgumentNullException.ThrowIfNull(character);var result=new Dictionary<string,JournalCategory>(StringComparer.Ordinal);
        if(character.Build?.Journal is not {} metadata)return result;
        if(metadata.Version!=1||metadata.Categories is not {Count:<=1000} categories)
            throw new ArgumentException("Journal organization needs supported, bounded category metadata.");
        foreach(var pair in categories)
        {
            WorkspaceValidation.Id(pair.Key);
            if(pair.Value is not {} label || !Enum.TryParse<JournalCategory>(label,out var category) || !Enum.IsDefined(category) || category.ToString()!=label)
                throw new ArgumentException("Choose Notes, Quests, Friends, Foes, Locations or Lore for journal entries.");
            result.Add(pair.Key,category);
        }
        return result;
    }
    public static IReadOnlySet<string> ArchivedNoteIds(CharacterState character)
    {
        var ids=new HashSet<string>(StringComparer.Ordinal);
        if(character.Build?.ArchivedJournalNotes is not {} values)return ids;
        if(values.Count>1000)throw new ArgumentException("Archived notes need a supported list of entry identifiers.");
        var available=character.Notes.Select(note=>note.Id).ToHashSet(StringComparer.Ordinal);
        foreach(var id in values)
        {
            if(id is null)throw new ArgumentException("An archived entry identifier is invalid.");
            WorkspaceValidation.Id(id);if(!ids.Add(id)||!available.Contains(id))throw new ArgumentException("An archived entry must identify one existing journal note.");
        }
        return ids;
    }
    public static CharacterState Archive(CharacterState character,string noteId,bool archived)
    {
        ValidateMetadata(character);WorkspaceValidation.Id(noteId);
        if(!character.Notes.Any(note=>note.Id==noteId))throw new ArgumentException("This journal entry is no longer available.");
        var updated=DomainCopy.Clone(character);var ids=ArchivedNoteIds(updated).ToHashSet(StringComparer.Ordinal);
        if(archived)ids.Add(noteId);else ids.Remove(noteId);
        updated=updated with{Build=(updated.Build??new()) with{ArchivedJournalNotes=[..ids.Order(StringComparer.Ordinal)]}};WorkspaceRules.Validate(updated);return updated;
    }
    public static void ValidateMetadata(CharacterState character){_ = Read(character);_ = ReadCustom(character);_ = ArchivedNoteIds(character);}
    public static IReadOnlyList<JournalEntry> Entries(CharacterState character)
    {
        var categories=Read(character);
        return character.Notes.Select(note=>new JournalEntry(note with{},categories.GetValueOrDefault(note.Id,JournalCategory.Notes))).ToList().AsReadOnly();
    }
    private static CharacterState SetCategory(CharacterState character,string noteId,JournalCategory category)
    {
        if(!Enum.IsDefined(category))throw new ArgumentException("Choose a supported journal category.");
        var categories=Read(character);
        if(category==JournalCategory.Notes)categories.Remove(noteId);else categories[noteId]=category;
        if(categories.Count==0&&character.Build?.Journal is null)return character;
        return character with{Build=(character.Build??new()) with{Journal=new(){Version=1,Categories=categories.ToDictionary(pair=>pair.Key,pair=>pair.Value.ToString(),StringComparer.Ordinal)}}};
    }
    public static CharacterState Add(CharacterState character,string title,string text,JournalCategory category)=>Add(character,title,text,StarterName(category));
    public static CharacterState Edit(CharacterState character,string noteId,string title,string text,JournalCategory category)=>Edit(character,noteId,title,text,StarterName(category));
    public static CharacterState Move(CharacterState character,string noteId,JournalCategory category)=>Move(character,noteId,StarterName(category));
    private static string StarterName(JournalCategory category)=>Enum.IsDefined(category)?category.ToString():throw new ArgumentException("Choose a supported journal category.");
}
