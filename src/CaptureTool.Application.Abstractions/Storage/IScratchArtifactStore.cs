namespace CaptureTool.Application.Abstractions.Storage;

public readonly record struct ScratchArtifactCleanupResult(
    int DeletedItemCount,
    long DeletedByteCount,
    int ActiveItemCount,
    long ActiveByteCount,
    int FailedItemCount)
{
    public bool Succeeded => FailedItemCount == 0;
}

public interface IScratchArtifactStore
{
    string CreateLeasedArtifactPath(string owner, string extension);
    void DeleteArtifact(string artifactPath);
    void RelinquishArtifact(string artifactPath);
    ScratchArtifactCleanupResult ClearUnleasedArtifacts();
    void ScavengeStaleArtifacts(TimeSpan maximumAge);
}
