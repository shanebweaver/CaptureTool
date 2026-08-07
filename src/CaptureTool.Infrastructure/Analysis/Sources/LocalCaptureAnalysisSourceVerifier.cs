using CaptureTool.Application.Abstractions.Analysis.Sources;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;
using System.Security.Cryptography;

namespace CaptureTool.Infrastructure.Analysis.Sources;

internal sealed class LocalCaptureAnalysisSourceVerifier : ICaptureAnalysisSourceVerifier
{
    private readonly ICaptureAssetCatalog _captureAssets;

    public LocalCaptureAnalysisSourceVerifier(ICaptureAssetCatalog captureAssets)
    {
        ArgumentNullException.ThrowIfNull(captureAssets);
        _captureAssets = captureAssets;
    }

    public async ValueTask<IVerifiedCaptureAnalysisSource?> TryOpenVerifiedAsync(
        CaptureAnalysisSourceVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CaptureId captureId = request.Authorization.Request.CaptureId;
        CaptureAsset? asset = _captureAssets.Get(captureId);
        if (asset is not { LifecycleState: CaptureAssetLifecycleState.Active })
        {
            return null;
        }

        long sourceGeneration = GetSourceGeneration(captureId);
        if (sourceGeneration <= 0)
        {
            return null;
        }

        FileStream? retainedLease = null;
        try
        {
            retainedLease = OpenSource(asset.RetainedSourcePath);
            ProvisionalSourceStamp before = GetStamp(retainedLease);
            byte[] fingerprint = await SHA256.HashDataAsync(retainedLease, cancellationToken)
                .ConfigureAwait(false);
            retainedLease.Position = 0;
            ProvisionalSourceStamp after = GetStamp(retainedLease);
            CaptureAsset? currentAsset = _captureAssets.Get(captureId);
            if (before != after || currentAsset is not { LifecycleState: CaptureAssetLifecycleState.Active } ||
                !string.Equals(
                    currentAsset.RetainedSourcePath,
                    asset.RetainedSourcePath,
                    StringComparison.OrdinalIgnoreCase) ||
                GetSourceGeneration(captureId) != sourceGeneration)
            {
                return null;
            }

            var sourceRevision = new SourceRevision(
                before.Length,
                before.LastWriteTimeUtc,
                ContentFingerprint.Sha256(Convert.ToHexStringLower(fingerprint)));
            var verified = new VerifiedCaptureAnalysisSource(
                captureId,
                MapMediaKind(asset.MediaType),
                sourceGeneration,
                before,
                sourceRevision,
                asset.RetainedSourcePath,
                retainedLease);
            retainedLease = null;
            return verified;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
        finally
        {
            if (retainedLease != null)
            {
                await retainedLease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private long GetSourceGeneration(CaptureId captureId)
    {
        return _captureAssets.GetChangesAfter(0)
            .Where(change => change.CaptureId == captureId && change.ChangeType is
                CaptureAssetChangeType.Finalized or
                CaptureAssetChangeType.SourceChanged or
                CaptureAssetChangeType.Deleted)
            .Select(change => change.Sequence)
            .LastOrDefault();
    }

    private static FileStream OpenSource(string path)
    {
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    private static ProvisionalSourceStamp GetStamp(FileStream stream)
    {
        var fileInfo = new FileInfo(stream.Name);
        fileInfo.Refresh();
        return new(
            stream.Length,
            new DateTimeOffset(
                DateTime.SpecifyKind(fileInfo.LastWriteTimeUtc, DateTimeKind.Utc)));
    }

    private static CaptureMediaKind MapMediaKind(CaptureFileType mediaType)
    {
        return mediaType switch
        {
            CaptureFileType.Image => CaptureMediaKind.Image,
            CaptureFileType.Audio => CaptureMediaKind.Audio,
            CaptureFileType.Video => CaptureMediaKind.Video,
            _ => throw new InvalidDataException("The retained capture source has an unknown media type."),
        };
    }

    private sealed class VerifiedCaptureAnalysisSource : IVerifiedCaptureAnalysisSource
    {
        private readonly string _path;
        private readonly FileStream _retainedLease;
        private bool _disposed;

        public VerifiedCaptureAnalysisSource(
            CaptureId captureId,
            CaptureMediaKind mediaKind,
            long captureSourceGeneration,
            ProvisionalSourceStamp sourceStamp,
            SourceRevision sourceRevision,
            string path,
            FileStream retainedLease)
        {
            CaptureId = captureId;
            MediaKind = mediaKind;
            CaptureSourceGeneration = captureSourceGeneration;
            SourceStamp = sourceStamp;
            SourceRevision = sourceRevision;
            _path = path;
            _retainedLease = retainedLease;
        }

        public CaptureId CaptureId { get; }

        public CaptureMediaKind MediaKind { get; }

        public long CaptureSourceGeneration { get; }

        public ProvisionalSourceStamp SourceStamp { get; }

        public SourceRevision SourceRevision { get; }

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            Stream stream = OpenSource(_path);
            return ValueTask.FromResult(stream);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await _retainedLease.DisposeAsync().ConfigureAwait(false);
        }
    }
}
