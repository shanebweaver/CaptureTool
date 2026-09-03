using CaptureTool.Domain.FileSystem;
using CaptureTool.Application.Abstractions.Edit;

namespace CaptureTool.Application.Abstractions.Edit.Audio.OpenAudioEditPage;

public sealed record OpenAudioEditPageRequest(
    AudioFile AudioFile,
    CaptureEditorContext? EditorContext = null);
