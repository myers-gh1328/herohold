using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Herohold.Core;
using Herohold.Rules;

namespace Herohold.Web.Services;

public sealed partial class WorkspaceSession
{
    private string currentId = SampleWorkspace.CharacterId;
    private long workflowRevision;
    private string? workflowSourceId;
    private CharacterState? uploadCandidate;
    private byte[]? workflowPdf;
    private string workflowKind = "";
    /// <summary>The open create, edit or import draft as the rules settled it.</summary>
    private DraftResult? draft;
    /// <summary>The open level-up: the character it started from, its draft and its HP policy.</summary>
    private CharacterState? levelSource;
    private LevelUpResult? levelUp;
    private LevelUpHp levelHp = new();
    public WorkflowView? Workflow { get; private set; }
    public ItemCatalog? Items { get; private set; }
    public IReadOnlyList<CharacterSummary> Library { get; private set; } = [];
    public string? LibraryNotice { get; private set; }
    private static readonly JsonSerializerOptions WorkspaceJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32,
        Converters = { new JsonStringEnumConverter() }
    };
    private CancellationTokenSource workflowCancellation=new();
    private ICharacterRules Rules=>rules??throw new InvalidOperationException("The character rules are not configured.");
    /// <summary>Runs one rules step off the circuit; a draft cancelled meanwhile is never shown.</summary>
    private async Task<T> RunRules<T>(Func<ICharacterRules,T> step)
    {
        var token=workflowCancellation.Token;var configured=Rules;
        var result=await Task.Run(()=>step(configured),token);
        token.ThrowIfCancellationRequested();return result;
    }
    private async Task<T> RunRulesAsync<T>(Func<ICharacterRules,CancellationToken,Task<T>> step)
    {
        var token=workflowCancellation.Token;var configured=Rules;
        var result=await Task.Run(()=>step(configured,token),token);
        token.ThrowIfCancellationRequested();return result;
    }
    /// <summary>The saved character an open draft edits, while it is still the one shown.</summary>
    private CharacterState? Edited=>workflowSourceId is not null&&workflowSourceId==Character?.Id?Character:null;

    private async Task LoadLibraryCoreAsync()
    {
        var summaries = await repository.ListAsync();
        Library = summaries.Select(s=>new CharacterSummary(s.Id,s.Name,s.Species,s.ClassName,s.Level,s.Revision))
            .OrderBy(s=>s.Name,StringComparer.OrdinalIgnoreCase).ToArray();
        LibraryNotice=null;
    }
    private async Task RefreshLibraryAfterAcceptanceAsync()
    {
        if(Snapshot is not null)
        {
            var c=Snapshot.Character;var entry=new CharacterSummary(c.Id,c.Name,c.Species,c.ClassName,c.Level,Snapshot.Revision);
            Library=Library.Where(s=>s.Id!=c.Id).Append(entry).OrderBy(s=>s.Name,StringComparer.OrdinalIgnoreCase).ToArray();
        }
        try{await LoadLibraryCoreAsync();}
        catch(Exception error)when(IsExpected(error))
        {LibraryNotice="This character is saved. Other library entries could not be refreshed; check the library files and reload the list.";}
    }
    private async Task RunWorkflow(Func<Task> action)
    {
        await gate.WaitAsync();
        try { Begin("Reviewing choices…"); await action(); if(Snapshot is not null)Saved(); }
        catch(OperationCanceledException){if(Snapshot is not null)Saved();}
        catch(Exception error)when(IsExpected(error)){Failed(error);}
        finally{Finish();gate.Release();}
    }
    public Task RefreshLibraryAsync()=>RunWorkflow(RefreshLibraryAfterAcceptanceAsync);
    public Task LoadCatalogAsync()=>RunWorkflow(async()=>Items=await RunRules(r=>r.Items()));
    public Task SwitchCharacterAsync(string id)=>RunWorkflow(async()=>
    {
        var loaded=await repository.LoadAsync(id)??throw new InvalidOperationException("This character is no longer in the library.");
        Snapshot=loaded;currentId=id;ClearWorkflow();await RefreshLibraryAfterAcceptanceAsync();
    });
    private void ClearWorkflow(){workflowCancellation.Cancel();workflowCancellation.Dispose();workflowCancellation=new();Workflow=null;draft=null;levelUp=null;levelSource=null;levelHp=new();uploadCandidate=null;workflowKind="";workflowSourceId=null;workflowPdf=null;}
    public void CancelWorkflow(){ClearWorkflow();Error=null;if(Snapshot is not null)Saved();Changed?.Invoke();}
    private void SetDraft(string kind,DraftResult result)
    {
        if(kind=="import"&&result.Validation.Complete&&result.Review?.CanAdopt!=false)
            repository.Validate(new(){Character=uploadCandidate??WorkspaceProjection.WithNewIdentity(Rules.Adopt(result.Draft))});
        workflowKind=kind;draft=result;levelUp=null;Workflow=WorkflowViews.From(kind,workflowRevision,result);
    }
    private void SetLevelUp(LevelUpResult result){workflowKind="levelup";levelUp=result;draft=null;Workflow=WorkflowViews.From(workflowRevision,result);}
    public Task StartCreateAsync()=>RunWorkflow(async()=>
    {
        ClearWorkflow();workflowRevision=Snapshot?.Revision??0;
        SetDraft("create",await RunRules(r=>r.Create()));
    });
    public Task StartEditAsync()=>RunWorkflow(async()=>
    {
        if(Character is null)throw new InvalidOperationException("Load a character first.");
        ClearWorkflow();workflowRevision=Snapshot!.Revision;workflowSourceId=Character.Id;
        var source=Character;SetDraft("edit",await RunRules(r=>r.Calculate(CharacterDraft.Of(source),source)));
    });
    public Task StartLevelUpAsync()=>StartLevelUpAsync(null);
    public Task StartLevelUpAsync(string? targetClassName)=>RunWorkflow(async()=>
    {
        if(Character is null)throw new InvalidOperationException("Load a character first.");
        if(Character.Level>=20)throw new InvalidOperationException("This character has reached level 20.");
        ClearWorkflow();workflowRevision=Snapshot!.Revision;workflowSourceId=Character.Id;
        var source=Character;levelSource=source;
        SetLevelUp(await RunRules(r=>r.StartLevelUp(source,source.Level+1,targetClassName)));
    });
    /// <summary>Sets one build value of the open creation, edit or import draft, such as its name or ability score method.</summary>
    public Task EditDraftAsync(IReadOnlyList<string> path,ChoiceValue value)
    {
        if(path.Count==0||path.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("Choose a build value to change.");
        return RunWorkflow(()=>EditCoreAsync([..path],value));
    }
    /// <summary>Previews the open level-up with this hit point policy.</summary>
    public Task PreviewLevelUpHpAsync(LevelUpHp hp)=>RunWorkflow(async()=>
    {
        if(workflowKind!="levelup")throw new InvalidOperationException("Open a level-up first.");
        var (source,current)=LevelState();levelHp=hp;
        SetLevelUp(await RunRules(r=>r.PreviewLevelUp(source,current,hp)));
    });
    private async Task EditCoreAsync(List<string> path,ChoiceValue value)
    {
        var kind=Workflow?.Kind??throw new InvalidOperationException("Open a character draft first.");
        if(kind is not ("create" or "edit" or "import"))throw new InvalidOperationException("This draft cannot be edited.");
        var open=draft??throw new InvalidOperationException("The draft has no character.");
        if(kind=="import"&&path[0]=="import_selections")
        {
            if(workflowPdf is null||open.Review?.Selections is not { } previous)throw new InvalidOperationException("Reopen the PDF review to change recovered selections.");
            if(path.Count<2)throw new ArgumentException("Choose a recovered selection to change.");
            var selections=previous.With([..path.Skip(1)],value);
            var pdf=workflowPdf;SetDraft(kind,await RunRulesAsync((r,t)=>r.ImportPdfAsync(pdf,selections,t)));
            return;
        }
        var existing=Edited;var edited=await RunRules(r=>r.Edit(open.Draft,path,value,existing));
        if(kind=="import"&&open.Review is {} review)edited=Rules.Revalidate(edited,review);
        uploadCandidate=null;SetDraft(kind,edited);
    }
    public Task GenerateAbilityScoresAsync(string method)=>AbilityScoreActionAsync(r=>(d,e)=>r.GenerateAbilityScores(d,method,e));
    public Task AssignAbilityScoreAsync(Ability ability,int index)=>AbilityScoreActionAsync(r=>(d,e)=>r.AssignAbilityScore(d,ability,index,e));
    private Task AbilityScoreActionAsync(Func<ICharacterRules,Func<CharacterDraft,CharacterState?,DraftResult>> action)=>RunWorkflow(async()=>
    {
        var kind=Workflow?.Kind;if(kind is not ("create" or "edit"))throw new ArgumentException("Open a creation or edit draft to generate ability scores.");
        var open=draft!.Draft;var existing=Edited;uploadCandidate=null;SetDraft(kind,await RunRules(r=>action(r)(open,existing)));
    });
    public Task ChooseWorkflowAsync(string choiceId,ChoiceValue choice)=>RunWorkflow(async()=>
    {
        var kind=Workflow?.Kind??throw new InvalidOperationException("Open a draft first.");
        if(kind=="levelup")
        {
            var (source,current)=LevelState();
            SetLevelUp(await RunRules(r=>r.ChooseLevelUp(source,current,choiceId,choice)));
            return;
        }
        var offered=Workflow!.Choices.FirstOrDefault(x=>x.Id==choiceId)??throw new ArgumentException("This choice is no longer available.");
        if(choiceId.StartsWith("existing_feat:",StringComparison.Ordinal))
        {
            uploadCandidate=null;var open=draft!;var existing=Edited;
            var repaired=await RunRules(r=>r.RepairFeat(open.Draft,choiceId["existing_feat:".Length..],choice,existing));
            if(kind=="import"&&open.Review is {} review)repaired=Rules.Revalidate(repaired,review);
            SetDraft(kind,repaired);return;
        }
        if(offered.Path.Count==0)throw new ArgumentException("This choice has no supported input path.");
        uploadCandidate=null;await EditCoreAsync([..offered.Path],choice);
    });
    private (CharacterState Source,LevelUpDraft Draft) LevelState()=>(levelSource??throw new InvalidOperationException("The level-up source is missing."),levelUp?.Review.Draft??throw new InvalidOperationException("The level-up draft is missing."));
    private void CheckWorkflowSource()
    {
        if(workflowSourceId is not null&&(Snapshot?.Character.Id!=workflowSourceId||Snapshot.Revision!=workflowRevision))
            throw new WorkspaceConflictException("The character changed after this draft opened. Start a fresh draft.");
    }
    public Task CommitWorkflowAsync(LevelUpHp? hp=null)=>RunWorkflow(async()=>
    {
        var kind=Workflow?.Kind??throw new InvalidOperationException("Open a draft first.");
        if(kind=="import"){await AdoptImportCoreAsync();return;}
        CheckWorkflowSource();CharacterState candidate;
        if(kind=="levelup")
        {
            var (source,current)=LevelState();var policy=hp??levelHp;
            candidate=await RunRules(r=>r.CommitLevelUp(source,current,policy));
        }
        else
        {
            var open=draft??throw new InvalidOperationException("The draft has no character.");var existing=kind=="create"?null:Character;
            candidate=await RunRules(r=>r.Adopt(open.Draft,existing));
        }
        if(kind=="create")candidate=WorkspaceProjection.WithNewIdentity(candidate) with{CurrentHp=candidate.MaximumHp,TemporaryHp=0};
        var expected=kind=="create"?0:Snapshot!.Revision;
        var saved=await repository.SaveAsync(new(){Revision=expected,Character=candidate},expected);
        Snapshot=saved;currentId=candidate.Id;ClearWorkflow();await RefreshLibraryAfterAcceptanceAsync();
    });
    public Task ReviewUploadAsync(string filename,byte[] bytes)=>RunWorkflow(async()=>
    {
        if(bytes.Length==0||bytes.Length>20*1024*1024)throw new ArgumentException("Choose a JSON or PDF file no larger than 20 MB.");
        ClearWorkflow();workflowRevision=Snapshot?.Revision??0;
        var pdf=bytes.AsSpan().StartsWith("%PDF-"u8);
        if(!pdf)
        {
            var document=JsonNode.Parse(bytes,documentOptions:new(){MaxDepth=32}) as JsonObject??throw new ArgumentException("Choose a character JSON object.");
            if(document.ContainsKey("schemaVersion")&&document.ContainsKey("character"))
            {
                var (snapshot,savedCanonical)=LegacyJsonSaves.ReadDocument(document);
                if(snapshot.SchemaVersion is not (1 or 2 or 3))throw new InvalidDataException("This workspace version is not supported.");
                WorkspaceRules.Validate(snapshot.Character);
                var candidate=WorkspaceProjection.WithNewIdentity(snapshot.Character);uploadCandidate=candidate;
                var reviewed=CharacterDraft.FromDocument(WorkspaceProjection.ToCanonical(candidate));
                var dropped=savedCanonical is null?[]:await RunRules(r=>r.DroppedFields(savedCanonical,reviewed));
                SetDraft("import",new(){Draft=reviewed,Summary=Summary(candidate),Validation=new(){Complete=true},
                    Review=new(){CanAdopt=true,SourceCanAdopt=true,SourceKind="Herohold workspace",Message="Review this character before adding a separate copy to the library.",DroppedFields=dropped}});
                return;
            }
            SetDraft("import",await RunRules(r=>r.ImportJson(document)));
        }
        else{workflowPdf=[..bytes];var copy=workflowPdf;SetDraft("import",await RunRulesAsync((r,t)=>r.ImportPdfAsync(copy,null,t)));}
    });
    private static DraftSummary Summary(CharacterState c)=>new()
    {
        Name=c.Name,Species=c.Species,ClassName=c.ClassName,Subclass=c.Subclass,Level=c.Level,ClassLevels=c.Build?.ClassLevels,MaximumHp=c.MaximumHp,CurrentHp=c.CurrentHp,
        AbilityMethod=c.Build?.AbilityScoreMethod is {Length:>0} method?method:"Manual entry",RolledScores=c.Build?.RolledAbilityScores??[],
        Assignments=Enum.GetValues<Ability>().Where(a=>c.Build?.AbilityScoreAssignments?.ContainsKey(a.ToString())==true).ToDictionary(a=>a,a=>c.Build!.AbilityScoreAssignments![a.ToString()])
    };
    public Task AdoptImportAsync()=>RunWorkflow(AdoptImportCoreAsync);
    private async Task AdoptImportCoreAsync()
    {
        if(Workflow?.Kind!="import"||draft is null)throw new InvalidOperationException("Review an uploaded character first.");
        if(!draft.Validation.Complete)throw new InvalidOperationException("Resolve the required character choices before confirming.");
        if(Workflow.Review?.CanAdopt==false)throw new InvalidOperationException("Confirm the required PDF review selections before adopting this character.");
        var open=draft.Draft;var candidate=uploadCandidate??WorkspaceProjection.WithNewIdentity(await RunRules(r=>r.Adopt(open)));
        var saved=await repository.SaveAsync(new(){Character=candidate},0);
        Snapshot=saved;currentId=candidate.Id;ClearWorkflow();await RefreshLibraryAfterAcceptanceAsync();
    }
    public async Task<ExportArtifact?> ExportAsync(string format)
    {
        ExportArtifact? artifact=null;
        await RunWorkflow(async()=>
        {
            if(Snapshot is null)throw new InvalidOperationException("Load a character first.");
            var safeName=new string(Character!.Name.Where(c=>char.IsLetterOrDigit(c)||c is ' ' or '-' or '_').Take(80).ToArray()).Trim();
            if(safeName.Length==0)safeName="character";
            if(format=="workspace"){artifact=new(safeName+".herohold.json","application/json",JsonSerializer.SerializeToUtf8Bytes(Snapshot,WorkspaceJson));return;}
            if(format is not ("json" or "pdf"))throw new ArgumentException("Choose a supported export format.");
            var exported=Character;var bytes=await RunRulesAsync((r,t)=>r.ExportAsync(exported,format,t));
            artifact=new(safeName+"."+format,format=="pdf"?"application/pdf":"application/json",bytes);
        });
        return artifact;
    }
}
