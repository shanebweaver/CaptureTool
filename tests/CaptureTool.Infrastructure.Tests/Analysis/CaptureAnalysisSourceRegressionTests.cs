using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.Analysis.Sources;

namespace CaptureTool.Infrastructure.Tests.Analysis;

public sealed partial class CaptureAnalysisWorkerTests
{
    [TestMethod]
    public async Task EmptySourceDoesNotDiscardOtherQueuedCaptures()
    {
        using var fixture = new Fixture();
        var empty = await fixture.EnqueueAsync(Ct);
        var healthy = await fixture.EnqueueAsync(Ct);
        await File.WriteAllBytesAsync(empty.SourcePath, [], Ct);
        await File.WriteAllTextAsync(healthy.SourcePath, "valid source", Ct);
        using var worker = new CaptureAnalysisWorker(fixture.Store, fixture.Authorization, new LocalAnalysisSource(),
            fixture.Configuration, fixture.Adapters, fixture.Catalog);
        empty = await EnqueueRealSourceAsync(worker, empty);
        healthy = await EnqueueRealSourceAsync(worker, healthy);

        await DrainAsync(worker, Ct);

        Assert.AreEqual(AnalysisRunStatus.InvalidSource, (await fixture.Store.GetWorkAsync(empty.CaptureId, Ct))!.Run.Status);
        Assert.AreEqual(AnalysisRunStatus.Completed, (await fixture.Store.GetWorkAsync(healthy.CaptureId, Ct))!.Run.Status);
        CollectionAssert.AreEqual(new[] { "preferred", "description" }, fixture.Calls.ToArray());
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task QueuedAnalysisFollowsRenameOfTheSelectedCopy(bool preferred, bool reuseOldPath)
    {
        using var fixture = new Fixture();
        var request = await fixture.EnqueueAsync(Ct, capabilities: [AnalysisCapability.Description]);
        string saved = Path.Combine(fixture.Environment.Root, "saved.png");
        await File.WriteAllTextAsync(request.SourcePath, "retained original", Ct);
        await File.WriteAllTextAsync(saved, "edited saved copy", Ct);
        await fixture.Catalog.RegisterAsync(new(request.CaptureId, CaptureFileType.Image, null,
            request.SourcePath, CaptureSourceOwnership.Application, saved), Ct);
        using var worker = new CaptureAnalysisWorker(fixture.Store, fixture.Authorization, new LocalAnalysisSource(),
            fixture.Configuration, fixture.Adapters, fixture.Catalog);
        request = await EnqueueRealSourceAsync(worker,
            request with { SourcePath = preferred ? saved : request.SourcePath, UsePreferredPath = preferred });
        string renamed = await fixture.Catalog.RenameFileAsync(request.CaptureId, request.SourcePath, "renamed.png", Ct);
        if (reuseOldPath)
        {
            await File.WriteAllTextAsync(request.SourcePath, "unrelated replacement", Ct);
            renamed = await fixture.Catalog.RenameFileAsync(request.CaptureId, renamed, "renamed-again.png", Ct);
        }
        string? analyzed = null;
        fixture.Description.Execute = async (input, ct) =>
        {
            Assert.AreEqual(renamed, input.SourcePath);
            analyzed = await File.ReadAllTextAsync(input.SourcePath, ct);
            return fixture.Description.Success();
        };
        await DrainAsync(worker, Ct);

        Assert.AreEqual(preferred ? "edited saved copy" : "retained original", analyzed);
        Assert.AreEqual(AnalysisRunStatus.Completed, (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!.Run.Status);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task QueuedAnalysisDoesNotSwitchToAnotherPreferredCopy(bool deleteSelected)
    {
        using var fixture = new Fixture();
        var request = await fixture.EnqueueAsync(Ct, capabilities: [AnalysisCapability.Description]);
        string selected = Path.Combine(fixture.Environment.Root, "selected.png");
        string newer = Path.Combine(fixture.Environment.Root, "newer.png");
        await File.WriteAllTextAsync(selected, "selected copy", Ct);
        // Equal bytes must not make a different file an acceptable replacement.
        await File.WriteAllTextAsync(newer, "selected copy", Ct);
        await fixture.Catalog.RegisterAsync(new(request.CaptureId, CaptureFileType.Image, null,
            request.SourcePath, CaptureSourceOwnership.Application, selected), Ct);
        using var worker = new CaptureAnalysisWorker(fixture.Store, fixture.Authorization, new LocalAnalysisSource(),
            fixture.Configuration, fixture.Adapters, fixture.Catalog);
        request = await EnqueueRealSourceAsync(worker,
            request with { SourcePath = selected, UsePreferredPath = true });
        await fixture.Catalog.SetPreferredPathAsync(request.CaptureId, newer, Ct);
        if (deleteSelected) File.Delete(selected);
        string? analyzed = null;
        fixture.Description.Execute = async (input, ct) =>
        {
            analyzed = await File.ReadAllTextAsync(input.SourcePath, ct);
            return fixture.Description.Success();
        };
        await DrainAsync(worker, Ct);

        Assert.AreEqual(deleteSelected ? null : "selected copy", analyzed);
        Assert.AreEqual(deleteSelected ? AnalysisRunStatus.InvalidSource : AnalysisRunStatus.Completed,
            (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!.Run.Status);
    }

    [TestMethod]
    public async Task ReplayingQueuedRequestDoesNotAuthorizeReplacementFile()
    {
        using var fixture = new Fixture();
        var request = await fixture.EnqueueAsync(Ct, capabilities: [AnalysisCapability.Description]);
        await File.WriteAllTextAsync(request.SourcePath, "selected content", Ct);
        using var worker = new CaptureAnalysisWorker(fixture.Store, fixture.Authorization, new LocalAnalysisSource(),
            fixture.Configuration, fixture.Adapters, fixture.Catalog);
        request = await EnqueueRealSourceAsync(worker, request);
        File.Move(request.SourcePath, request.SourcePath + ".moved");
        await File.WriteAllTextAsync(request.SourcePath, "selected content", Ct);

        Assert.IsTrue(await worker.EnqueueAsync(request, Ct));
        await DrainAsync(worker, Ct);

        Assert.IsEmpty(fixture.Calls);
        Assert.AreEqual(AnalysisRunStatus.InvalidSource, (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!.Run.Status);
    }

    [TestMethod]
    public async Task MissingFileAtAdmissionCannotBeReplacedBeforeExecution()
    {
        using var fixture = new Fixture();
        var request = await fixture.EnqueueAsync(Ct, capabilities: [AnalysisCapability.Description]);
        using var worker = new CaptureAnalysisWorker(fixture.Store, fixture.Authorization, new LocalAnalysisSource(),
            fixture.Configuration, fixture.Adapters, fixture.Catalog);
        request = await EnqueueRealSourceAsync(worker, request);
        await File.WriteAllTextAsync(request.SourcePath, "late replacement", Ct);

        await DrainAsync(worker, Ct);

        Assert.IsEmpty(fixture.Calls);
        Assert.AreEqual(AnalysisRunStatus.InvalidSource, (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!.Run.Status);
    }

    private async Task<AnalysisRequest> EnqueueRealSourceAsync(CaptureAnalysisWorker worker, AnalysisRequest request)
    {
        request = request with { RequestId = Guid.NewGuid(), ExpectedRunId = request.RequestId };
        Assert.IsTrue(await worker.EnqueueAsync(request, Ct));
        return request;
    }
}
