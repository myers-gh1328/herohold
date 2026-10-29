using Herohold.Core;
using Herohold.Web.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Herohold.Integration.Tests;

/// <summary>The session reaches storage only through <see cref="IWorkspaceStore"/>.</summary>
[TestClass]
public sealed class WorkspaceStoreSeamTests
{
    private sealed class MemoryStore : IWorkspaceStore
    {
        public readonly Dictionary<string, WorkspaceSnapshot> Saved = [];
        public Task<IReadOnlyList<WorkspaceSummary>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkspaceSummary>>([.. Saved.Values.Select(s => new WorkspaceSummary(s.Character.Id, s.Character.Name, s.Character.Species, s.Character.ClassName, s.Character.Level, s.Revision))]);
        public Task<WorkspaceSnapshot?> LoadAsync(string id, CancellationToken ct = default) => Task.FromResult(Saved.GetValueOrDefault(id));
        public Task<WorkspaceSnapshot> SaveAsync(WorkspaceSnapshot snapshot, long expectedRevision, CancellationToken ct = default)
        {
            if ((Saved.GetValueOrDefault(snapshot.Character.Id)?.Revision ?? 0) != expectedRevision) throw new WorkspaceConflictException("Stale.");
            var next = snapshot with { Revision = expectedRevision + 1 };
            Saved[next.Character.Id] = next;
            return Task.FromResult(next);
        }
        public void Validate(WorkspaceSnapshot snapshot) { }
    }

    [TestMethod]
    public async Task ANoteIsSavedToWhicheverStoreTheSessionWasGiven()
    {
        var store = new MemoryStore();
        var session = new WorkspaceSession(store);
        await session.InitializeAsync();
        await session.AddNoteAsync("At the bridge", "Ask the ferryman.");
        Assert.IsNull(session.Error);
        var saved = store.Saved[session.Character!.Id];
        Assert.AreEqual(session.Snapshot!.Revision, saved.Revision);
        Assert.IsTrue(saved.Character.Notes.Any(n => n.Title == "At the bridge"));
    }
}
