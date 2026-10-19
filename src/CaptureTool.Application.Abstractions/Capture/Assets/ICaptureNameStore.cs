using CaptureTool.Domain;

namespace CaptureTool.Application.Abstractions.Capture.Assets;

/// <summary>Explicit physical renames and saved names in the protected asset catalog.</summary>
public interface ICaptureNameStore
{
    /// <summary>Renames one registered file without overwriting another file, and commits its new identity path.</summary>
    Task<string> RenameFileAsync(CaptureId id, string path, string fileName, CancellationToken cancellationToken = default);
}
