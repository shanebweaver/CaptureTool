using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Edit.Image.TextExtraction;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.FileSystem;
using System.Drawing;
using System.Threading.Channels;

namespace CaptureTool.Application.Analysis;

internal sealed partial class CaptureMemoryService
{
    public async Task<TextExtractionResult?> TryExtractAsync(ImageFile image, Size imageSize, CancellationToken cancellationToken = default)
    {
        string path = image.PersistentFilePath ?? image.FilePath;
        string workingPath = image.FilePath;
        if (_imageTextSource == null || !Path.IsPathFullyQualified(path) || !Path.IsPathFullyQualified(workingPath))
        {
            return null;
        }

        CancellationToken revoked;
        using (var grant = await _authorization.AcquireAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!grant.IsAllowed)
            {
                return TextExtractionResult.NotReady;
            }
            revoked = grant.Revoked;
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token, revoked);
        CancellationToken ct = linked.Token;

        // A captured image may be edited from a temporary copy. Only associate its
        // recognition with the saved capture when both still contain identical bytes.
        await using var source = await TryOpenImageTextSourceAsync(path, ct).ConfigureAwait(false);
        if (source == null)
        {
            return null;
        }
        bool separateCopy = !SamePath(path, workingPath);
        await using var working = separateCopy ? await TryOpenImageTextSourceAsync(workingPath, ct).ConfigureAwait(false) : null;
        if (separateCopy && working?.Revision != source.Revision)
        {
            return null;
        }

        var scope = await _store.GetAdmissionScopeAsync(ct).ConfigureAwait(false);
        long epoch = Interlocked.Read(ref _epoch);
        var changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        void OnChanged() => changes.Writer.TryWrite(true);
        AnalysisRunToken? requested = null;
        AnalysisRunToken? observed = null;
        bool attempted = false;
        StateChanged += OnChanged;
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (!IsCurrentEpoch(epoch) || Volatile.Read(ref _deleting) != 0 ||
                    scope.Generation != (await _store.GetAdmissionScopeAsync(ct).ConfigureAwait(false)).Generation)
                {
                    return TextExtractionResult.Cancelled;
                }

                var assets = await _catalog.ReadAllAsync(ct).ConfigureAwait(false);
                var matches = assets.Where(asset => SamePath(asset.SourcePath, path) || SamePath(asset.PreferredPath, path)).Take(2).ToArray();
                if (matches.Length > 1)
                {
                    return TextExtractionResult.Failed();
                }
                var asset = matches.SingleOrDefault();
                var work = asset == null ? null : await _store.GetWorkAsync(asset.Id, ct).ConfigureAwait(false);
                var record = asset == null ? null : await _store.GetAsync(asset.Id, source.Revision, ct).ConfigureAwait(false);
                RecognizedTextDocument? document = BuildImageTextDocument(record, imageSize);
                bool complete = document is { HasTextResults: true, HasQrCodeResults: true };
                if (complete || attempted && work?.Run.IsPending != true)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!await source.VerifyAsync(ct).ConfigureAwait(false) || working != null && !await working.VerifyAsync(ct).ConfigureAwait(false) ||
                        scope.Generation != (await _store.GetAdmissionScopeAsync(ct).ConfigureAwait(false)).Generation ||
                        !IsCurrentEpoch(epoch) || Volatile.Read(ref _deleting) != 0 || !_authorization.IsAllowed)
                    {
                        return TextExtractionResult.Cancelled;
                    }
                    ct.ThrowIfCancellationRequested();

                    if (!complete && work?.Run.Status == AnalysisRunStatus.Cancelled)
                    {
                        return TextExtractionResult.Cancelled;
                    }
                    return document == null ? TextExtractionResult.Failed() : TextExtractionResult.Success(document);
                }

                if (work?.Run.IsPending == true)
                {
                    if (State.Activity.Activity == AnalysisActivity.StorageUnavailable)
                    {
                        return TextExtractionResult.Failed();
                    }
                    observed = work.Token;
                    // Subscribe before reading state so even a very fast completion wakes us.
                    await changes.Reader.ReadAsync(ct).ConfigureAwait(false);
                    continue;
                }
                if (observed != null && work?.Token == observed && work.Run.Status == AnalysisRunStatus.Cancelled)
                {
                    return TextExtractionResult.Cancelled;
                }

                attempted = true;
                await RunCommandAsync(() => RequestAnalysisCoreAsync(path, AnalysisCapability.TextRecognition, true, epoch, ct,
                    token => requested = token), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (requested != null)
            {
                // A toolbar cancellation must not stop a pre-existing scan we joined,
                // or a newer request that replaced ours while cancellation was pending.
                await RunSafelyAsync(token => RunCommandAsync(async () =>
                {
                    if ((await _store.GetWorkAsync(requested.CaptureId, token).ConfigureAwait(false))?.Token == requested)
                    {
                        await _worker.CancelAsync(requested.CaptureId, token).ConfigureAwait(false);
                    }
                }, token), _lifetime.Token).ConfigureAwait(false);
            }
            throw;
        }
        finally
        {
            StateChanged -= OnChanged;
        }
    }

    private async Task<IAnalysisSourceLease?> TryOpenImageTextSourceAsync(string path, CancellationToken ct)
    {
        try { return await _imageTextSource!.OpenAsync(path, ct).ConfigureAwait(false); }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException) { return null; }
    }

    private static RecognizedTextDocument? BuildImageTextDocument(CaptureAnalysisRecord? record, Size size)
    {
        if (record?.MediaKind != AnalysisMediaKind.Image)
        {
            return null;
        }
        var text = record.Results.Select(result => result.Payload).OfType<TextRecognitionMetadata>().SingleOrDefault();
        var qr = record.Results.Select(result => result.Payload).OfType<QrCodeMetadata>().SingleOrDefault();
        if (text == null && qr == null)
        {
            return null;
        }

        RectangleF Bounds(NormalizedBounds? bounds) => bounds == null ? RectangleF.Empty :
            new((float)bounds.X * size.Width, (float)bounds.Y * size.Height, (float)bounds.Width * size.Width, (float)bounds.Height * size.Height);
        return new RecognizedTextDocumentBuilder().Build(size,
            text?.Regions.Select(region => new RecognizedTextRegion(region.Text, Bounds(region.Bounds), region.LineIndex ?? -1, region.WordIndex ?? -1)).ToArray(),
            qr?.Codes.Select(code => new RecognizedQrCodeRegion(code.Value, Bounds(code.Bounds))).ToArray());
    }
}
