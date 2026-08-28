#if DEBUG
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CaptureTool.Presentation.Windows.WinUI.Debugging;

internal static class CaptureAnalysisInspectorJsonSerializer
{
    public static string Serialize(CaptureAnalysisRecord record, CaptureAsset? asset)
    {
        ArgumentNullException.ThrowIfNull(record);

        string? sourcePath = asset?.PreferredOpenPath ?? asset?.RetainedSourcePath;
        var document = new JsonObject
        {
            ["exportSchemaVersion"] = 1,
            ["exportedAtUtc"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ["capture"] = new JsonObject
            {
                ["captureId"] = record.CaptureId.ToString(),
                ["fileName"] = sourcePath == null ? null : Path.GetFileName(sourcePath),
                ["sourcePath"] = sourcePath,
                ["mediaKind"] = FormatEnum(record.MediaKind),
                ["capturedAtUtc"] = record.CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                ["lifecycleState"] = asset == null ? null : FormatEnum(asset.LifecycleState),
                ["lifecycleRevision"] = asset?.LifecycleRevision,
            },
            ["sourceRevision"] = new JsonObject
            {
                ["length"] = record.SourceRevision.Length,
                ["lastWriteTimeUtc"] = record.SourceRevision.LastWriteTimeUtc.ToString(
                    "O",
                    CultureInfo.InvariantCulture),
                ["fingerprint"] = new JsonObject
                {
                    ["algorithm"] = record.SourceRevision.Fingerprint.Algorithm,
                    ["value"] = record.SourceRevision.Fingerprint.Value,
                },
            },
            ["recipe"] = CreateRecipe(record.Recipe),
            ["isUsable"] = record.IsUsable,
            ["analyses"] = new JsonArray(record.Analyses
                .OrderBy(analysis => analysis.Capability.Id.Value, StringComparer.Ordinal)
                .Select(CreateAnalysis)
                .ToArray()),
        };

        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonObject CreateRecipe(CaptureAnalysisRecipe recipe)
    {
        return new JsonObject
        {
            ["id"] = recipe.Id.Value,
            ["version"] = recipe.Version.Value,
            ["mediaKind"] = FormatEnum(recipe.MediaKind),
            ["capabilities"] = new JsonArray(recipe.Capabilities
                .Select(requested => new JsonObject
                {
                    ["capability"] = CreateCapability(requested.Capability),
                    ["requirement"] = FormatEnum(requested.Requirement),
                    ["dependencies"] = new JsonArray(requested.Dependencies
                        .Select(dependency => JsonValue.Create(dependency.Id.Value))
                        .ToArray()),
                })
                .ToArray()),
        };
    }

    private static JsonObject CreateAnalysis(CapabilityAnalysis analysis)
    {
        return new JsonObject
        {
            ["capability"] = CreateCapability(analysis.Capability),
            ["canonicalResult"] = analysis.CanonicalResult == null
                ? null
                : CreateResult(analysis.CanonicalResult),
            ["latestOutcome"] = analysis.LatestOutcome == null
                ? null
                : CreateOutcome(analysis.LatestOutcome),
        };
    }

    private static JsonObject CreateCapability(CapabilityDefinition capability)
    {
        return new JsonObject
        {
            ["id"] = capability.Id.Value,
            ["schemaVersion"] = capability.SchemaVersion.Value,
            ["classification"] = FormatEnum(capability.Classification),
        };
    }

    private static JsonObject CreateResult(CanonicalCapabilityResult result)
    {
        return new JsonObject
        {
            ["resultId"] = result.ResultId.Value.ToString("D"),
            ["analyzer"] = CreateAnalyzer(result.Analyzer),
            ["processingBoundary"] = FormatEnum(result.ProcessingBoundary),
            ["generatedAtUtc"] = result.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            ["inputs"] = new JsonArray(result.Inputs.Select(input => new JsonObject
            {
                ["resultId"] = input.ResultId.Value.ToString("D"),
                ["capability"] = CreateCapability(input.Capability),
                ["analyzerRevision"] = input.AnalyzerRevision.Value,
                ["generatedAtUtc"] = input.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            }).ToArray()),
            ["payload"] = CreatePayload(result.Payload),
        };
    }

    private static JsonObject CreateOutcome(CapabilityOutcome outcome)
    {
        return new JsonObject
        {
            ["state"] = FormatEnum(outcome.State),
            ["failure"] = new JsonObject
            {
                ["code"] = FormatEnum(outcome.Failure.Code),
                ["disposition"] = FormatEnum(outcome.Failure.Disposition),
            },
            ["analyzer"] = CreateAnalyzer(outcome.Analyzer),
            ["processingBoundary"] = FormatEnum(outcome.ProcessingBoundary),
            ["generatedAtUtc"] = outcome.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture),
        };
    }

    private static JsonObject CreateAnalyzer(AnalyzerIdentity analyzer)
    {
        return new JsonObject
        {
            ["analyzerId"] = analyzer.AnalyzerId,
            ["providerId"] = analyzer.ProviderId,
            ["modelId"] = analyzer.ModelId,
            ["modelVersion"] = analyzer.ModelVersion,
            ["adapterVersion"] = analyzer.AdapterVersion,
            ["runtimeId"] = analyzer.RuntimeId,
            ["runtimeVersion"] = analyzer.RuntimeVersion,
            ["packageVersion"] = analyzer.PackageVersion,
            ["configurationFingerprint"] = analyzer.ConfigurationFingerprint,
            ["analyzerRevision"] = analyzer.Revision.Value,
        };
    }

    private static JsonObject CreatePayload(CapabilityPayload payload) => payload switch
    {
        MediaPropertiesV1 properties => CreateMediaProperties(properties),
        OcrDocumentV1 ocr => CreateOcrDocument(ocr),
        ImageDescriptionV1 description => new JsonObject
        {
            ["type"] = "image-description-v1",
            ["description"] = description.Description,
            ["purpose"] = FormatEnum(description.Purpose),
            ["style"] = description.Style,
            ["confidence"] = description.Confidence,
        },
        SpeechTranscriptV1 transcript => new JsonObject
        {
            ["type"] = "speech-transcript-v1",
            ["fullText"] = transcript.FullText,
            ["languageTag"] = transcript.LanguageTag,
            ["segments"] = new JsonArray(transcript.Segments.Select(segment => new JsonObject
            {
                ["text"] = segment.Text,
                ["start"] = FormatTime(segment.StartTime),
                ["end"] = FormatTime(segment.EndTime),
                ["speakerLabel"] = segment.SpeakerLabel,
                ["confidence"] = segment.Confidence,
            }).ToArray()),
        },
        VideoOcrTrackV1 videoOcr => new JsonObject
        {
            ["type"] = "video-ocr-track-v1",
            ["fullText"] = videoOcr.FullText,
            ["observations"] = new JsonArray(videoOcr.Observations.Select(observation =>
                new JsonObject
                {
                    ["text"] = observation.Text,
                    ["start"] = FormatTime(observation.StartTime),
                    ["end"] = FormatTime(observation.EndTime),
                }).ToArray()),
        },
        VideoDescriptionTrackV1 videoDescription => new JsonObject
        {
            ["type"] = "video-description-track-v1",
            ["fullText"] = videoDescription.FullText,
            ["observations"] = new JsonArray(videoDescription.Observations.Select(observation =>
                new JsonObject
                {
                    ["description"] = observation.Description,
                    ["start"] = FormatTime(observation.StartTime),
                    ["end"] = FormatTime(observation.EndTime),
                }).ToArray()),
        },
        _ => new JsonObject
        {
            ["type"] = payload.GetType().FullName,
            ["note"] = "This build does not have a readable inspector projection for this payload type.",
        },
    };

    private static JsonObject CreateMediaProperties(MediaPropertiesV1 properties)
    {
        return new JsonObject
        {
            ["type"] = "media-properties-v1",
            ["mediaKind"] = FormatEnum(properties.MediaKind),
            ["pixelSize"] = properties.PixelSize is not { } size
                ? null
                : new JsonObject
                {
                    ["width"] = size.Width,
                    ["height"] = size.Height,
                },
            ["duration"] = FormatTime(properties.Duration),
            ["mimeType"] = properties.MimeType,
            ["container"] = properties.Container,
            ["videoCodec"] = properties.VideoCodec,
            ["audioCodec"] = properties.AudioCodec,
            ["audioChannelCount"] = properties.AudioChannelCount,
            ["sampleRateHz"] = properties.SampleRateHz,
            ["bitRate"] = properties.BitRate,
            ["frameRate"] = properties.FrameRate,
        };
    }

    private static JsonObject CreateOcrDocument(OcrDocumentV1 ocr)
    {
        return new JsonObject
        {
            ["type"] = "ocr-document-v1",
            ["rasterSize"] = new JsonObject
            {
                ["width"] = ocr.RasterSize.Width,
                ["height"] = ocr.RasterSize.Height,
            },
            ["fullText"] = ocr.FullText,
            ["languages"] = new JsonArray(ocr.Languages.Select(language => new JsonObject
            {
                ["languageTag"] = language.LanguageTag,
                ["confidence"] = language.Confidence,
            }).ToArray()),
            ["regions"] = new JsonArray(ocr.Regions.Select(region => new JsonObject
            {
                ["bounds"] = CreateBounds(region.Bounds),
                ["confidence"] = region.Confidence,
                ["lines"] = new JsonArray(region.Lines.Select(line => new JsonObject
                {
                    ["text"] = line.Text,
                    ["bounds"] = CreateBounds(line.Bounds),
                    ["confidence"] = line.Confidence,
                    ["words"] = new JsonArray(line.Words.Select(word => new JsonObject
                    {
                        ["text"] = word.Text,
                        ["bounds"] = CreateBounds(word.Bounds),
                        ["confidence"] = word.Confidence,
                    }).ToArray()),
                }).ToArray()),
            }).ToArray()),
        };
    }

    private static JsonObject CreateBounds(PixelRect bounds)
    {
        return new JsonObject
        {
            ["x"] = bounds.X,
            ["y"] = bounds.Y,
            ["width"] = bounds.Width,
            ["height"] = bounds.Height,
        };
    }

    private static string? FormatTime(TimeSpan? value) => value?.ToString("c", CultureInfo.InvariantCulture);

    private static string FormatEnum<T>(T value) where T : struct, Enum
    {
        string text = value.ToString();
        return text.Length == 0
            ? text
            : char.ToLowerInvariant(text[0]) + text[1..];
    }
}
#endif
