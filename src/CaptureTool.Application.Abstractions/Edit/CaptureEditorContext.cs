using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Domain;

namespace CaptureTool.Application.Abstractions.Edit;

public sealed record CaptureEditorContext
{
    public CaptureEditorContext(
        string persistentSourcePath,
        CaptureId? captureId = null,
        CaptureMemoryMatchEvidence? initialMatch = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(persistentSourcePath);
        if (captureId is { IsEmpty: true })
        {
            throw new ArgumentException("A supplied capture ID cannot be empty.", nameof(captureId));
        }

        PersistentSourcePath = Path.GetFullPath(persistentSourcePath);
        CaptureId = captureId;
        InitialMatch = initialMatch;
    }

    public string PersistentSourcePath { get; }

    public CaptureId? CaptureId { get; }

    public CaptureMemoryMatchEvidence? InitialMatch { get; }
}
