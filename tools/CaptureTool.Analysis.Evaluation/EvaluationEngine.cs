namespace CaptureTool.Analysis.Evaluation;

public static class EvaluationEngine
{
    private const long FiftyMegabytes = 50L * 1024L * 1024L;

    public static EvaluationReport Evaluate(
        EvaluationCorpus corpus,
        ProviderEvaluationRun run,
        string corpusSha256,
        string runConfigurationSha256,
        DateTimeOffset createdUtc,
        DateTimeOffset expiresUtc)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentException.ThrowIfNullOrWhiteSpace(corpusSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(runConfigurationSha256);
        if (expiresUtc <= createdUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresUtc));
        }

        Validate(corpus, run);

        IReadOnlyDictionary<string, ProviderQueryResult> resultsByQuery = run.Queries
            .ToDictionary(result => result.QueryId, StringComparer.Ordinal);
        EvaluationQuery[] matchingQueries = [.. corpus.Queries.Where(query =>
            query.Kind != EvaluationContractValues.NoMatchQuery)];
        EvaluationQuery[] exactQueries = [.. matchingQueries.Where(query =>
            query.Kind == EvaluationContractValues.ExactTextQuery)];
        EvaluationQuery[] descriptiveQueries = [.. matchingQueries.Where(query =>
            query.Kind == EvaluationContractValues.DescriptiveQuery)];
        EvaluationQuery[] noMatchQueries = [.. corpus.Queries.Where(query =>
            query.Kind == EvaluationContractValues.NoMatchQuery)];
        ProviderFixtureResult[] successfulFixtures = [.. run.Fixtures.Where(result =>
            result.Status == EvaluationContractValues.SucceededStatus)];
        IReadOnlyDictionary<string, EvaluationFixture> fixturesById = corpus.Fixtures
            .ToDictionary(fixture => fixture.CaptureId, StringComparer.Ordinal);

        var metrics = new EvaluationMetrics
        {
            PrecisionAt1 = Average(matchingQueries.Select(query =>
                IsRelevantAt(query, resultsByQuery[query.QueryId], 1) ? 1d : 0d)),
            RecallAt5ExactText = Average(exactQueries.Select(query =>
                IsRelevantAt(query, resultsByQuery[query.QueryId], 5) ? 1d : 0d)),
            RecallAt5Descriptive = Average(descriptiveQueries.Select(query =>
                IsRelevantAt(query, resultsByQuery[query.QueryId], 5) ? 1d : 0d)),
            NormalizedDiscountedCumulativeGainAt5 = Average(matchingQueries.Select(query =>
                CalculateNormalizedDiscountedCumulativeGain(query, resultsByQuery[query.QueryId], 5))),
            NoMatchFalsePositiveRate = Average(noMatchQueries.Select(query =>
                resultsByQuery[query.QueryId].OrderedCaptureIds.Count > 0 ? 1d : 0d)),
            OcrCharacterAccuracy = Average(successfulFixtures.Select(result =>
                CalculateCharacterAccuracy(
                    fixturesById[result.CaptureId].ExpectedOcrText,
                    result.OcrText))),
            BoundedFailureRate = Average(run.Fixtures.Select(result =>
                result.Status == EvaluationContractValues.SucceededStatus ? 0d : 1d)),
            PreparationMeanMilliseconds = Average(run.Fixtures.Select(result =>
                result.PreparationMilliseconds)),
            AnalysisP95Milliseconds = Percentile95(run.Fixtures.Select(result =>
                result.AnalysisMilliseconds)),
            SearchP95Milliseconds = Percentile95(run.Queries.Select(result =>
                result.LatencyMilliseconds)),
            PeakWorkingSetBytes = run.Fixtures.Max(result => result.PeakWorkingSetBytes),
            TotalCpuMilliseconds = run.Fixtures.Sum(result => result.CpuMilliseconds),
            TotalGpuMilliseconds = SumOptional(run.Fixtures.Select(result => result.GpuMilliseconds)),
            TotalNpuMilliseconds = SumOptional(run.Fixtures.Select(result => result.NpuMilliseconds)),
            ProviderOutputBytes = run.Fixtures.Sum(result => result.OutputBytes),
            ProtectedStorageBytes = run.ProtectedStorageBytes,
        };

        bool x64Smoke = HasPassingSmoke(run, "x64");
        bool arm64Smoke = HasPassingSmoke(run, "arm64");
        List<EvaluationGate> gates =
        [
            MinimumGate("precision-at-1", ">= 0.80", metrics.PrecisionAt1, 0.80),
            MinimumGate("recall-at-5-exact-text", ">= 0.95", metrics.RecallAt5ExactText, 0.95),
            MinimumGate("recall-at-5-descriptive", ">= 0.75", metrics.RecallAt5Descriptive, 0.75),
            MinimumGate("ndcg-at-5", ">= 0.80", metrics.NormalizedDiscountedCumulativeGainAt5, 0.80),
            MaximumGate("no-match-false-positive-rate", "<= 0.05", metrics.NoMatchFalsePositiveRate, 0.05),
            MaximumGate("bounded-failure-rate", "<= 0.05", metrics.BoundedFailureRate, 0.05),
            new EvaluationGate
            {
                GateId = "warm-search-p95-1000-items",
                Requirement = "< 150 ms with at least 1000 items in a warm run",
                Observed = metrics.SearchP95Milliseconds,
                Passed = run.RunMode == EvaluationContractValues.WarmRunMode &&
                    run.SearchIndexItemCount >= 1_000 &&
                    metrics.SearchP95Milliseconds < 150d,
            },
            new EvaluationGate
            {
                GateId = "protected-storage-1000-items",
                Requirement = "< 50 MiB with at least 1000 items, excluding model packages",
                Observed = run.ProtectedStorageBytes,
                Passed = run.SearchIndexItemCount >= 1_000 &&
                    run.ProtectedStorageBytes < FiftyMegabytes,
            },
            BooleanGate("packaged-native-aot-x64", "x64 packaged Native AOT smoke passes", x64Smoke),
            BooleanGate("packaged-native-aot-arm64", "ARM64 packaged Native AOT smoke passes", arm64Smoke),
        ];

        return new EvaluationReport
        {
            SchemaVersion = EvaluationContractValues.SchemaVersion,
            Namespace = EvaluationRunStore.NamespaceName,
            RunId = run.RunId,
            CorpusId = corpus.CorpusId,
            CorpusVersion = corpus.CorpusVersion,
            QuerySetVersion = corpus.QuerySetVersion,
            CorpusSha256 = corpusSha256,
            RunConfigurationSha256 = runConfigurationSha256,
            ProviderId = run.ProviderId,
            ModelId = run.ModelId,
            ModelVersion = run.ModelVersion,
            AdapterId = run.AdapterId,
            AdapterVersion = run.AdapterVersion,
            ConfigurationFingerprint = run.ConfigurationFingerprint,
            ProcessingBoundary = run.ProcessingBoundary,
            DeviceClass = run.DeviceClass,
            RunMode = run.RunMode,
            Analyzers = [.. run.Analyzers.Select(CloneAnalyzerIdentity)],
            CreatedUtc = createdUtc,
            ExpiresUtc = expiresUtc,
            Metrics = metrics,
            Gates = gates,
            Passed = gates.All(gate => gate.Passed),
        };
    }

    private static void Validate(EvaluationCorpus corpus, ProviderEvaluationRun run)
    {
        if (corpus.Fixtures is null ||
            corpus.Queries is null ||
            corpus.Fixtures.Any(fixture => fixture is null) ||
            corpus.Queries.Any(query => query is null) ||
            run.Analyzers is null ||
            run.Fixtures is null ||
            run.Queries is null ||
            run.PackagedAotSmoke is null ||
            run.Analyzers.Any(analyzer => analyzer is null) ||
            run.Fixtures.Any(fixture => fixture is null) ||
            run.Queries.Any(query => query is null) ||
            run.PackagedAotSmoke.Any(smoke => smoke is null))
        {
            throw new InvalidDataException("Evaluation document collections cannot be null or contain null entries.");
        }

        if (corpus.SchemaVersion != EvaluationContractValues.SchemaVersion ||
            run.SchemaVersion != EvaluationContractValues.SchemaVersion)
        {
            throw new InvalidDataException("Only evaluation schema version 1 is supported.");
        }

        RequireText(corpus.CorpusId, nameof(corpus.CorpusId));
        RequireText(corpus.CorpusVersion, nameof(corpus.CorpusVersion));
        RequireText(corpus.QuerySetVersion, nameof(corpus.QuerySetVersion));
        RequireText(run.RunId, nameof(run.RunId));
        RequireSafeSegment(run.RunId, nameof(run.RunId));
        RequireText(run.ProviderId, nameof(run.ProviderId));
        RequireText(run.ModelId, nameof(run.ModelId));
        RequireText(run.ModelVersion, nameof(run.ModelVersion));
        RequireText(run.AdapterId, nameof(run.AdapterId));
        RequireText(run.AdapterVersion, nameof(run.AdapterVersion));
        RequireText(run.ConfigurationFingerprint, nameof(run.ConfigurationFingerprint));
        RequireText(run.DeviceClass, nameof(run.DeviceClass));

        if (run.CorpusId != corpus.CorpusId ||
            run.CorpusVersion != corpus.CorpusVersion ||
            run.QuerySetVersion != corpus.QuerySetVersion)
        {
            throw new InvalidDataException("The run does not target the supplied corpus and query versions.");
        }

        if (run.ProcessingBoundary != EvaluationContractValues.OnDeviceBoundary)
        {
            throw new InvalidDataException(
                "Remote evaluation is not authorized by this local-only release gate. It requires a separate architecture and privacy review.");
        }

        if (run.RunMode is not EvaluationContractValues.WarmRunMode and
            not EvaluationContractValues.ColdRunMode)
        {
            throw new InvalidDataException("Run mode must be 'cold' or 'warm'.");
        }

        if (run.SearchIndexItemCount <= 0 || run.ProtectedStorageBytes < 0)
        {
            throw new InvalidDataException("Search item count and protected storage measurements must be valid.");
        }

        ValidateCorpus(corpus);
        ValidateRun(corpus, run);
    }

    private static void ValidateCorpus(EvaluationCorpus corpus)
    {
        if (corpus.Fixtures.Count == 0 || corpus.Queries.Count == 0)
        {
            throw new InvalidDataException("The evaluation corpus requires fixtures and queries.");
        }

        EnsureUnique(corpus.Fixtures.Select(fixture => fixture.CaptureId), "fixture capture id");
        EnsureUnique(corpus.Queries.Select(query => query.QueryId), "query id");
        HashSet<string> fixtureIds = corpus.Fixtures
            .Select(fixture => fixture.CaptureId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (EvaluationFixture fixture in corpus.Fixtures)
        {
            RequireText(fixture.CaptureId, nameof(fixture.CaptureId));
            RequireText(fixture.ExpectedDescription, nameof(fixture.ExpectedDescription));
            if (!fixture.IsSyntheticOrSeparatelyApproved)
            {
                throw new InvalidDataException(
                    $"Fixture '{fixture.CaptureId}' is neither synthetic nor separately approved.");
            }
        }

        foreach (EvaluationQuery query in corpus.Queries)
        {
            if (query.Relevance is null || query.Relevance.Any(item => item is null))
            {
                throw new InvalidDataException("Query relevance cannot be null or contain null entries.");
            }

            RequireText(query.QueryId, nameof(query.QueryId));
            RequireText(query.Text, nameof(query.Text));
            if (query.Kind is not EvaluationContractValues.ExactTextQuery and
                not EvaluationContractValues.DescriptiveQuery and
                not EvaluationContractValues.NoMatchQuery)
            {
                throw new InvalidDataException($"Query '{query.QueryId}' has an unsupported kind.");
            }

            EnsureUnique(query.Relevance.Select(item => item.CaptureId), "relevance capture id");
            if (query.Kind == EvaluationContractValues.NoMatchQuery && query.Relevance.Count != 0)
            {
                throw new InvalidDataException("No-match queries cannot declare relevant captures.");
            }

            if (query.Kind != EvaluationContractValues.NoMatchQuery && query.Relevance.Count == 0)
            {
                throw new InvalidDataException("Matching queries require at least one relevant capture.");
            }

            if (query.Relevance.Any(item => !fixtureIds.Contains(item.CaptureId) || item.Gain <= 0))
            {
                throw new InvalidDataException("Query relevance must identify fixtures with positive gains.");
            }
        }

        if (!corpus.Queries.Any(query => query.Kind == EvaluationContractValues.ExactTextQuery) ||
            !corpus.Queries.Any(query => query.Kind == EvaluationContractValues.DescriptiveQuery) ||
            !corpus.Queries.Any(query => query.Kind == EvaluationContractValues.NoMatchQuery))
        {
            throw new InvalidDataException(
                "The query set must include exact-text, descriptive, and no-match cases.");
        }
    }

    private static void ValidateRun(EvaluationCorpus corpus, ProviderEvaluationRun run)
    {
        if (run.Analyzers.Count == 0)
        {
            throw new InvalidDataException("The run must record at least one evaluated analyzer identity.");
        }

        EnsureUnique(run.Analyzers.Select(analyzer => analyzer.AnalyzerId), "evaluated analyzer id");
        EnsureUnique(run.Analyzers.Select(analyzer => analyzer.Capability), "evaluated capability");
        foreach (EvaluatedAnalyzerIdentity analyzer in run.Analyzers)
        {
            RequireText(analyzer.Capability, nameof(analyzer.Capability));
            RequireText(analyzer.AnalyzerId, nameof(analyzer.AnalyzerId));
            RequireText(analyzer.ProviderId, nameof(analyzer.ProviderId));
            RequireText(analyzer.AdapterVersion, nameof(analyzer.AdapterVersion));
            if (analyzer.ProviderId != run.ProviderId ||
                analyzer.ProcessingBoundary != EvaluationContractValues.OnDeviceBoundary)
            {
                throw new InvalidDataException(
                    "Every evaluated analyzer must belong to the run provider and remain on-device.");
            }
        }

        EnsureUnique(run.Fixtures.Select(result => result.CaptureId), "run fixture capture id");
        EnsureUnique(run.Queries.Select(result => result.QueryId), "run query id");
        EnsureUnique(run.PackagedAotSmoke.Select(result => result.Architecture.ToLowerInvariant()),
            "AOT smoke architecture");

        HashSet<string> expectedFixtureIds = corpus.Fixtures
            .Select(fixture => fixture.CaptureId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> actualFixtureIds = run.Fixtures
            .Select(result => result.CaptureId)
            .ToHashSet(StringComparer.Ordinal);
        if (!expectedFixtureIds.SetEquals(actualFixtureIds))
        {
            throw new InvalidDataException("The run must contain exactly one result for every fixture.");
        }

        HashSet<string> expectedQueryIds = corpus.Queries
            .Select(query => query.QueryId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> actualQueryIds = run.Queries
            .Select(result => result.QueryId)
            .ToHashSet(StringComparer.Ordinal);
        if (!expectedQueryIds.SetEquals(actualQueryIds))
        {
            throw new InvalidDataException("The run must contain exactly one result for every query.");
        }

        foreach (ProviderFixtureResult result in run.Fixtures)
        {
            RequireText(result.Status, nameof(result.Status));
            if (result.PreparationMilliseconds < 0 ||
                result.AnalysisMilliseconds < 0 ||
                result.PeakWorkingSetBytes < 0 ||
                result.CpuMilliseconds < 0 ||
                result.GpuMilliseconds < 0 ||
                result.NpuMilliseconds < 0 ||
                result.OutputBytes < 0)
            {
                throw new InvalidDataException("Provider resource measurements cannot be negative.");
            }
        }

        foreach (ProviderQueryResult result in run.Queries)
        {
            if (result.OrderedCaptureIds is null ||
                result.OrderedCaptureIds.Any(string.IsNullOrWhiteSpace) ||
                result.LatencyMilliseconds < 0 ||
                result.OrderedCaptureIds.Any(captureId => !expectedFixtureIds.Contains(captureId)) ||
                result.OrderedCaptureIds.Distinct(StringComparer.Ordinal).Count() !=
                    result.OrderedCaptureIds.Count)
            {
                throw new InvalidDataException("Query results contain invalid measurements or capture ids.");
            }
        }
    }

    private static bool IsRelevantAt(
        EvaluationQuery query,
        ProviderQueryResult result,
        int maximumRank)
    {
        HashSet<string> relevant = query.Relevance
            .Select(item => item.CaptureId)
            .ToHashSet(StringComparer.Ordinal);
        return result.OrderedCaptureIds.Take(maximumRank).Any(relevant.Contains);
    }

    private static double CalculateNormalizedDiscountedCumulativeGain(
        EvaluationQuery query,
        ProviderQueryResult result,
        int maximumRank)
    {
        IReadOnlyDictionary<string, int> gains = query.Relevance
            .ToDictionary(item => item.CaptureId, item => item.Gain, StringComparer.Ordinal);
        double actual = result.OrderedCaptureIds
            .Take(maximumRank)
            .Select((captureId, index) => DiscountedGain(gains.GetValueOrDefault(captureId), index))
            .Sum();
        double ideal = query.Relevance
            .OrderByDescending(item => item.Gain)
            .Take(maximumRank)
            .Select((item, index) => DiscountedGain(item.Gain, index))
            .Sum();
        return ideal == 0d ? 0d : actual / ideal;
    }

    private static double DiscountedGain(int gain, int zeroBasedRank)
    {
        return (Math.Pow(2d, gain) - 1d) / Math.Log2(zeroBasedRank + 2d);
    }

    private static double CalculateCharacterAccuracy(string expected, string actual)
    {
        if (expected.Length == 0)
        {
            return actual.Length == 0 ? 1d : 0d;
        }

        int[] previous = Enumerable.Range(0, actual.Length + 1).ToArray();
        int[] current = new int[actual.Length + 1];
        for (int expectedIndex = 1; expectedIndex <= expected.Length; expectedIndex++)
        {
            current[0] = expectedIndex;
            for (int actualIndex = 1; actualIndex <= actual.Length; actualIndex++)
            {
                int substitution = previous[actualIndex - 1] +
                    (expected[expectedIndex - 1] == actual[actualIndex - 1] ? 0 : 1);
                current[actualIndex] = Math.Min(
                    Math.Min(previous[actualIndex] + 1, current[actualIndex - 1] + 1),
                    substitution);
            }

            (previous, current) = (current, previous);
        }

        return Math.Max(0d, 1d - ((double)previous[actual.Length] /
            Math.Max(expected.Length, actual.Length)));
    }

    private static double Average(IEnumerable<double> values)
    {
        double[] copiedValues = [.. values];
        return copiedValues.Length == 0 ? 0d : copiedValues.Average();
    }

    private static double Percentile95(IEnumerable<double> values)
    {
        double[] ordered = [.. values.Order()];
        if (ordered.Length == 0)
        {
            return 0d;
        }

        int index = (int)Math.Ceiling(ordered.Length * 0.95d) - 1;
        return ordered[index];
    }

    private static double? SumOptional(IEnumerable<double?> values)
    {
        double?[] copiedValues = [.. values];
        return copiedValues.Any(value => value.HasValue)
            ? copiedValues.Sum(value => value ?? 0d)
            : null;
    }

    private static bool HasPassingSmoke(ProviderEvaluationRun run, string architecture)
    {
        return run.PackagedAotSmoke.Any(result =>
            string.Equals(result.Architecture, architecture, StringComparison.OrdinalIgnoreCase) &&
            result.Passed);
    }

    private static EvaluatedAnalyzerIdentity CloneAnalyzerIdentity(EvaluatedAnalyzerIdentity analyzer)
    {
        return new EvaluatedAnalyzerIdentity
        {
            Capability = analyzer.Capability,
            AnalyzerId = analyzer.AnalyzerId,
            ProviderId = analyzer.ProviderId,
            ModelId = analyzer.ModelId,
            ModelVersion = analyzer.ModelVersion,
            AdapterVersion = analyzer.AdapterVersion,
            RuntimeId = analyzer.RuntimeId,
            RuntimeVersion = analyzer.RuntimeVersion,
            PackageVersion = analyzer.PackageVersion,
            ConfigurationFingerprint = analyzer.ConfigurationFingerprint,
            ProcessingBoundary = analyzer.ProcessingBoundary,
        };
    }

    private static EvaluationGate MinimumGate(
        string id,
        string requirement,
        double observed,
        double minimum)
    {
        return new EvaluationGate
        {
            GateId = id,
            Requirement = requirement,
            Observed = observed,
            Passed = observed >= minimum,
        };
    }

    private static EvaluationGate MaximumGate(
        string id,
        string requirement,
        double observed,
        double maximum)
    {
        return new EvaluationGate
        {
            GateId = id,
            Requirement = requirement,
            Observed = observed,
            Passed = observed <= maximum,
        };
    }

    private static EvaluationGate BooleanGate(string id, string requirement, bool passed)
    {
        return new EvaluationGate
        {
            GateId = id,
            Requirement = requirement,
            Observed = passed ? 1d : 0d,
            Passed = passed,
        };
    }

    private static void EnsureUnique(IEnumerable<string> values, string name)
    {
        string[] copiedValues = [.. values];
        if (copiedValues.Any(string.IsNullOrWhiteSpace) ||
            copiedValues.Distinct(StringComparer.Ordinal).Count() != copiedValues.Length)
        {
            throw new InvalidDataException($"Every {name} must be nonblank and unique.");
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{name} cannot be blank.");
        }
    }

    private static void RequireSafeSegment(string value, string name)
    {
        if (value is "." or ".." || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidDataException($"{name} must be a safe file-name segment.");
        }
    }
}
