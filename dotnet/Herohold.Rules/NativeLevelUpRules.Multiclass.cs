using Herohold.Core;
namespace Herohold.Rules;
public sealed partial class NativeLevelUpRules
{
    public IReadOnlyList<ClassEntryOption> EligibleClasses(DraftCharacter source)
    {
        var review=ClassTrackRules.Read(source.Build);if(!review.CanCalculate)return catalog.ClassRecords.Select(c=>new ClassEntryOption(c.Name,false,"Resolve imported class tracks first.")).ToArray();
        var scores=characters.FeatContext(source).Scores.ToDictionary(p=>Enum.Parse<Ability>(p.Key),p=>p.Value);
        return catalog.ClassRecords.Select(c=>c.Name).Select(name=>
        {
            var same=review.Tracks.Count==1&&review.Tracks[0].ClassName==name;var issues=same?[]:MulticlassRules.ValidateEntry(review.Tracks,name,scores);var reason=source.Build.Level>=20?"Character level20 is the maximum.":string.Join(" ",issues.Select(i=>i.Message));return new ClassEntryOption(name,reason.Length==0,reason);
        }).ToArray();
    }
    public LevelUpReview Start(DraftCharacter source,int targetLevel,string targetClassName)
    {
        var current=source.Build.Level;if(current is <1 or >=20||targetLevel!=current+1)throw new LevelUpException("invalid_level","Advance one character level at a time, up to20.");
        var review=ClassTrackRules.Read(source.Build);if(!review.CanCalculate)throw new LevelUpException("incomplete_class_tracks",string.Join(" ",review.Issues.Select(i=>i.Message)));
        var option=EligibleClasses(source).SingleOrDefault(c=>c.Name==targetClassName)??throw new LevelUpException("invalid_class","Choose a supported class.");if(!option.Allowed)throw new LevelUpException("multiclass_prerequisite",option.Reason);
        if(review.Tracks.Count==1&&review.Tracks[0].ClassName==targetClassName&&source.Build.ClassTracks is null)return Start(source,targetLevel);
        var track=review.Tracks.SingleOrDefault(t=>t.ClassName==targetClassName);var before=track?.Level??0;
        return RebuildTrack(source,new(){TransactionId=Guid.NewGuid().ToString("N"),SourceHash=SourceHash(source.Build,source.Equipment),ClassName=targetClassName,ClassTrackId=track?.Id??"class-"+targetClassName.ToLowerInvariant(),CurrentLevel=current,TargetLevel=targetLevel,CurrentClassLevel=before,TargetClassLevel=before+1,AddsNewClass=track is null},new());
    }
    private LevelUpReview RebuildTrack(DraftCharacter source,LevelUpDraft draft,LevelUpHpOptions hp)
    {
        var build=source.Build;
        if(draft.Completed||(build.CompletedLevelUpTransactions??[]).Contains(draft.TransactionId))throw new LevelUpException("already_completed","This level-up transaction has already completed.");
        if(draft.Selections is null||draft.Selections.Count>100||draft.Selections.Any(p=>string.IsNullOrWhiteSpace(p.Key)||p.Key.Length>200||System.Text.Json.JsonSerializer.Serialize(p.Value).Length>32768))throw new LevelUpException("invalid_draft","A class choice exceeds the supported bounds.");
        if(!Guid.TryParseExact(draft.TransactionId,"N",out _)||draft.SourceHash!=SourceHash(build,source.Equipment))throw new LevelUpException("stale_draft","The character changed. Start a fresh level-up draft.");
        var review=ClassTrackRules.Read(build);if(!review.CanCalculate)throw new LevelUpException("incomplete_class_tracks","Resolve the class track history first.");
        var existing=review.Tracks.SingleOrDefault(t=>t.Id==draft.ClassTrackId);
        if(draft.CurrentLevel!=build.Level||draft.TargetLevel!=draft.CurrentLevel+1||draft.TargetLevel>20||draft.CurrentClassLevel!=(existing?.Level??0)||draft.TargetClassLevel!=draft.CurrentClassLevel+1||draft.AddsNewClass!=(existing is null)||existing is not null&&existing.ClassName!=draft.ClassName)throw new LevelUpException("invalid_draft","The target class draft no longer matches this sheet.");
        if(existing is null&&(review.Tracks.Any(t=>t.ClassName==draft.ClassName)||draft.ClassTrackId!="class-"+draft.ClassName.ToLowerInvariant()))throw new LevelUpException("invalid_draft","The new class must use its unique stable track identity.");
        var option=EligibleClasses(source).SingleOrDefault(o=>o.Name==draft.ClassName);if(option?.Allowed!=true)throw new LevelUpException("multiclass_prerequisite",option?.Reason??"Choose a supported target class.");
        var settledSource=Calculate(source);
        var sourceReport=Describe(With(source,settledSource.Build));if(sourceReport.Issues.Any(i=>i.Severity==BuildIssueSeverity.Error)||sourceReport.Definitions.Any(d=>d.Status!=BuildChoiceStatus.Resolved))throw new LevelUpException("incomplete_character","Complete the character's current required choices before leveling up.");
        var none=new Dictionary<string,ChoiceValue>(StringComparer.Ordinal){["class_options"]=ChoiceValue.Record([]),["spells"]=ChoiceValue.Record([]),["weapon_masteries"]=ChoiceValue.ListOf([])};
        var track=existing??new ClassTrack(draft.ClassTrackId,draft.ClassName,"XPHB",1,"",none);
        DraftCharacter scoped;IReadOnlyList<BuilderChoiceDefinition> pending;IReadOnlyList<BuildIssue> issues;IReadOnlyList<string> automatic;int baseGain;var die=MulticlassRules.HitDie(track.ClassName);
        if(hp.Method is not (HitPointMethod.Fixed or HitPointMethod.Rolled)||hp.Method==HitPointMethod.Rolled&&(hp.Roll is null||hp.Roll<1||hp.Roll>die))throw new LevelUpException("invalid_hp","Choose fixed HP or a valid unmodified class Hit Die result.");
        baseGain=hp.Method==HitPointMethod.Rolled?hp.Roll!.Value:HitPointRules.FixedHitDieGain(die);
        if(existing is not null)
        {
            var before=Settle(new DraftCharacter(ClassTrackRules.Scope(build,existing,existing.Id==review.InitialTrackId),source.Equipment,new(draft.TargetLevel,existing.Id!=review.InitialTrackId)));
            var inner=new LevelUpDraft{TransactionId=draft.TransactionId,SourceHash=SourceHash(before.Build,before.Equipment),ClassName=existing.ClassName,CurrentLevel=existing.Level,TargetLevel=existing.Level+1,Selections=draft.Selections};
            var result=Rebuild(before,inner,hp);scoped=With(before,result.Build);pending=result.PendingChoices;issues=result.Issues;automatic=result.AutomaticFeatures;
        }
        else
        {
            scoped=Settle(new DraftCharacter(ClassTrackRules.Scope(build,track,false),source.Equipment,new(source.Scope?.TotalLevel??build.Level,true)));
            for(var pass=0;pass<8;pass++)
            {
                var before=Fingerprint(scoped.Build);foreach(var d in Describe(scoped).Definitions.Where(IsClassChoice))if(draft.Selections.TryGetValue("new-"+d.Id,out var selected))scoped=ApplyPath(scoped,d.Path.ToArray(),selected);scoped=Settle(scoped);if(before==Fingerprint(scoped.Build))break;
            }
            pending=Describe(scoped).Definitions.Where(IsClassChoice).Select(d=>d with{Id="new-"+d.Id}).ToArray();issues=pending.SelectMany(d=>d.Issues).ToArray();var profile=ClassProgressionRules.AtLevel(classes,track.ClassName,1);automatic=profile.FeaturesAtLevel;
        }
        var scopedTree=scoped.Build.AsChoiceTree();var choices=new Dictionary<string,ChoiceValue>(track.Choices,StringComparer.Ordinal);foreach(var key in new[]{"class_options","spells","weapon_masteries"})choices[key]=scopedTree.At([key])??new();
        var advanced=track with{Level=draft.TargetClassLevel,SubclassName=scoped.Build.Subclass,Choices=choices};var tracks=review.Tracks.Where(t=>t.Id!=track.Id).Append(advanced).ToArray();
        var candidate=ClassTrackRules.WriteClone(build,new(tracks,review.InitialTrackId,[]));
        candidate=candidate with{Feats=scoped.Build.Feats??candidate.Feats,AbilityScoreAdjustments=scoped.Build.AbilityScoreAdjustments??candidate.AbilityScoreAdjustments,OriginFeatChoices=scoped.Build.OriginFeatChoices??candidate.OriginFeatChoices,HeroholdOriginFeatChoices=scoped.Build.HeroholdOriginFeatChoices??candidate.HeroholdOriginFeatChoices,
            HpHistory=[..candidate.HpHistory??[],new HpGainFact{CharacterLevel=draft.TargetLevel,ClassName=track.ClassName,Method=hp.Method==HitPointMethod.Rolled?"rolled":"fixed",BaseGain=baseGain}]};
        var settled=Calculate(With(source,candidate));var increase=settled.MaximumHp-settledSource.MaximumHp;
        var report=Describe(With(source,settled.Build));var allIssues=issues.Concat(report.Issues).ToArray();
        return new(){Draft=Detach(draft),Build=settled.Build,MaximumHp=settled.MaximumHp,PendingChoices=pending,Issues=allIssues,AutomaticFeatures=automatic,CanFinalize=allIssues.All(i=>i.Severity!=BuildIssueSeverity.Error)&&pending.All(d=>d.Status==BuildChoiceStatus.Resolved)&&report.Definitions.All(d=>d.Status==BuildChoiceStatus.Resolved),HitDie=die,HpBaseGain=baseGain,HpIncrease=increase,ProficiencyBonusBefore=CharacterCalculations.ProficiencyBonus(draft.CurrentLevel),ProficiencyBonusAfter=CharacterCalculations.ProficiencyBonus(draft.TargetLevel)};
    }
    private static bool IsClassChoice(BuilderChoiceDefinition d)=>d.Path.FirstOrDefault() is "class_options" or "spells" or "weapon_masteries" or "subclass";
}
