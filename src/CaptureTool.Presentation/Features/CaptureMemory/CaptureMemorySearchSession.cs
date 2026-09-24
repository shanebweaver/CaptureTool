using CaptureTool.Domain;

namespace CaptureTool.Presentation.Features.CaptureMemory;

/// <summary>In-memory navigation state only. Does not retain media, snippets or file paths.</summary>
public sealed class CaptureMemorySearchSession
{
    public string Query { get; set; } = string.Empty;
    public CaptureId? SelectedCaptureId { get; set; }
    public double VerticalOffset { get; set; }
}
