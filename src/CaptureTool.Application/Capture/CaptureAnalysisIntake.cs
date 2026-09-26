using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture;

/// <summary>Records capture identity and saved aliases without scheduling analysis.</summary>
internal sealed class CaptureAnalysisIntake(ICaptureMemoryService memory)
{
    public Task RegisterAsync(string path, CaptureFileType media) => memory.RegisterCaptureAsync(
        new(CaptureId.New(), media, DateTimeOffset.UtcNow, path, CaptureSourceOwnership.Application));

    public async Task SavedAsync(Task registration, string source, string preferred)
    {
        await registration.ConfigureAwait(false);
        await memory.SetPreferredPathAsync(source, preferred).ConfigureAwait(false);
    }
}
