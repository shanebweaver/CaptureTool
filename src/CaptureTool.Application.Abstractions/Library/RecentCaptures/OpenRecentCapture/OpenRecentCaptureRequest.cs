using CaptureTool.Application.Abstractions.Edit;

namespace CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;

public sealed record OpenRecentCaptureRequest(
    string FilePath,
    CaptureEditorContext? EditorContext = null);
