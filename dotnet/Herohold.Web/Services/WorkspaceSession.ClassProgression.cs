using Herohold.Rules;
namespace Herohold.Web.Services;
public sealed partial class WorkspaceSession
{
    public IReadOnlyList<ClassEntryOption> LevelUpClassOptions
    {
        get
        {
            if(Character is null)return [];
            var review=ClassTrackRules.Read(WorkspaceProjection.BuildOf(Character));
            return MulticlassRules.ClassNames.Select(name=>
            {
                var same=review.Tracks.Count==1&&review.Tracks[0].ClassName==name;
                var reason=!review.CanCalculate?"Resolve the imported class track history first.":Character.Level>=20?"Character level20 is the maximum.":same?"":string.Join(" ",MulticlassRules.ValidateEntry(review.Tracks,name,Character.AbilityScores).Select(i=>i.Message));return new ClassEntryOption(name,reason.Length==0,reason);
            }).ToArray();
        }
    }
}
