using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Analysis.Memory;

internal sealed class CaptureMemorySearchProjection :
    ICaptureMemorySearchService,
    ICaptureAnalysisProjectionRefresher,
    ICaptureAnalysisProjectionMaintenance,
    IDisposable
{
    private const double ExactFilenameScore = 900;
    private const double FilenamePhraseScore = 850;
    private const double OcrPhraseScore = 700;
    private const double OcrTokenScore = 650;
    private const double DescriptionPhraseScore = 550;
    private const double DescriptionTokenScore = 500;
    private const double FilenameTokenScore = 400;
    private const double TypoPenalty = 25;
    private const double MaximumRecencyTieBreaker = 0.01;

    private readonly ICaptureAnalysisStore _metadataStore;
    private readonly ICaptureAnalysisControlStore _controlStore;
    private readonly ICaptureAssetCatalog _captureAssets;
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private readonly object _stateGate = new();
    private Dictionary<CaptureId, ProjectionEntry> _entries = [];
    private bool _isInitialized;

    public CaptureMemorySearchProjection(
        ICaptureAnalysisStore metadataStore,
        ICaptureAnalysisControlStore controlStore,
        ICaptureAssetCatalog captureAssets)
    {
        _metadataStore = metadataStore;
        _controlStore = controlStore;
        _captureAssets = captureAssets;
    }

    public async ValueTask<IReadOnlyList<CaptureMemorySearchResult>> SearchAsync(
        CaptureMemorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        CaptureMemoryNormalizedText query;
        try
        {
            query = CaptureMemoryTextNormalizer.Normalize(request.Query);
        }
        catch (ArgumentException)
        {
            return [];
        }

        if (query.Tokens.Length == 0)
        {
            return [];
        }

        ProjectionEntry[] snapshot;
        lock (_stateGate)
        {
            snapshot = _entries.Values.ToArray();
        }

        var matches = new List<SearchMatch>(Math.Min(snapshot.Length, request.MaximumResults * 2));
        for (int index = 0; index < snapshot.Length; index++)
        {
            if ((index & 63) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            SearchMatch? match = Match(snapshot[index], request.Query, query);
            if (match != null)
            {
                matches.Add(match);
            }
        }

        SearchMatch[] ranked = matches
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Entry.CapturedAtUtc)
            .ThenBy(match => match.Entry.CaptureId.ToString(), StringComparer.Ordinal)
            .Take(request.MaximumResults)
            .ToArray();

        var results = new CaptureMemorySearchResult[ranked.Length];
        for (int index = 0; index < ranked.Length; index++)
        {
            SearchMatch match = ranked[index];
            results[index] = new CaptureMemorySearchResult(
                match.Entry.CaptureId,
                match.Entry.MediaKind,
                match.Entry.CapturedAtUtc,
                match.Score,
                index + 1,
                match.Evidence);
        }

        return results;
    }

    public async ValueTask RefreshAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("A projection refresh requires a capture ID.", nameof(captureId));
        }

        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ProjectionEntry? entry = await BuildEligibleEntryAsync(captureId, cancellationToken)
                .ConfigureAwait(false);
            lock (_stateGate)
            {
                if (entry == null)
                {
                    _entries.Remove(captureId);
                }
                else
                {
                    _entries[captureId] = entry;
                }
            }
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async ValueTask RemoveAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Projection removal requires a capture ID.", nameof(captureId));
        }

        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_stateGate)
            {
                _entries.Remove(captureId);
            }
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_stateGate)
            {
                _entries = [];
                _isInitialized = true;
            }
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async ValueTask<int> RebuildAsync(CancellationToken cancellationToken = default)
    {
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RebuildCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public void Dispose()
    {
        _mutationGate.Dispose();
    }

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        lock (_stateGate)
        {
            if (_isInitialized)
            {
                return;
            }
        }

        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_stateGate)
            {
                if (_isInitialized)
                {
                    return;
                }
            }

            _ = await RebuildCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private async ValueTask<int> RebuildCoreAsync(CancellationToken cancellationToken)
    {
        CaptureAnalysisControlSnapshot initialControl = await _controlStore
            .GetAsync(cancellationToken).ConfigureAwait(false);
        HashSet<CaptureId> initiallyEligible = GetEnrolledCaptureIds(initialControl.State);
        var rebuilt = new Dictionary<CaptureId, ProjectionEntry>();

        await foreach (CaptureAnalysisStoreSnapshot snapshot in _metadataStore
            .ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CaptureId captureId = snapshot.Record.CaptureId;
            if (!initiallyEligible.Contains(captureId))
            {
                continue;
            }

            CaptureAsset? asset = GetActiveAsset(captureId);
            ProjectionEntry? entry = asset == null
                ? null
                : TryBuildEntry(snapshot.Record, asset);
            if (entry != null)
            {
                rebuilt[captureId] = entry;
            }
        }

        // Re-read the durable ledger before publishing the candidate. A concurrent exclusion,
        // forget, clear, or revoke therefore cannot be reintroduced by a stale rebuild.
        CaptureAnalysisControlSnapshot currentControl = await _controlStore
            .GetAsync(cancellationToken).ConfigureAwait(false);
        HashSet<CaptureId> currentlyEligible = GetEnrolledCaptureIds(currentControl.State);
        foreach (CaptureId captureId in rebuilt.Keys.ToArray())
        {
            if (!currentlyEligible.Contains(captureId) || GetActiveAsset(captureId) == null)
            {
                rebuilt.Remove(captureId);
            }
        }

        lock (_stateGate)
        {
            _entries = rebuilt;
            _isInitialized = true;
        }

        return rebuilt.Count;
    }

    private async ValueTask<ProjectionEntry?> BuildEligibleEntryAsync(
        CaptureId captureId,
        CancellationToken cancellationToken)
    {
        CaptureAnalysisControlSnapshot control = await _controlStore
            .GetAsync(cancellationToken).ConfigureAwait(false);
        if (!GetEnrolledCaptureIds(control.State).Contains(captureId))
        {
            return null;
        }

        CaptureAsset? asset = GetActiveAsset(captureId);
        if (asset == null)
        {
            return null;
        }

        CaptureAnalysisStoreSnapshot? metadata = await _metadataStore
            .GetAsync(captureId, cancellationToken).ConfigureAwait(false);
        return metadata == null ? null : TryBuildEntry(metadata.Record, asset);
    }

    private CaptureAsset? GetActiveAsset(CaptureId captureId)
    {
        CaptureAsset? asset = _captureAssets.Get(captureId);
        return asset is { LifecycleState: CaptureAssetLifecycleState.Active }
            ? asset
            : null;
    }

    private static HashSet<CaptureId> GetEnrolledCaptureIds(CaptureAnalysisControlState state)
    {
        return state.Enrollments
            .Where(enrollment => enrollment.State == CaptureAnalysisEnrollmentState.Enrolled)
            .Select(enrollment => enrollment.CaptureId)
            .ToHashSet();
    }

    private static ProjectionEntry? TryBuildEntry(
        CaptureAnalysisRecord record,
        CaptureAsset asset)
    {
        if (record.CaptureId != asset.Id)
        {
            return null;
        }

        try
        {
            string filename = GetPreferredFilename(asset);
            CaptureMemoryNormalizedText normalizedFilename =
                CaptureMemoryTextNormalizer.Normalize(filename);

            OcrDocumentV1? ocr = GetPayload<OcrDocumentV1>(record, AnalysisCapabilities.OcrDocumentV1);
            ImageDescriptionV1? description = GetPayload<ImageDescriptionV1>(
                record,
                AnalysisCapabilities.ImageDescriptionV1);
            CaptureMemoryNormalizedText? normalizedOcr = ocr == null
                ? null
                : CaptureMemoryTextNormalizer.Normalize(ocr.FullText);
            CaptureMemoryNormalizedText? normalizedDescription = description == null
                ? null
                : CaptureMemoryTextNormalizer.Normalize(description.Description);
            OcrEvidenceEntry[] lines = ocr == null
                ? []
                : ocr.Regions
                    .SelectMany(region => region.Lines)
                    .Where(line => !string.IsNullOrWhiteSpace(line.Text))
                    .Select(line => new OcrEvidenceEntry(
                        line.Text,
                        CaptureMemoryTextNormalizer.Normalize(line.Text),
                        new CaptureMemoryPixelBounds(
                            line.Bounds.X,
                            line.Bounds.Y,
                            line.Bounds.Width,
                            line.Bounds.Height,
                            checked((int)ocr.RasterSize.Width),
                            checked((int)ocr.RasterSize.Height))))
                    .ToArray();

            return new ProjectionEntry(
                record.CaptureId,
                record.MediaKind,
                record.CapturedAtUtc,
                filename,
                normalizedFilename,
                ocr?.FullText,
                normalizedOcr,
                lines,
                description?.Description,
                normalizedDescription);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            // The projection is disposable. A malformed derived entry is skipped without writing
            // to canonical metadata or touching the retained capture.
            return null;
        }
    }

    private static string GetPreferredFilename(CaptureAsset asset)
    {
        string? preferred = asset.PreferredOpenPath;
        string filename = preferred == null ? string.Empty : Path.GetFileName(preferred);
        return string.IsNullOrWhiteSpace(filename)
            ? Path.GetFileName(asset.RetainedSourcePath)
            : filename;
    }

    private static TPayload? GetPayload<TPayload>(
        CaptureAnalysisRecord record,
        CapabilityDefinition capability)
        where TPayload : CapabilityPayload
    {
        return record.TryGetAnalysis(capability.Id, out CapabilityAnalysis? analysis) &&
            analysis?.Capability == capability &&
            analysis.CanonicalResult?.Payload is TPayload payload
                ? payload
                : null;
    }

    private static SearchMatch? Match(
        ProjectionEntry entry,
        string rawQuery,
        CaptureMemoryNormalizedText query)
    {
        if (string.Equals(entry.FilenameNormalized.Value, query.Value, StringComparison.Ordinal))
        {
            return CreateMatch(
                entry,
                ExactFilenameScore,
                CaptureMemoryMatchKind.Filename,
                entry.Filename,
                rawQuery);
        }

        if (query.Tokens.Length > 1 && CaptureMemoryTextNormalizer.ContainsPhrase(
            entry.FilenameNormalized.Value,
            query.Value))
        {
            return CreateMatch(
                entry,
                FilenamePhraseScore,
                CaptureMemoryMatchKind.Filename,
                entry.Filename,
                rawQuery);
        }

        SearchMatch? ocr = MatchOcr(entry, rawQuery, query);
        if (ocr != null)
        {
            return ocr;
        }

        SearchMatch? description = MatchDescription(entry, rawQuery, query);
        if (description != null)
        {
            return description;
        }

        CaptureMemoryTokenMatch filenameTokens = CaptureMemoryTextNormalizer.MatchTokens(
            query.Tokens,
            entry.FilenameNormalized.TokenSet,
            entry.FilenameNormalized.Tokens);
        return filenameTokens == CaptureMemoryTokenMatch.None
            ? null
            : CreateMatch(
                entry,
                FilenameTokenScore - GetTypoPenalty(filenameTokens),
                CaptureMemoryMatchKind.Filename,
                entry.Filename,
                rawQuery);
    }

    private static SearchMatch? MatchOcr(
        ProjectionEntry entry,
        string rawQuery,
        CaptureMemoryNormalizedText query)
    {
        if (entry.OcrText == null || entry.OcrNormalized == null)
        {
            return null;
        }

        if (query.Tokens.Length > 1 && CaptureMemoryTextNormalizer.ContainsPhrase(
            entry.OcrNormalized.Value,
            query.Value))
        {
            OcrEvidenceEntry? line = entry.OcrLines.FirstOrDefault(candidate =>
                CaptureMemoryTextNormalizer.ContainsPhrase(candidate.Normalized.Value, query.Value));
            return CreateOcrMatch(entry, OcrPhraseScore, line, rawQuery);
        }

        CaptureMemoryTokenMatch tokens = CaptureMemoryTextNormalizer.MatchTokens(
            query.Tokens,
            entry.OcrNormalized.TokenSet,
            entry.OcrNormalized.Tokens);
        if (tokens == CaptureMemoryTokenMatch.None)
        {
            return null;
        }

        OcrEvidenceEntry? evidenceLine = entry.OcrLines.FirstOrDefault(candidate =>
            CaptureMemoryTextNormalizer.MatchTokens(
                query.Tokens,
                candidate.Normalized.TokenSet,
                candidate.Normalized.Tokens) != CaptureMemoryTokenMatch.None);
        return CreateOcrMatch(
            entry,
            OcrTokenScore - GetTypoPenalty(tokens),
            evidenceLine,
            rawQuery);
    }

    private static SearchMatch? MatchDescription(
        ProjectionEntry entry,
        string rawQuery,
        CaptureMemoryNormalizedText query)
    {
        if (entry.Description == null || entry.DescriptionNormalized == null)
        {
            return null;
        }

        if (query.Tokens.Length > 1 && CaptureMemoryTextNormalizer.ContainsPhrase(
            entry.DescriptionNormalized.Value,
            query.Value))
        {
            return CreateMatch(
                entry,
                DescriptionPhraseScore,
                CaptureMemoryMatchKind.ImageDescription,
                entry.Description,
                rawQuery);
        }

        CaptureMemoryTokenMatch tokens = CaptureMemoryTextNormalizer.MatchTokens(
            query.Tokens,
            entry.DescriptionNormalized.TokenSet,
            entry.DescriptionNormalized.Tokens);
        return tokens == CaptureMemoryTokenMatch.None
            ? null
            : CreateMatch(
                entry,
                DescriptionTokenScore - GetTypoPenalty(tokens),
                CaptureMemoryMatchKind.ImageDescription,
                entry.Description,
                rawQuery);
    }

    private static SearchMatch CreateOcrMatch(
        ProjectionEntry entry,
        double baseScore,
        OcrEvidenceEntry? line,
        string rawQuery)
    {
        string source = line?.Text ?? entry.OcrText!;
        return CreateMatch(
            entry,
            baseScore,
            CaptureMemoryMatchKind.OcrText,
            source,
            rawQuery,
            line?.Bounds);
    }

    private static SearchMatch CreateMatch(
        ProjectionEntry entry,
        double baseScore,
        CaptureMemoryMatchKind matchKind,
        string source,
        string rawQuery,
        CaptureMemoryPixelBounds? bounds = null)
    {
        double recency = entry.CapturedAtUtc.UtcDateTime.Ticks /
            (double)DateTime.MaxValue.Ticks * MaximumRecencyTieBreaker;
        return new SearchMatch(
            entry,
            baseScore + recency,
            new CaptureMemoryMatchEvidence(
                matchKind,
                CaptureMemoryTextNormalizer.CreateSafeSnippet(source, rawQuery),
                bounds));
    }

    private static double GetTypoPenalty(CaptureMemoryTokenMatch match)
    {
        return match == CaptureMemoryTokenMatch.SingleTypo ? TypoPenalty : 0;
    }

    private sealed record ProjectionEntry(
        CaptureId CaptureId,
        CaptureMediaKind MediaKind,
        DateTimeOffset CapturedAtUtc,
        string Filename,
        CaptureMemoryNormalizedText FilenameNormalized,
        string? OcrText,
        CaptureMemoryNormalizedText? OcrNormalized,
        IReadOnlyList<OcrEvidenceEntry> OcrLines,
        string? Description,
        CaptureMemoryNormalizedText? DescriptionNormalized);

    private sealed record OcrEvidenceEntry(
        string Text,
        CaptureMemoryNormalizedText Normalized,
        CaptureMemoryPixelBounds Bounds);

    private sealed record SearchMatch(
        ProjectionEntry Entry,
        double Score,
        CaptureMemoryMatchEvidence Evidence);
}
