using CaptureTool.Domain.FileSystem;
using CaptureTool.Application.Abstractions.Edit;

namespace CaptureTool.Application.Abstractions.Edit.Video.OpenVideoEditPage;

public sealed record OpenVideoEditPageRequest(
    VideoFile VideoFile,
    CaptureEditorContext? EditorContext = null);
