using CaptureTool.Analysis.Evaluation;

namespace CaptureTool.Analysis.Evaluation.Tests;

[TestClass]
public sealed class EvaluationEngineTests
{
    private static readonly DateTimeOffset CreatedUtc =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Evaluate_ShouldPassReproducibleLocalReleaseGates()
    {
        EvaluationReport report = Evaluate(CreateCorpus(), CreatePassingRun());

        Assert.IsTrue(report.Passed);
        Assert.AreEqual(EvaluationRunStore.NamespaceName, report.Namespace);
        Assert.AreEqual("CORPUS-HASH", report.CorpusSha256);
        Assert.AreEqual("CONFIG-HASH", report.RunConfigurationSha256);
        Assert.AreEqual("provider", report.ProviderId);
        Assert.AreEqual("model-v1", report.ModelId);
        Assert.AreEqual("adapter-v1", report.AdapterId);
        Assert.HasCount(2, report.Analyzers);
        Assert.AreEqual("ocr-document/v1", report.Analyzers[0].Capability);
        Assert.AreEqual(1d, report.Metrics.PrecisionAt1);
        Assert.AreEqual(1d, report.Metrics.RecallAt5ExactText);
        Assert.AreEqual(1d, report.Metrics.RecallAt5Descriptive);
        Assert.AreEqual(1d, report.Metrics.NormalizedDiscountedCumulativeGainAt5);
        Assert.AreEqual(0d, report.Metrics.NoMatchFalsePositiveRate);
        Assert.AreEqual(1d, report.Metrics.OcrCharacterAccuracy);
        Assert.AreEqual(0d, report.Metrics.BoundedFailureRate);
        Assert.AreEqual(12.5d, report.Metrics.PreparationMeanMilliseconds);
        Assert.AreEqual(50d, report.Metrics.AnalysisP95Milliseconds);
        Assert.AreEqual(30d, report.Metrics.SearchP95Milliseconds);
        Assert.AreEqual(200L, report.Metrics.PeakWorkingSetBytes);
        Assert.AreEqual(30d, report.Metrics.TotalCpuMilliseconds);
        Assert.AreEqual(7d, report.Metrics.TotalGpuMilliseconds);
        Assert.AreEqual(11d, report.Metrics.TotalNpuMilliseconds);
        Assert.HasCount(10, report.Gates);
        Assert.IsTrue(report.Gates.All(gate => gate.Passed));
    }

    [TestMethod]
    public void Evaluate_ShouldExposeQualityAndOperationalFailuresWithoutMutatingInputs()
    {
        EvaluationCorpus corpus = CreateCorpus();
        ProviderEvaluationRun run = CreatePassingRun();
        run.RunMode = EvaluationContractValues.ColdRunMode;
        run.ProtectedStorageBytes = 60L * 1024L * 1024L;
        run.Fixtures[0].OcrText = "wrong";
        run.Fixtures[1].Status = "transient-failure";
        run.Queries.Single(result => result.QueryId == "exact").OrderedCaptureIds = ["image-b"];
        run.Queries.Single(result => result.QueryId == "none").OrderedCaptureIds = ["image-a"];
        run.PackagedAotSmoke.Single(result => result.Architecture == "arm64").Passed = false;

        EvaluationReport report = Evaluate(corpus, run);

        Assert.IsFalse(report.Passed);
        Assert.IsLessThan(1d, report.Metrics.OcrCharacterAccuracy);
        Assert.AreEqual(0.5d, report.Metrics.BoundedFailureRate);
        Assert.AreEqual(0d, report.Metrics.RecallAt5ExactText);
        Assert.AreEqual(1d, report.Metrics.NoMatchFalsePositiveRate);
        Assert.IsFalse(report.Gates.Single(gate =>
            gate.GateId == "warm-search-p95-1000-items").Passed);
        Assert.IsFalse(report.Gates.Single(gate =>
            gate.GateId == "protected-storage-1000-items").Passed);
        Assert.IsFalse(report.Gates.Single(gate =>
            gate.GateId == "packaged-native-aot-arm64").Passed);
        Assert.AreEqual("wrong", run.Fixtures[0].OcrText);
    }

    [TestMethod]
    public void Evaluate_ShouldRejectRemoteRunsUnderLocalOnlyPolicy()
    {
        ProviderEvaluationRun run = CreatePassingRun();
        run.ProcessingBoundary = "remote";

        InvalidDataException exception = Assert.ThrowsExactly<InvalidDataException>(() =>
            Evaluate(CreateCorpus(), run));

        StringAssert.Contains(exception.Message, "separate architecture and privacy review");
    }

    [TestMethod]
    public void Evaluate_ShouldRejectUnapprovedFixtureData()
    {
        EvaluationCorpus corpus = CreateCorpus();
        corpus.Fixtures[0].IsSyntheticOrSeparatelyApproved = false;

        Assert.ThrowsExactly<InvalidDataException>(() =>
            Evaluate(corpus, CreatePassingRun()));
    }

    [TestMethod]
    public void Evaluate_ShouldRejectMismatchedOrIncompleteVersionedRuns()
    {
        ProviderEvaluationRun wrongVersion = CreatePassingRun();
        wrongVersion.QuerySetVersion = "different";
        Assert.ThrowsExactly<InvalidDataException>(() =>
            Evaluate(CreateCorpus(), wrongVersion));

        ProviderEvaluationRun incomplete = CreatePassingRun();
        incomplete.Queries.RemoveAt(0);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            Evaluate(CreateCorpus(), incomplete));
    }

    [TestMethod]
    public void Evaluate_ShouldRejectInvalidMeasurementsAndIdentifiers()
    {
        ProviderEvaluationRun negative = CreatePassingRun();
        negative.Fixtures[0].CpuMilliseconds = -1;
        Assert.ThrowsExactly<InvalidDataException>(() =>
            Evaluate(CreateCorpus(), negative));

        ProviderEvaluationRun unsafeRun = CreatePassingRun();
        unsafeRun.RunId = "../escape";
        Assert.ThrowsExactly<InvalidDataException>(() =>
            Evaluate(CreateCorpus(), unsafeRun));

        ProviderEvaluationRun duplicateResult = CreatePassingRun();
        duplicateResult.Queries[0].OrderedCaptureIds = ["image-a", "image-a"];
        Assert.ThrowsExactly<InvalidDataException>(() =>
            Evaluate(CreateCorpus(), duplicateResult));
    }

    [TestMethod]
    public void Evaluate_ShouldTrackUnsupportedAcceleratorCountersAsUnavailable()
    {
        ProviderEvaluationRun run = CreatePassingRun();
        foreach (ProviderFixtureResult fixture in run.Fixtures)
        {
            fixture.GpuMilliseconds = null;
            fixture.NpuMilliseconds = null;
        }

        EvaluationReport report = Evaluate(CreateCorpus(), run);

        Assert.IsNull(report.Metrics.TotalGpuMilliseconds);
        Assert.IsNull(report.Metrics.TotalNpuMilliseconds);
    }

    [TestMethod]
    public void Evaluate_ShouldRejectNullCollectionsFromMalformedJson()
    {
        EvaluationCorpus corpus = CreateCorpus();
        corpus.Queries[0].Relevance = null!;

        Assert.ThrowsExactly<InvalidDataException>(() =>
            Evaluate(corpus, CreatePassingRun()));

        ProviderEvaluationRun run = CreatePassingRun();
        run.Queries[0].OrderedCaptureIds = null!;
        Assert.ThrowsExactly<InvalidDataException>(() =>
            Evaluate(CreateCorpus(), run));
    }

    internal static EvaluationCorpus CreateCorpus()
    {
        return new EvaluationCorpus
        {
            SchemaVersion = 1,
            CorpusId = "corpus",
            CorpusVersion = "1.0.0",
            QuerySetVersion = "1.0.0",
            Fixtures =
            [
                new EvaluationFixture
                {
                    CaptureId = "image-a",
                    IsSyntheticOrSeparatelyApproved = true,
                    ExpectedOcrText = "hello world",
                    ExpectedDescription = "a mountain lake",
                },
                new EvaluationFixture
                {
                    CaptureId = "image-b",
                    IsSyntheticOrSeparatelyApproved = true,
                    ExpectedOcrText = string.Empty,
                    ExpectedDescription = "a red bicycle",
                },
            ],
            Queries =
            [
                new EvaluationQuery
                {
                    QueryId = "exact",
                    Kind = EvaluationContractValues.ExactTextQuery,
                    Text = "hello world",
                    Relevance = [new EvaluationRelevance { CaptureId = "image-a", Gain = 3 }],
                },
                new EvaluationQuery
                {
                    QueryId = "description",
                    Kind = EvaluationContractValues.DescriptiveQuery,
                    Text = "red bike",
                    Relevance = [new EvaluationRelevance { CaptureId = "image-b", Gain = 3 }],
                },
                new EvaluationQuery
                {
                    QueryId = "none",
                    Kind = EvaluationContractValues.NoMatchQuery,
                    Text = "spaceship",
                },
            ],
        };
    }

    internal static ProviderEvaluationRun CreatePassingRun()
    {
        return new ProviderEvaluationRun
        {
            SchemaVersion = 1,
            RunId = "run-v1",
            CorpusId = "corpus",
            CorpusVersion = "1.0.0",
            QuerySetVersion = "1.0.0",
            ProviderId = "provider",
            ModelId = "model-v1",
            ModelVersion = "1.2.3",
            AdapterId = "adapter-v1",
            AdapterVersion = "2.0.0",
            ConfigurationFingerprint = "sha256:config",
            ProcessingBoundary = EvaluationContractValues.OnDeviceBoundary,
            DeviceClass = "test-device",
            RunMode = EvaluationContractValues.WarmRunMode,
            SearchIndexItemCount = 1_000,
            ProtectedStorageBytes = 40L * 1024L * 1024L,
            Analyzers =
            [
                new EvaluatedAnalyzerIdentity
                {
                    Capability = "ocr-document/v1",
                    AnalyzerId = "ocr-adapter",
                    ProviderId = "provider",
                    ModelId = "ocr-model",
                    ModelVersion = "1",
                    AdapterVersion = "2.0.0",
                    RuntimeId = "runtime",
                    RuntimeVersion = "3",
                    PackageVersion = "4",
                    ConfigurationFingerprint = "sha256:ocr",
                    ProcessingBoundary = EvaluationContractValues.OnDeviceBoundary,
                },
                new EvaluatedAnalyzerIdentity
                {
                    Capability = "image-description/v1",
                    AnalyzerId = "description-adapter",
                    ProviderId = "provider",
                    ModelId = "description-model",
                    ModelVersion = "1",
                    AdapterVersion = "2.0.0",
                    RuntimeId = "runtime",
                    RuntimeVersion = "3",
                    PackageVersion = "4",
                    ConfigurationFingerprint = "sha256:description",
                    ProcessingBoundary = EvaluationContractValues.OnDeviceBoundary,
                },
            ],
            Fixtures =
            [
                new ProviderFixtureResult
                {
                    CaptureId = "image-a",
                    Status = EvaluationContractValues.SucceededStatus,
                    OcrText = "hello world",
                    Description = "a mountain lake",
                    PreparationMilliseconds = 25,
                    AnalysisMilliseconds = 40,
                    PeakWorkingSetBytes = 100,
                    CpuMilliseconds = 10,
                    GpuMilliseconds = 3,
                    NpuMilliseconds = 5,
                    OutputBytes = 1000,
                },
                new ProviderFixtureResult
                {
                    CaptureId = "image-b",
                    Status = EvaluationContractValues.SucceededStatus,
                    OcrText = string.Empty,
                    Description = "a red bicycle",
                    PreparationMilliseconds = 0,
                    AnalysisMilliseconds = 50,
                    PeakWorkingSetBytes = 200,
                    CpuMilliseconds = 20,
                    GpuMilliseconds = 4,
                    NpuMilliseconds = 6,
                    OutputBytes = 2000,
                },
            ],
            Queries =
            [
                new ProviderQueryResult
                {
                    QueryId = "exact",
                    LatencyMilliseconds = 20,
                    OrderedCaptureIds = ["image-a", "image-b"],
                },
                new ProviderQueryResult
                {
                    QueryId = "description",
                    LatencyMilliseconds = 30,
                    OrderedCaptureIds = ["image-b", "image-a"],
                },
                new ProviderQueryResult
                {
                    QueryId = "none",
                    LatencyMilliseconds = 10,
                },
            ],
            PackagedAotSmoke =
            [
                new PackagedAotSmokeResult { Architecture = "x64", Passed = true },
                new PackagedAotSmokeResult { Architecture = "arm64", Passed = true },
            ],
        };
    }

    internal static EvaluationReport Evaluate(
        EvaluationCorpus corpus,
        ProviderEvaluationRun run)
    {
        return EvaluationEngine.Evaluate(
            corpus,
            run,
            "CORPUS-HASH",
            "CONFIG-HASH",
            CreatedUtc,
            CreatedUtc.AddDays(30));
    }
}
