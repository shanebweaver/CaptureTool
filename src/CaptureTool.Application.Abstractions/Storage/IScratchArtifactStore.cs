namespace CaptureTool.Application.Abstractions.Storage;

public interface IScratchArtifactStore
{
    string CreateLeasedArtifactPath(string owner, string extension);
    void DeleteArtifact(string artifactPath);
    void RelinquishArtifact(string artifactPath);
    void ClearUnleasedArtifacts();
    void ScavengeStaleArtifacts(TimeSpan maximumAge);
}
