namespace CaptureTool.Analysis.Evaluation;

public static class EvaluationContractValues
{
    public const int SchemaVersion = 1;
    public const string OnDeviceBoundary = "on-device";
    public const string WarmRunMode = "warm";
    public const string ColdRunMode = "cold";
    public const string ExactTextQuery = "exact-text";
    public const string DescriptiveQuery = "descriptive";
    public const string NoMatchQuery = "no-match";
    public const string SucceededStatus = "succeeded";
}

public sealed class EvaluationCorpus
{
    public int SchemaVersion { get; set; }

    public string CorpusId { get; set; } = string.Empty;

    public string CorpusVersion { get; set; } = string.Empty;

    public string QuerySetVersion { get; set; } = string.Empty;

    public List<EvaluationFixture> Fixtures { get; set; } = [];

    public List<EvaluationQuery> Queries { get; set; } = [];
}

public sealed class EvaluationFixture
{
    public string CaptureId { get; set; } = string.Empty;

    public bool IsSyntheticOrSeparatelyApproved { get; set; }

    public string ExpectedOcrText { get; set; } = string.Empty;

    public string ExpectedDescription { get; set; } = string.Empty;
}

public sealed class EvaluationQuery
{
    public string QueryId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public List<EvaluationRelevance> Relevance { get; set; } = [];
}

public sealed class EvaluationRelevance
{
    public string CaptureId { get; set; } = string.Empty;

    public int Gain { get; set; }
}

public sealed class ProviderEvaluationRun
{
    public int SchemaVersion { get; set; }

    public string RunId { get; set; } = string.Empty;

    public string CorpusId { get; set; } = string.Empty;

    public string CorpusVersion { get; set; } = string.Empty;

    public string QuerySetVersion { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public string ModelVersion { get; set; } = string.Empty;

    public string AdapterId { get; set; } = string.Empty;

    public string AdapterVersion { get; set; } = string.Empty;

    public string ConfigurationFingerprint { get; set; } = string.Empty;

    public string ProcessingBoundary { get; set; } = string.Empty;

    public string DeviceClass { get; set; } = string.Empty;

    public string RunMode { get; set; } = string.Empty;

    public int SearchIndexItemCount { get; set; }

    public long ProtectedStorageBytes { get; set; }

    public List<EvaluatedAnalyzerIdentity> Analyzers { get; set; } = [];

    public List<ProviderFixtureResult> Fixtures { get; set; } = [];

    public List<ProviderQueryResult> Queries { get; set; } = [];

    public List<PackagedAotSmokeResult> PackagedAotSmoke { get; set; } = [];
}

public sealed class EvaluatedAnalyzerIdentity
{
    public string Capability { get; set; } = string.Empty;

    public string AnalyzerId { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public string? ModelId { get; set; }

    public string? ModelVersion { get; set; }

    public string AdapterVersion { get; set; } = string.Empty;

    public string? RuntimeId { get; set; }

    public string? RuntimeVersion { get; set; }

    public string? PackageVersion { get; set; }

    public string? ConfigurationFingerprint { get; set; }

    public string ProcessingBoundary { get; set; } = string.Empty;
}

public sealed class ProviderFixtureResult
{
    public string CaptureId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string OcrText { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public double PreparationMilliseconds { get; set; }

    public double AnalysisMilliseconds { get; set; }

    public long PeakWorkingSetBytes { get; set; }

    public double CpuMilliseconds { get; set; }

    public double? GpuMilliseconds { get; set; }

    public double? NpuMilliseconds { get; set; }

    public long OutputBytes { get; set; }
}

public sealed class ProviderQueryResult
{
    public string QueryId { get; set; } = string.Empty;

    public double LatencyMilliseconds { get; set; }

    public List<string> OrderedCaptureIds { get; set; } = [];
}

public sealed class PackagedAotSmokeResult
{
    public string Architecture { get; set; } = string.Empty;

    public bool Passed { get; set; }
}

public sealed class EvaluationMetrics
{
    public double PrecisionAt1 { get; set; }

    public double RecallAt5ExactText { get; set; }

    public double RecallAt5Descriptive { get; set; }

    public double NormalizedDiscountedCumulativeGainAt5 { get; set; }

    public double NoMatchFalsePositiveRate { get; set; }

    public double OcrCharacterAccuracy { get; set; }

    public double BoundedFailureRate { get; set; }

    public double PreparationMeanMilliseconds { get; set; }

    public double AnalysisP95Milliseconds { get; set; }

    public double SearchP95Milliseconds { get; set; }

    public long PeakWorkingSetBytes { get; set; }

    public double TotalCpuMilliseconds { get; set; }

    public double? TotalGpuMilliseconds { get; set; }

    public double? TotalNpuMilliseconds { get; set; }

    public long ProviderOutputBytes { get; set; }

    public long ProtectedStorageBytes { get; set; }
}

public sealed class EvaluationGate
{
    public string GateId { get; set; } = string.Empty;

    public string Requirement { get; set; } = string.Empty;

    public double Observed { get; set; }

    public bool Passed { get; set; }
}

public sealed class EvaluationReport
{
    public int SchemaVersion { get; set; }

    public string Namespace { get; set; } = string.Empty;

    public string RunId { get; set; } = string.Empty;

    public string CorpusId { get; set; } = string.Empty;

    public string CorpusVersion { get; set; } = string.Empty;

    public string QuerySetVersion { get; set; } = string.Empty;

    public string CorpusSha256 { get; set; } = string.Empty;

    public string RunConfigurationSha256 { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public string ModelVersion { get; set; } = string.Empty;

    public string AdapterId { get; set; } = string.Empty;

    public string AdapterVersion { get; set; } = string.Empty;

    public string ConfigurationFingerprint { get; set; } = string.Empty;

    public string ProcessingBoundary { get; set; } = string.Empty;

    public string DeviceClass { get; set; } = string.Empty;

    public string RunMode { get; set; } = string.Empty;

    public List<EvaluatedAnalyzerIdentity> Analyzers { get; set; } = [];

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset ExpiresUtc { get; set; }

    public EvaluationMetrics Metrics { get; set; } = new();

    public List<EvaluationGate> Gates { get; set; } = [];

    public bool Passed { get; set; }
}
