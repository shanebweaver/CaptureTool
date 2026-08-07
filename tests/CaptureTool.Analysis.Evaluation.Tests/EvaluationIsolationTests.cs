using CaptureTool.Analysis.Evaluation;

#pragma warning disable IL2026 // Architecture tests intentionally inspect the untrimmed test assembly.
#pragma warning disable IL2070 // Architecture tests intentionally inspect public DTO properties.

namespace CaptureTool.Analysis.Evaluation.Tests;

[TestClass]
public sealed class EvaluationIsolationTests
{
    [TestMethod]
    public void EvaluationTool_ShouldHaveNoProductionCaptureToolDependencies()
    {
        string[] productionReferences = typeof(EvaluationEngine).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name != null && name.StartsWith("CaptureTool.", StringComparison.Ordinal))
            .Cast<string>()
            .ToArray();

        Assert.IsEmpty(
            productionReferences,
            "Experimental evaluation must not reference production metadata, projection, or provider assemblies.");
    }

    [TestMethod]
    public void EvaluationContracts_ShouldExposeNoProductionPathsOrTelemetryFields()
    {
        Type[] documentTypes =
        [
            typeof(EvaluationCorpus),
            typeof(EvaluationFixture),
            typeof(EvaluationQuery),
            typeof(EvaluationRelevance),
            typeof(ProviderEvaluationRun),
            typeof(EvaluatedAnalyzerIdentity),
            typeof(ProviderFixtureResult),
            typeof(ProviderQueryResult),
            typeof(PackagedAotSmokeResult),
            typeof(EvaluationReport),
            typeof(EvaluationMetrics),
            typeof(EvaluationGate),
        ];
        string[] forbiddenMembers = documentTypes
            .SelectMany(type => type.GetProperties())
            .Select(property => property.Name)
            .Where(name =>
                name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Telemetry", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Prompt", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Training", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.IsEmpty(forbiddenMembers);
    }
}
