using Herohold.Core;
using Microsoft.Extensions.Logging;
namespace Herohold.Web.Services;

public sealed partial class WorkspaceSession
{
    /// <summary>Derive detached reference text from actual owned build sources. Never saves or replaces the active sheet.</summary>
    public async Task<List<FeatureDetail>?> ReadOwnedFeatureReferencesAsync(string sourceCharacterId)
    {
        var source=Snapshot;
        if(source is null||source.Character.Id!=sourceCharacterId||rules is null||source.Character.Build is null)return null;
        try
        {
            var recalculated=await RunRules(r=>r.Recalculate(CharacterDraft.Of(source.Character),source.Character));
            if(Snapshot?.Character.Id!=sourceCharacterId||Snapshot.Revision!=source.Revision)return null;
            return recalculated.Derived.FeatureDetails;
        }
        catch(Exception error)when(IsExpected(error)||error is OperationCanceledException){log.LogInformation(error,"Feature references were not refreshed for {CharacterId}.",sourceCharacterId);return null;}
    }
}
