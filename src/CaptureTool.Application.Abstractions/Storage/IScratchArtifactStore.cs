namespace CaptureTool.Application.Abstractions.Storage;

public interface IScratchArtifactStore
{
    string CreateLeasedArtifactPath(string owner, string extension);
    /// <summary>Prunes this owner's unleased artifacts and refuses allocation when its retained artifact limit is reached.</summary>
    string CreateLeasedArtifactPath(string owner, string extension, int maximumRetainedArtifacts);
    void DeleteArtifact(string artifactPath);
    void RelinquishArtifact(string artifactPath);
    void ClearUnleasedArtifacts();
    void ScavengeStaleArtifacts(TimeSpan maximumAge);
}
