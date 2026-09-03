using CaptureTool.Domain.FileSystem;
using CaptureTool.Application.Abstractions.Edit;

namespace CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;

public sealed record OpenImageEditPageRequest(
    ImageFile ImageFile,
    CaptureEditorContext? EditorContext = null);
