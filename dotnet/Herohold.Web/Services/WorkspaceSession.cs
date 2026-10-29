using System.Text.Json;
using Herohold.Core;
using Herohold.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Herohold.Web.Services;

/// <summary>A circuit-local view over the separately stored migration workspace.</summary>
public sealed partial class WorkspaceSession(IWorkspaceStore repository, ICharacterRules? rules = null, ILogger<WorkspaceSession>? logger = null)
{
    private readonly ILogger log = logger ?? NullLogger<WorkspaceSession>.Instance;
    private readonly SemaphoreSlim gate = new(1, 1);
    public WorkspaceSnapshot? Snapshot { get; private set; }
    public CharacterState? Character => Snapshot?.Character;
    public bool IsLoaded => Snapshot is not null;
    public bool IsBusy { get; private set; }
    public string? Error { get; private set; }
    public string SaveStatus { get; private set; } = "Loading character…";
    public event Action? Changed;

    public Task InitializeAsync() => IsLoaded ? Task.CompletedTask : ReloadAsync();

    public async Task ReloadAsync()
    {
        await gate.WaitAsync();
        try
        {
            Begin("Loading character…");
            var loaded = await repository.LoadAsync(currentId);
            if (loaded is null)
            {
                try { loaded = await repository.SaveAsync(SampleWorkspace.Create(), 0); }
                catch (WorkspaceConflictException)
                {
                    // Two new circuits may initialize the fixture together. Adopt the winner.
                    loaded = await repository.LoadAsync(currentId)
                        ?? throw new InvalidOperationException("The character could not be initialized.");
                }
            }
            if(loaded.Character.Id==SampleWorkspace.CharacterId&&(loaded.Character.Play.Spells.Count==0||loaded.Character.Build is null))
            {
                try{loaded=await repository.SaveAsync(loaded with{Character=loaded.Character with{Play=loaded.Character.Play.Spells.Count==0?SampleWorkspace.CreatePlay():loaded.Character.Play,Build=loaded.Character.Build??BuildFacts.FromCanonical(SampleCanonical.Create()),Resources=loaded.Character.Resources.Select(r=>new[]{"luck","dread","hunters","slot1","slot2","hitdice"}.Contains(r.Id)?r with{SourceKind="build"}:r).ToList(),Equipment=loaded.Character.Equipment.Any(i=>(i.Name=="Studded Leather"||i.Name=="Studded Leather Armor"))?loaded.Character.Equipment:[new(){Id="studded-leather",Name="Studded Leather Armor",Equipped=true,Description="Light armor. Base AC12 plus Dexterity."},..loaded.Character.Equipment]}},loaded.Revision);}
                catch(WorkspaceConflictException){loaded=await repository.LoadAsync(currentId)??throw new InvalidOperationException("Reload the character to continue.");}
            }
            Snapshot = loaded;
            await RefreshLibraryAfterAcceptanceAsync();
            Saved();
        }
        catch (Exception error) when (IsExpected(error)) { Failed(error); }
        finally { Finish(); gate.Release(); }
    }

    public Task AdjustHpAsync(int delta,string? sourceCharacterId=null) => AdjustHpPlayAsync(delta,sourceCharacterId);
    public Task AdjustResourceAsync(string id,int delta,string? sourceCharacterId=null) => AdjustResourcePlayAsync(id,delta,sourceCharacterId);
    public Task AdjustQuantityAsync(string id, int delta) => MutateEquipment(c => WorkspaceRules.AdjustQuantity(c, id, delta));
    public Task SetEquipmentAsync(string id, bool equipped, bool attuned) => MutateEquipment(c => WorkspaceRules.SetEquipment(c, id, equipped, attuned));
    public Task AddNoteAsync(string title, string text) => Mutate(c => WorkspaceRules.AddNote(c, title, text));
    public Task AddEffectAsync(string name, string source, string note, int? rounds, bool concentration) =>
        Mutate(c => WorkspaceRules.AddEffect(c, new ActiveEffect
        {
            Id = Guid.NewGuid().ToString("N"), Name = name, Source = source, Note = note,
            RemainingRounds = rounds, Concentration = concentration
        }));
    public Task RemoveEffectAsync(string id) => Mutate(c => WorkspaceRules.RemoveEffect(c, id));
    public Task AdvanceTurnAsync() => Mutate(WorkspaceRules.AdvanceTurn);

    private async Task Mutate(Func<CharacterState, CharacterState> mutation)
    {
        var expectedId=Snapshot?.Character.Id;
        await gate.WaitAsync();
        try
        {
            Begin("Saving…");
            if (Snapshot is null) throw new InvalidOperationException("Load the character before editing.");
            if(expectedId!=Snapshot.Character.Id)throw new WorkspaceConflictException("This action belongs to the previously opened character. Review it before trying again.");
            // Commit the detached candidate; a failed save never replaces visible prior state.
            var candidate = Snapshot with { Character = SheetRules.CloseInspirationWindows(mutation(Snapshot.Character)) };
            Snapshot = await repository.SaveAsync(candidate, Snapshot.Revision);
            Saved();
        }
        catch (Exception error) when (IsExpected(error)) { Failed(error); }
        finally { Finish(); gate.Release(); }
    }

    private static bool IsExpected(Exception error) => error is ArgumentException or InvalidOperationException
        or IOException or InvalidDataException or UnauthorizedAccessException or JsonException or OverflowException or CharacterWorkflowException;
    private void Begin(string status) { IsBusy = true; Error = null; SaveStatus = status; Changed?.Invoke(); }
    private void Saved() => SaveStatus = "Saved";
    private void Failed(Exception error)
    {
        // Players see a short message; the server log keeps the cause, including PDF worker output.
        var diagnostics = (error as PdfFormatException ?? error.InnerException as PdfFormatException)?.Diagnostics ?? "";
        log.LogWarning(error, "Character change failed for {CharacterId} during {Status}. {Diagnostics}", Snapshot?.Character.Id ?? "(none)", SaveStatus, diagnostics);
        Error = error switch
        {
            CharacterWorkflowException => error.Message,
            WorkspaceConflictException => error.Message,
            ArgumentException => error.Message,
            InvalidDataException => error.Message,
            InvalidOperationException => error.Message,
            _ => "The character could not be read or saved. The previous saved data was kept. Check storage and reload before trying again."
        };
        SaveStatus = "Change not saved";
    }
    private void Finish() { IsBusy = false; Changed?.Invoke(); }
}
