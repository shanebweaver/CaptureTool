using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture;

/// <summary>Capture completion records authorization before handing work to asynchronous intake.</summary>
internal sealed class CaptureAnalysisIntake(ICaptureMemoryService memory)
{
    public Task RegisterAsync(string path, CaptureFileType media) => memory.RegisterCaptureAsync(
        new(CaptureId.New(), media, DateTimeOffset.UtcNow, path, CaptureSourceOwnership.Application), memory.CaptureAuthorization);

    public async Task SavedAsync(Task registration, string source, string preferred)
    {
        await registration.ConfigureAwait(false);
        await memory.SetPreferredPathAsync(source, preferred).ConfigureAwait(false);
    }
}
