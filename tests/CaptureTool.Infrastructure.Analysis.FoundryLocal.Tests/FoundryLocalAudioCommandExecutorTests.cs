using System.Text.Json;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal.Tests;

[TestClass]
public sealed class FoundryLocalAudioCommandExecutorTests
{
    [TestMethod]
    public void CreateRequestJson_MatchesFoundryLocalOneXContract()
    {
        const string modelId = "whisper-tiny-cpu";
        const string audioPath = @"C:\capture audio\sample.wav";

        string json = FoundryLocalAudioCommandExecutor.CreateRequestJson(modelId, audioPath);

        using JsonDocument command = JsonDocument.Parse(json);
        string requestJson = command.RootElement
            .GetProperty("Params")
            .GetProperty("OpenAICreateRequest")
            .GetString()!;
        using JsonDocument request = JsonDocument.Parse(requestJson);
        Assert.AreEqual(modelId, request.RootElement.GetProperty("Model").GetString());
        Assert.AreEqual(audioPath, request.RootElement.GetProperty("FileName").GetString());
        Assert.AreEqual(2, request.RootElement.EnumerateObject().Count());
    }

    [TestMethod]
    [DataRow("text")]
    [DataRow("Text")]
    public void ReadTranscript_AcceptsNativeResponseCasing(string propertyName)
    {
        string json = $$"""{"{{propertyName}}":"Capture memory can hear this."}""";

        string result = FoundryLocalAudioCommandExecutor.ReadTranscript(json);

        Assert.AreEqual("Capture memory can hear this.", result);
    }

    [TestMethod]
    public void ReadTranscript_RejectsMissingText()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            FoundryLocalAudioCommandExecutor.ReadTranscript("{}"));
    }

    [TestMethod]
    public void ReadTranscript_RejectsBlankResponse()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            FoundryLocalAudioCommandExecutor.ReadTranscript(" "));
    }

    [TestMethod]
    public void ReadTranscription_MapsOptionalTimedSegmentsAndLanguage()
    {
        const string json = """
            {
              "Text": "One two.",
              "Language": "en",
              "Segments": [
                { "Text": "One", "Start": 1.25, "End": 2.5 },
                { "text": "two", "start": 2.5, "end": 4.75 }
              ]
            }
            """;

        FoundryLocalAudioTranscription result =
            FoundryLocalAudioCommandExecutor.ReadTranscription(json);

        Assert.AreEqual("One two.", result.Text);
        Assert.AreEqual("en", result.Language);
        Assert.HasCount(2, result.Segments);
        Assert.AreEqual(TimeSpan.FromSeconds(1.25), result.Segments[0].StartTime);
        Assert.AreEqual(TimeSpan.FromSeconds(4.75), result.Segments[1].EndTime);
    }

    [TestMethod]
    public void ReadTranscription_IgnoresMalformedOptionalSegments()
    {
        const string json = """
            {
              "text": "Keep the transcript.",
              "segments": [
                { "text": "backwards", "start": 2, "end": 1 },
                { "text": "missing end", "start": 2 },
                { "text": "valid", "start": 3, "end": 4 }
              ]
            }
            """;

        FoundryLocalAudioTranscription result =
            FoundryLocalAudioCommandExecutor.ReadTranscription(json);

        Assert.HasCount(1, result.Segments);
        Assert.AreEqual("valid", result.Segments[0].Text);
    }

    [TestMethod]
    public void FindModelId_SelectsRequestedAliasAndDevice()
    {
        const string catalog = """
            [
              { "id": "gpu-id", "alias": "whisper-tiny", "runtime": { "deviceType": "GPU" } },
              { "id": "cpu-id", "alias": "whisper-tiny", "runtime": { "deviceType": "CPU" } },
              { "id": "other-id", "alias": "other", "runtime": { "deviceType": "CPU" } }
            ]
            """;

        string? result = FoundryLocalAudioCommandExecutor.FindModelId(
            catalog,
            "whisper-tiny",
            "cpu");

        Assert.AreEqual("cpu-id", result);
    }

    [TestMethod]
    public void FindModelId_ReturnsNullWhenVariantIsUnavailable()
    {
        const string catalog =
            """[{ "id": "gpu-id", "alias": "whisper-tiny", "runtime": { "deviceType": "GPU" } }]""";

        string? result = FoundryLocalAudioCommandExecutor.FindModelId(
            catalog,
            "whisper-tiny",
            "CPU");

        Assert.IsNull(result);
    }
}
