using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows;
using CaptureTool.Infrastructure.Analysis.Windows.DependencyInjection;
using CaptureTool.Infrastructure.Analysis.Windows.Foundry;
using CaptureTool.Application.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using Moq;

namespace CaptureTool.Infrastructure.Windows.Tests.Analysis;

[TestClass]
public sealed class WindowsLanguageModelTests
{
    public TestContext TestContext { get; set; } = null!;
    private const string Valid = """{"altText":"An invoice for USD 125.00.","evidence":0}""";

    [TestMethod]
    public void DefaultCompositionPrefersWindowsWithPhi4FallbackForEverySemanticAction()
    {
        using var services = new ServiceCollection().AddSingleton(Mock.Of<IStorageService>()).AddSingleton(Mock.Of<IScratchArtifactStore>())
            .AddWindowsAnalysisProviders(MetadataEnrichmentConfiguration.SemanticModels).BuildServiceProvider();
        var processors = services.GetServices<IMetadataProcessor>().ToArray();
        Assert.HasCount(6, processors);
        var byId = processors.ToDictionary(processor => processor.Descriptor.Id);
        foreach (var step in MetadataEnrichmentConfiguration.Steps.Skip(1).Append(MetadataEnrichmentConfiguration.AltTextStep))
        {
            Assert.HasCount(2, step.Candidates);
            Assert.StartsWith("windows-language-model-", step.Candidates[0]);
            Assert.StartsWith("foundry-phi4-mini-", step.Candidates[1]);
            Assert.IsInstanceOfType<WindowsLanguageModelProcessor>(byId[step.Candidates[0]]);
            Assert.IsInstanceOfType<FoundryMetadataProcessor>(byId[step.Candidates[1]]);
            Assert.IsTrue(step.Candidates.All(id => byId[id].Descriptor.Capability == step.Capability));
        }
    }

    [TestMethod]
    [DataRow(AIFeatureReadyState.Ready, AnalyzerAvailability.Ready)]
    [DataRow(AIFeatureReadyState.NotReady, AnalyzerAvailability.PreparationRequired)]
    [DataRow(AIFeatureReadyState.NotSupportedOnCurrentSystem, AnalyzerAvailability.Unsupported)]
    [DataRow(AIFeatureReadyState.DisabledByUser, AnalyzerAvailability.Unsupported)]
    [DataRow(AIFeatureReadyState.CapabilityMissing, AnalyzerAvailability.Unsupported)]
    [DataRow(AIFeatureReadyState.NotCompatibleWithSystemHardware, AnalyzerAvailability.Unsupported)]
    [DataRow(AIFeatureReadyState.OSUpdateNeeded, AnalyzerAvailability.Unsupported)]
    public void ReadinessHonorsHardwareAndUserDisable(AIFeatureReadyState state, AnalyzerAvailability expected)
    {
        Assert.AreEqual(expected, WindowsLanguageModelClient.MapReady(state));
        Assert.AreEqual(expected, WindowsImageAnalyzer.MapReady(state));
    }

    [TestMethod]
    public async Task PassiveReadinessNeverPreparesOrCreatesAModel()
    {
        var client = new Client { Availability = AnalyzerAvailability.PreparationRequired };
        var processor = new WindowsLanguageModelProcessor(Input().Descriptor, client);
        Assert.AreEqual(AnalyzerAvailability.PreparationRequired, await processor.GetAvailabilityAsync(TestContext.CancellationToken));
        Assert.AreEqual(0, client.Preparations);
        Assert.IsEmpty(client.Sessions);
    }

    [TestMethod]
    public async Task RepeatedActionsUseFreshSessionsAndPreserveExactEvidence()
    {
        var client = new Client();
        var input = Input();
        var processor = new WindowsLanguageModelProcessor(input.Descriptor, client);
        for (int index = 0; index < 3; index++)
        {
            var result = await processor.ProcessAsync(input, TestContext.CancellationToken);
            Assert.AreEqual(AnalyzerOutcomeKind.Succeeded, result.Kind);
            Assert.AreEqual("microsoft-windows", result.Producer!.ProviderId);
            Assert.IsNull(result.Producer.ModelVersion, "Windows does not expose a resolved model version; do not invent one.");
            Assert.AreEqual(input.Entries[0].ResultId, ((ImageAltTextMetadata)result.Payload!).Suggestion!.Evidence[0].ResultId);
        }
        Assert.HasCount(3, client.Sessions);
        Assert.IsTrue(client.Sessions.All(session => session.Disposed && session.Calls == 1));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task MalformedOutputGetsAtMostOneCorrectionOnTheSameSession(bool recover)
    {
        var client = new Client { Generate = calls => Task.FromResult(new WindowsLanguageModelResponse(LanguageModelResponseStatus.Complete,
            calls == 2 && recover ? Valid : Valid.Replace("\"evidence\":0", "\"evidence\":99"))) };
        var result = await new WindowsLanguageModelProcessor(Input().Descriptor, client).ProcessAsync(Input(), TestContext.CancellationToken);
        Assert.AreEqual(recover ? AnalyzerOutcomeKind.Succeeded : AnalyzerOutcomeKind.Failed, result.Kind);
        Assert.HasCount(1, client.Sessions);
        Assert.AreEqual(2, client.Sessions[0].Calls);
        Assert.IsTrue(client.Sessions[0].Disposed);
    }

    [TestMethod]
    public async Task CancellationRetainsOwnershipUntilNativeCompletionAndPreventsCorrection()
    {
        using var cancelled = new CancellationTokenSource();
        var completion = new TaskCompletionSource<WindowsLanguageModelResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { Generate = _ => completion.Task };
        var task = new WindowsLanguageModelProcessor(Input().Descriptor, client).ProcessAsync(Input(), cancelled.Token);
        cancelled.Cancel();
        Assert.IsFalse(task.IsCompleted);
        Assert.IsFalse(client.Sessions[0].Disposed);
        completion.SetResult(new(LanguageModelResponseStatus.Complete, "invalid"));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => task);
        Assert.AreEqual(1, client.Sessions[0].Calls);
        Assert.IsTrue(client.Sessions[0].Disposed);
    }

    [TestMethod]
    [DataRow(LanguageModelResponseStatus.BlockedByPolicy, AnalyzerOutcomeKind.ContentRejected)]
    [DataRow(LanguageModelResponseStatus.ResponseBlockedByContentModeration, AnalyzerOutcomeKind.ContentRejected)]
    [DataRow(LanguageModelResponseStatus.PromptLargerThanContext, AnalyzerOutcomeKind.Unsupported)]
    [DataRow(LanguageModelResponseStatus.UnsupportedLanguage, AnalyzerOutcomeKind.Unsupported)]
    [DataRow(LanguageModelResponseStatus.Error, AnalyzerOutcomeKind.Failed)]
    public async Task NativeFailuresAreNotRetriedOrPublished(LanguageModelResponseStatus status, AnalyzerOutcomeKind kind)
    {
        var client = new Client { Generate = _ => Task.FromResult(new WindowsLanguageModelResponse(status, Valid)) };
        var result = await new WindowsLanguageModelProcessor(Input().Descriptor, client).ProcessAsync(Input(), TestContext.CancellationToken);
        Assert.AreEqual(kind, result.Kind);
        Assert.IsNull(result.Payload);
        Assert.AreEqual(1, client.Sessions[0].Calls);
    }

    [TestMethod]
    public async Task UnsupportedOrDeniedAccessCannotCreateAModel()
    {
        var client = new Client { Availability = AnalyzerAvailability.Unsupported };
        var result = await new WindowsLanguageModelProcessor(Input().Descriptor, client).ProcessAsync(Input(), TestContext.CancellationToken);
        Assert.AreEqual(AnalyzerOutcomeKind.Unsupported, result.Kind);
        Assert.IsEmpty(client.Sessions);
        client.Availability = AnalyzerAvailability.Ready;
        client.CreateError = new UnauthorizedAccessException();
        result = await new WindowsLanguageModelProcessor(Input().Descriptor, client).ProcessAsync(Input(), TestContext.CancellationToken);
        Assert.AreEqual("windows-language-model-access-denied", result.FailureCode);
    }

    private static MetadataProcessorInput Input()
    {
        var source = new AnalysisResult(new TextRecognitionMetadata([new("Invoice total USD 125.00.")]), new("ocr", "test", "model", "1"), DateTimeOffset.UtcNow, "test");
        var record = new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Image, new(new string('a', 64)), "test", Guid.NewGuid(), [source]);
        return MetadataProcessorInput.Create(record, MetadataEnrichmentConfiguration.CreateSemantic("windows-test", AnalysisCapability.ImageAltText));
    }

    private sealed class Client : IWindowsLanguageModelClient
    {
        public AnalyzerAvailability Availability = AnalyzerAvailability.Ready;
        public int Preparations;
        public List<Session> Sessions = [];
        public Exception? CreateError;
        public Func<int, Task<WindowsLanguageModelResponse>> Generate = _ => Task.FromResult(new WindowsLanguageModelResponse(LanguageModelResponseStatus.Complete, Valid));
        public AnalyzerAvailability GetAvailability() => Availability;
        public Task<AnalyzerAvailability> PrepareAsync(CancellationToken ct) { Preparations++; return Task.FromResult(Availability); }
        public Task<IWindowsLanguageModelSession> CreateSessionAsync(CancellationToken ct)
        {
            if (CreateError != null) throw CreateError;
            var session = new Session(Generate); Sessions.Add(session);
            return Task.FromResult<IWindowsLanguageModelSession>(session);
        }
    }
    private sealed class Session(Func<int, Task<WindowsLanguageModelResponse>> generate) : IWindowsLanguageModelSession
    {
        public bool Disposed;
        public int Calls;
        public Task<WindowsLanguageModelResponse> GenerateAsync(string instructions, string sources)
        {
            Calls++;
            Assert.AreEqual(Calls == 2, instructions.Contains("previous attempt failed", StringComparison.Ordinal));
            Assert.DoesNotContain("Invoice total USD 125.00.", instructions);
            Assert.DoesNotContain("/no_think", instructions);
            Assert.Contains("Invoice total USD 125.00.", sources);
            return generate(Calls);
        }
        public void Dispose() => Disposed = true;
    }
}
