namespace CaptureTool.Application.Abstractions.Settings.ClearTempFiles;

public sealed record ClearTempFilesResponse(
    int DeletedItemCount,
    long DeletedByteCount,
    int ActiveItemCount,
    long ActiveByteCount,
    int FailedItemCount)
{
    public bool Succeeded => FailedItemCount == 0;
}
