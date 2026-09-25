using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Windows.DependencyInjection;
using CaptureTool.Infrastructure.Analysis.Windows.Foundry;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CaptureTool.Infrastructure.Windows.Tests.Analysis;

[TestClass]
public sealed class FoundryVisionTests
{
    private const string Model = "resolved-cpu-model:3";
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void RequestContainsInlineMediaAndDisablesResponseStorage()
    {
        using var request = JsonDocument.Parse(FoundryVisionProtocol.CreateRequest(Model, [1, 2, 3]));
        JsonElement root = request.RootElement;
        Assert.AreEqual(Model, root.GetProperty("model").GetString());
        Assert.IsFalse(root.GetProperty("store").GetBoolean());
        Assert.IsFalse(root.GetProperty("stream").GetBoolean());
        Assert.IsLessThanOrEqualTo(512, root.GetProperty("max_output_tokens").GetInt32());
        JsonElement image = root.GetProperty("input")[0].GetProperty("content")[1];
        Assert.AreEqual("image/jpeg", image.GetProperty("media_type").GetString());
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, image.GetProperty("image_data").GetBytesFromBase64());
        Assert.IsFalse(image.TryGetProperty("image_url", out _));
        Assert.ThrowsExactly<InvalidDataException>(() => FoundryVisionProtocol.CreateRequest(Model, []));
        Assert.ThrowsExactly<InvalidDataException>(() => FoundryVisionProtocol.CreateRequest(Model, new byte[FoundryVisionProtocol.MaximumImageBytes + 1]));
    }

    [TestMethod]
    public void OnlyCompletedAssistantTextBecomesDescriptionMetadata()
    {
        JsonObject response = Response();
        response["output"]!.AsArray().Insert(0, new JsonObject { ["type"] = "reasoning", ["text"] = "private reasoning" });
        response["output"]!.AsArray().Add((JsonNode)new JsonObject { ["type"] = "function_call", ["arguments"] = "tool output" });
        var result = Parse(response);
        Assert.AreEqual(AnalyzerOutcomeKind.Succeeded, result.Status);
        Assert.AreEqual("A red square.", result.Text);
    }

    [TestMethod]
    [DataRow("model", "another-model")]
    [DataRow("status", "incomplete")]
    [DataRow("error", "failure")]
    public void MismatchedFailedOrIncompleteResponsesAreNotSuccessful(string property, string value)
    {
        JsonObject response = Response();
        response[property] = value;
        Assert.AreEqual((AnalyzerOutcomeKind.Failed, (string?)null), Parse(response));
    }

    [TestMethod]
    [DataRow("role", "user")]
    [DataRow("status", "in_progress")]
    public void InvalidAssistantMessagesAreNotCanonical(string property, string value)
    {
        JsonObject response = Response();
        response["output"]![0]![property] = value;
        Assert.AreEqual((AnalyzerOutcomeKind.Failed, (string?)null), Parse(response));
    }

    [TestMethod]
    public void RefusalAndEmptyOrOversizedTextCannotBecomeSuccessfulDescriptions()
    {
        JsonObject response = Response();
        response["output"]![0]!["content"]![0]!["type"] = "refusal";
        Assert.AreEqual((AnalyzerOutcomeKind.ContentRejected, (string?)null), Parse(response));
        response = Response();
        response["output"]![0]!["content"]![0]!["text"] = "   ";
        Assert.AreEqual((AnalyzerOutcomeKind.Failed, (string?)null), Parse(response));
        response["output"]![0]!["content"]![0]!["text"] = new string('a', FoundryVisionProtocol.MaximumDescriptionCharacters + 1);
        Assert.AreEqual((AnalyzerOutcomeKind.Failed, (string?)null), Parse(response));
        Assert.ThrowsExactly<InvalidDataException>(() => FoundryVisionProtocol.ParseResponse(new byte[FoundryVisionProtocol.MaximumResponseBytes + 1], Model));
        Assert.Throws<JsonException>(() => FoundryVisionProtocol.ParseResponse("invalid"u8.ToArray(), Model));
    }

    [TestMethod]
    public async Task CompositionAndPassiveReadinessDoNotTouchStorageOrInitializeModels()
    {
        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        using var provider = new ServiceCollection().AddSingleton(storage.Object).AddSingleton(Mock.Of<IScratchArtifactStore>())
            .AddWindowsAnalysisProviders().BuildServiceProvider();
        IMediaAnalyzer vision = provider.GetServices<IMediaAnalyzer>().Single(analyzer => analyzer.Descriptor.Id == "foundry-local-image-description");
        Assert.AreEqual(AnalysisCapability.Description, vision.Descriptor.Capability);
        Assert.AreEqual(AnalyzerAvailability.PreparationRequired,
            await vision.GetAvailabilityAsync(AnalysisMediaKind.Image, "en", TestContext.CancellationToken));
        Assert.AreEqual(AnalyzerAvailability.PreparationRequired,
            await vision.GetAvailabilityAsync(AnalysisMediaKind.Video, "en", TestContext.CancellationToken));
        Assert.AreEqual(AnalyzerAvailability.Unsupported,
            await vision.GetAvailabilityAsync(AnalysisMediaKind.Audio, "en", TestContext.CancellationToken));
        storage.VerifyNoOtherCalls();
    }

    private static JsonObject Response() => JsonNode.Parse("""
        {"model":"resolved-cpu-model:3","status":"completed","error":null,"output":[
          {"type":"message","role":"assistant","status":"completed","content":[
            {"type":"output_text","text":" A red square. "}]}]}
        """)!.AsObject();
    private static (AnalyzerOutcomeKind Status, string? Text) Parse(JsonObject response) =>
        FoundryVisionProtocol.ParseResponse(Encoding.UTF8.GetBytes(response.ToJsonString()), Model);
}
