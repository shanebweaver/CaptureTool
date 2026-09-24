using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.FileSystem;
using System.Drawing;

namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

/// <summary>A snapshot of the opened file, not a second metadata cache.</summary>
public sealed record CapturedImageTextSource(string Path, string? PersistentPath, SourceRevision Revision, Size ImageSize);

public interface ICapturedImageTextReader
{
    Task<CapturedImageTextSource?> OpenAsync(ImageFile image, Size size, CancellationToken cancellationToken);
    Task<RecognizedTextDocument?> ReadAsync(CapturedImageTextSource source, CancellationToken cancellationToken);
}
