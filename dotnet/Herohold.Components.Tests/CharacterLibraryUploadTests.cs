using Bunit;
using Herohold.Core;
using Herohold.TestSupport;
using Herohold.Web.Components.Shared;
using Herohold.Web.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Herohold.Components.Tests;

[TestClass]
public sealed class CharacterLibraryUploadTests
{
    /// <summary>Holds every save until released, so a test can act while Herohold is still saving.</summary>
    private sealed class HeldStore(IWorkspaceStore inner) : IWorkspaceStore
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Holding { get; set; }
        public Task<IReadOnlyList<WorkspaceSummary>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
        public Task<WorkspaceSnapshot?> LoadAsync(string id, CancellationToken ct = default) => inner.LoadAsync(id, ct);
        public async Task<WorkspaceSnapshot> SaveAsync(WorkspaceSnapshot snapshot, long expectedRevision, CancellationToken ct = default)
        { if (Holding) await Release.Task; return await inner.SaveAsync(snapshot, expectedRevision, ct); }
        public void Validate(WorkspaceSnapshot snapshot) => inner.Validate(snapshot);
    }

    [TestMethod]
    public async Task AFileChosenWhileSavingIsReportedNotSilentlyIgnored()
    {
        var directory = Path.Combine(Path.GetTempPath(), "herohold-library-upload-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new HeldStore(HandMadeStore.At(directory));
            await store.SaveAsync(SampleWorkspace.Create(), 0);
            var session = new WorkspaceSession(store);
            await session.InitializeAsync();
            using var context = new BunitContext();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton(session);
            var cut = context.Render<CharacterLibrary>(p => p.Add(x => x.Session, session).Add(x => x.ShowImport, true));

            store.Holding = true;
            var saving = session.AddNoteAsync("While saving", "Still writing.");
            Assert.IsTrue(session.IsBusy);
            cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("{}", "late.json"));
            cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Herohold is still saving. Choose the file again when it finishes."));

            store.Release.SetResult();
            await saving;
            Assert.IsNull(session.Error, session.Error);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
