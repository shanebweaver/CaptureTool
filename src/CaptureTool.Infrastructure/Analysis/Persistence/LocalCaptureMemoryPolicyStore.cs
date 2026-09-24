using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Infrastructure.Persistence;
using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.Analysis.Persistence;

internal sealed class LocalCaptureMemoryPolicyStore : ICaptureMemoryPolicyStore
{
    private readonly string _path;
    private readonly ProtectedDocumentFile _documents;
    public LocalCaptureMemoryPolicyStore(IStorageService storage, IUserDataProtector protector)
        : this(storage, protector, new LocalProtectedFileSystem()) { }
    internal LocalCaptureMemoryPolicyStore(IStorageService storage, IUserDataProtector protector, IProtectedFileSystem files)
    {
        _path = Path.Combine(storage.GetApplicationDataFolderPath(), "CaptureMemoryPolicy.bin");
        _documents = new(protector, files);
    }
    public async Task<CaptureMemoryPolicy?> LoadAsync(CancellationToken cancellationToken)
    {
        CaptureMemoryPolicyDocument? document = await _documents.ReadAsync(_path, CaptureMemoryPolicyJsonContext.Default.CaptureMemoryPolicyDocument, cancellationToken).ConfigureAwait(false);
        if (document == null) return null;
        if (document.Version is not (1 or 2) || document.Policy == null) throw new InvalidDataException("Unsupported capture memory policy.");
        Validate(document.Policy);
        if (document.Version == 1)
        {
            // Earlier consent covered background analysis only, not every AI tool.
            // Preserve media/metadata, but ask once for the expanded consent scope.
            var migrated = new CaptureMemoryPolicy(false, false, Guid.NewGuid(), document.Policy.EnableBoundary);
            await SaveAsync(migrated, cancellationToken).ConfigureAwait(false);
            return migrated;
        }
        return document.Policy;
    }
    public Task SaveAsync(CaptureMemoryPolicy policy, CancellationToken cancellationToken)
    {
        Validate(policy);
        return _documents.WriteAsync(_path, new CaptureMemoryPolicyDocument(2, policy), CaptureMemoryPolicyJsonContext.Default.CaptureMemoryPolicyDocument, cancellationToken);
    }
    private static void Validate(CaptureMemoryPolicy policy)
    {
        if (policy.Revision == Guid.Empty || policy.EnableBoundary < 0 || policy.ScanningEnabled && !policy.ConsentGranted)
            throw new InvalidDataException("Invalid capture memory policy.");
    }
}
internal sealed record CaptureMemoryPolicyDocument(int Version, CaptureMemoryPolicy Policy);
[JsonSerializable(typeof(CaptureMemoryPolicyDocument))]
internal partial class CaptureMemoryPolicyJsonContext : JsonSerializerContext;
