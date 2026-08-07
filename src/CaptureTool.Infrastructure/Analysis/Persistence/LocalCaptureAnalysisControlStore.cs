using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Persistence.Serialization;
using System.Security.Cryptography;

namespace CaptureTool.Infrastructure.Analysis.Persistence;

internal sealed class LocalCaptureAnalysisControlStore :
    ICaptureAnalysisControlStore,
    IDisposable
{
    internal const string ControlDirectoryName = "CaptureAnalysis";
    internal const string ControlVersionDirectoryName = "control-v1";
    internal const string ControlFileName = "control.ledger";
    internal const int CurrentSchemaVersion = 1;
    internal const long InitialDocumentRevision = 1;

    private readonly IStorageService _storageService;
    private readonly IUserDataProtectionService _dataProtectionService;
    private readonly IAtomicFileWriter _atomicFileWriter;
    private readonly ILogService _logService;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CaptureAnalysisControlSnapshot? _snapshot;
    private Exception? _loadFailure;
    private bool _isLoaded;
    private bool _isReadOnlyVersion;

    public LocalCaptureAnalysisControlStore(
        IStorageService storageService,
        IUserDataProtectionService dataProtectionService,
        IAtomicFileWriter atomicFileWriter,
        ILogService logService)
    {
        _storageService = storageService;
        _dataProtectionService = dataProtectionService;
        _atomicFileWriter = atomicFileWriter;
        _logService = logService;
    }

    public async ValueTask<CaptureAnalysisControlSnapshot> GetAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureLoaded();
            return _snapshot!;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<CaptureAnalysisControlWriteResult> TryWriteAsync(
        CaptureAnalysisControlState state,
        long expectedDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (expectedDocumentRevision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedDocumentRevision));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                EnsureLoaded();
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Capture Analysis control state is unavailable.");
                return new(CaptureAnalysisControlWriteStatus.Unavailable);
            }

            if (_isReadOnlyVersion)
            {
                return new(CaptureAnalysisControlWriteStatus.ReadOnlyVersion);
            }

            CaptureAnalysisControlSnapshot current = _snapshot!;
            if (current.DocumentRevision != expectedDocumentRevision)
            {
                return new(CaptureAnalysisControlWriteStatus.Conflict, current);
            }

            cancellationToken.ThrowIfCancellationRequested();
            long nextRevision;
            try
            {
                nextRevision = checked(current.DocumentRevision + 1);
            }
            catch (OverflowException ex)
            {
                _logService.LogException(ex, "Capture Analysis control revision is exhausted.");
                return new(CaptureAnalysisControlWriteStatus.Unavailable);
            }

            byte[] plaintext = CaptureAnalysisDocumentSerializer.SerializeControl(
                state,
                nextRevision,
                CurrentSchemaVersion);
            try
            {
                byte[] protectedBytes = _dataProtectionService.Protect(plaintext);
                _atomicFileWriter.Write(GetControlFilePath(), protectedBytes);
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Failed to persist Capture Analysis control state.");
                return new(CaptureAnalysisControlWriteStatus.Unavailable);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            // No cancellation check follows the durable replace: callers must observe the committed winner.
            var committed = new CaptureAnalysisControlSnapshot(nextRevision, state);
            _snapshot = committed;
            return new(CaptureAnalysisControlWriteStatus.Succeeded, committed);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    internal string GetControlFilePath()
    {
        return Path.Combine(
            _storageService.GetApplicationDataFolderPath(),
            ControlDirectoryName,
            ControlVersionDirectoryName,
            ControlFileName);
    }

    private void EnsureLoaded()
    {
        if (_isLoaded)
        {
            if (_loadFailure != null)
            {
                throw new InvalidDataException(
                    "Capture Analysis control state could not be restored safely.",
                    _loadFailure);
            }

            return;
        }

        _isLoaded = true;
        string filePath = GetControlFilePath();
        if (!File.Exists(filePath))
        {
            _snapshot = CreateFailClosedSnapshot(InitialDocumentRevision);
            return;
        }

        byte[]? plaintext = null;
        try
        {
            byte[] protectedBytes = File.ReadAllBytes(filePath);
            plaintext = _dataProtectionService.Unprotect(protectedBytes);
            CaptureAnalysisDocumentHeader header =
                CaptureAnalysisDocumentSerializer.ReadHeader(plaintext);
            if (header.SchemaVersion != CurrentSchemaVersion)
            {
                _isReadOnlyVersion = true;
                _snapshot = CreateFailClosedSnapshot(header.DocumentRevision);
                return;
            }

            CaptureAnalysisControlSnapshot snapshot =
                CaptureAnalysisDocumentSerializer.DeserializeControl(plaintext);
            if (snapshot.DocumentRevision != header.DocumentRevision)
            {
                throw new InvalidDataException("Capture Analysis control revisions disagree.");
            }

            _snapshot = snapshot;
        }
        catch (Exception ex)
        {
            _loadFailure = ex;
            _logService.LogException(
                ex,
                "Failed closed while restoring protected Capture Analysis control state.");
            throw new InvalidDataException(
                "Capture Analysis control state could not be restored safely.",
                ex);
        }
        finally
        {
            if (plaintext != null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    private static CaptureAnalysisControlSnapshot CreateFailClosedSnapshot(long documentRevision)
    {
        return new(
            documentRevision,
            new CaptureAnalysisControlState(CaptureAnalysisPolicy.Unknown, []));
    }
}
