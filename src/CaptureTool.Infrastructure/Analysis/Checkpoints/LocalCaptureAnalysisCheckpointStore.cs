using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Checkpoints;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain;
using CaptureTool.Infrastructure.Analysis.Persistence;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace CaptureTool.Infrastructure.Analysis.Checkpoints;

internal sealed class LocalCaptureAnalysisCheckpointStore :
    ICaptureAnalysisCheckpointStore,
    IDisposable
{
    internal const string CheckpointsVersionDirectoryName = "checkpoints-v1";
    internal const string CheckpointExtension = ".checkpoint";
    internal const int MaximumPayloadBytes = 64 * 1024 * 1024;

    private const string CaptureDirectoryDomain = "capture-analysis-checkpoint-capture/v1/";
    private const string CheckpointDomain = "capture-analysis-checkpoint/v1/";
    private const int HeaderLength = 8 + 32 + sizeof(int);
    private static ReadOnlySpan<byte> Magic => "CTCHK001"u8;

    private readonly IApplicationLocalCachePathProvider _localCachePathProvider;
    private readonly IUserDataProtectionService _dataProtectionService;
    private readonly IAtomicFileWriter _atomicFileWriter;
    private readonly ILogService _logService;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalCaptureAnalysisCheckpointStore(
        IApplicationLocalCachePathProvider localCachePathProvider,
        IUserDataProtectionService dataProtectionService,
        IAtomicFileWriter atomicFileWriter,
        ILogService logService)
    {
        _localCachePathProvider = localCachePathProvider;
        _dataProtectionService = dataProtectionService;
        _atomicFileWriter = atomicFileWriter;
        _logService = logService;
    }

    public ICaptureAnalyzerCheckpoint Open(CaptureAnalysisCheckpointKey key)
    {
        if (key.CaptureId.IsEmpty)
        {
            throw new ArgumentException("A checkpoint key is required.", nameof(key));
        }

        return new Checkpoint(this, key);
    }

    public async ValueTask DeleteCaptureAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("A capture ID is required.", nameof(captureId));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string directory = GetCaptureDirectoryPath(captureId);
            EnsureManagedPath(directory, GetRootPath());
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string root = GetRootPath();
            EnsureManagedPath(root, Path.GetDirectoryName(root)!);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<int> PruneAsync(
        DateTimeOffset olderThanUtc,
        CancellationToken cancellationToken = default)
    {
        if (olderThanUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "A checkpoint cutoff must be expressed in UTC.",
                nameof(olderThanUtc));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string root = GetRootPath();
            if (!Directory.Exists(root))
            {
                return 0;
            }

            int removed = 0;
            foreach (string path in Directory.EnumerateFiles(
                root,
                $"*{CheckpointExtension}",
                SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureManagedPath(path, root);
                if (File.GetLastWriteTimeUtc(path) < olderThanUtc.UtcDateTime)
                {
                    File.Delete(path);
                    removed++;
                }
            }

            foreach (string directory in Directory.EnumerateDirectories(root))
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }

            return removed;
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

    private async ValueTask<ReadOnlyMemory<byte>?> ReadAsync(
        CaptureAnalysisCheckpointKey key,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string path = GetCheckpointPath(key);
            if (!File.Exists(path))
            {
                return null;
            }

            byte[]? plaintext = null;
            try
            {
                plaintext = _dataProtectionService.Unprotect(File.ReadAllBytes(path));
                byte[] expectedKeyHash = GetCheckpointKeyHash(key);
                if (plaintext.Length < HeaderLength ||
                    !plaintext.AsSpan(0, Magic.Length).SequenceEqual(Magic) ||
                    !plaintext.AsSpan(Magic.Length, expectedKeyHash.Length)
                        .SequenceEqual(expectedKeyHash))
                {
                    throw new InvalidDataException("A checkpoint identity is inconsistent.");
                }

                int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(
                    plaintext.AsSpan(Magic.Length + expectedKeyHash.Length, sizeof(int)));
                if (payloadLength < 0 || payloadLength > MaximumPayloadBytes ||
                    plaintext.Length != HeaderLength + payloadLength)
                {
                    throw new InvalidDataException("A checkpoint payload length is invalid.");
                }

                return plaintext.AsMemory(HeaderLength, payloadLength).ToArray();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logService.LogException(
                    exception,
                    "Discarding an unreadable Capture Analysis checkpoint.");
                TryDelete(path);
                return null;
            }
            finally
            {
                if (plaintext != null)
                {
                    CryptographicOperations.ZeroMemory(plaintext);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask WriteAsync(
        CaptureAnalysisCheckpointKey key,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (payload.Length > MaximumPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                $"Analyzer checkpoints are limited to {MaximumPayloadBytes} bytes.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] keyHash = GetCheckpointKeyHash(key);
            byte[] plaintext = new byte[HeaderLength + payload.Length];
            try
            {
                Magic.CopyTo(plaintext);
                keyHash.CopyTo(plaintext, Magic.Length);
                BinaryPrimitives.WriteInt32LittleEndian(
                    plaintext.AsSpan(Magic.Length + keyHash.Length, sizeof(int)),
                    payload.Length);
                payload.Span.CopyTo(plaintext.AsSpan(HeaderLength));
                string path = GetCheckpointPath(key);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                _atomicFileWriter.Write(path, _dataProtectionService.Protect(plaintext));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask DeleteAsync(
        CaptureAnalysisCheckpointKey key,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TryDelete(GetCheckpointPath(key));
        }
        finally
        {
            _gate.Release();
        }
    }

    private string GetCheckpointPath(CaptureAnalysisCheckpointKey key)
    {
        string path = Path.Combine(
            GetCaptureDirectoryPath(key.CaptureId),
            Convert.ToHexStringLower(GetCheckpointKeyHash(key)) + CheckpointExtension);
        EnsureManagedPath(path, GetRootPath());
        return path;
    }

    private string GetCaptureDirectoryPath(CaptureId captureId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            CaptureDirectoryDomain + captureId));
        string path = Path.Combine(GetRootPath(), Convert.ToHexStringLower(hash));
        EnsureManagedPath(path, GetRootPath());
        return path;
    }

    private string GetRootPath()
    {
        return Path.Combine(
            _localCachePathProvider.GetApplicationLocalCacheFolderPath(),
            LocalCaptureAnalysisStore.AnalysisDirectoryName,
            CheckpointsVersionDirectoryName);
    }

    private static byte[] GetCheckpointKeyHash(CaptureAnalysisCheckpointKey key)
    {
        string canonical = string.Join(
            '|',
            key.CaptureId,
            key.SourceRevision.Length,
            key.SourceRevision.LastWriteTimeUtc.UtcTicks,
            key.SourceRevision.Fingerprint,
            key.Capability.Id,
            key.Capability.SchemaVersion,
            key.Capability.Classification,
            key.AnalyzerRevision);
        return SHA256.HashData(Encoding.UTF8.GetBytes(CheckpointDomain + canonical));
    }

    private static void EnsureManagedPath(string path, string root)
    {
        string fullPath = Path.GetFullPath(path);
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A checkpoint path escaped its managed root.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Checkpoints are disposable app-created state; cleanup is retried by later lifecycle work.
        }
    }

    private sealed class Checkpoint(
        LocalCaptureAnalysisCheckpointStore store,
        CaptureAnalysisCheckpointKey key) : ICaptureAnalyzerCheckpoint
    {
        public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(
            CancellationToken cancellationToken = default)
        {
            return store.ReadAsync(key, cancellationToken);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> payload,
            CancellationToken cancellationToken = default)
        {
            return store.WriteAsync(key, payload, cancellationToken);
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            return store.DeleteAsync(key, cancellationToken);
        }
    }
}
