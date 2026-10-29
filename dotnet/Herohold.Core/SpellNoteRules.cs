namespace Herohold.Core;
public static class SpellNoteRules
{
    public static CharacterState Set(CharacterState character,string spellId,string text)
    {
        ArgumentNullException.ThrowIfNull(character);WorkspaceValidation.Id(spellId);
        if(text is null||text.Length>4000)throw new ArgumentException("Spell notes must be at most 4,000 characters.");
        var owned=character.Play.PreparedSpellIds.Concat(character.Play.KnownSpellIds).Concat(character.Play.SpellbookIds).Concat(character.Play.GrantedSpellIds);
        if(!owned.Contains(spellId,StringComparer.Ordinal))throw new ArgumentException("Choose a spell owned by this character.");
        var copy=DomainCopy.Clone(character);if(string.IsNullOrWhiteSpace(text))copy.Play.SpellNotes.Remove(spellId);else copy.Play.SpellNotes[spellId]=text.Trim();
        return copy;
    }
}
