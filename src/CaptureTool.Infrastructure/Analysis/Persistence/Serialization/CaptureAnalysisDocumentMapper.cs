using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Analysis.Persistence.Serialization;

internal sealed record CaptureAnalysisEnvelopeReadResult(
    CaptureAnalysisStoreSnapshot Snapshot,
    IReadOnlyList<JsonElement> OpaqueCapabilityEntries);

internal static class CaptureAnalysisDocumentMapper
{
    public static CaptureAnalysisControlDocument ToDocument(
        CaptureAnalysisControlState state,
        long documentRevision,
        int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new()
        {
            SchemaVersion = schemaVersion,
            DocumentRevision = documentRevision,
            Policy = ToDocument(state.Policy),
            CaptureChangeCheckpoint = state.CaptureChangeCheckpoint,
            Enrollments = state.Enrollments.Select(ToDocument).ToList(),
        };
    }

    public static CaptureAnalysisControlSnapshot ToDomain(CaptureAnalysisControlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        CaptureAnalysisPolicy policy = ToDomain(Require(document.Policy, "control policy"));
        var state = new CaptureAnalysisControlState(
            policy,
            Require(document.Enrollments, "control enrollments").Select(ToDomain),
            document.CaptureChangeCheckpoint);
        return new(document.DocumentRevision, state);
    }

    public static CaptureAnalysisEnvelopeDocument ToDocument(
        CaptureAnalysisRecord record,
        long documentRevision,
        int schemaVersion,
        IEnumerable<JsonElement>? opaqueCapabilityEntries = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        List<JsonElement> capabilityEntries = record.Analyses
            .OrderBy(analysis => analysis.Capability.Id.Value, StringComparer.Ordinal)
            .Select(ToJsonElement)
            .ToList();
        capabilityEntries.AddRange((opaqueCapabilityEntries ?? []).Select(element => element.Clone()));

        return new()
        {
            SchemaVersion = schemaVersion,
            DocumentRevision = documentRevision,
            CaptureId = record.CaptureId.ToString(),
            MediaKind = record.MediaKind,
            CapturedAtUtc = record.CapturedAtUtc,
            SourceRevision = ToDocument(record.SourceRevision),
            Recipe = ToDocument(record.Recipe),
            CapabilityEntries = capabilityEntries,
        };
    }

    public static CaptureAnalysisEnvelopeReadResult ToDomain(CaptureAnalysisEnvelopeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        CaptureId captureId = CaptureId.Parse(document.CaptureId);
        SourceRevision sourceRevision = ToDomain(Require(document.SourceRevision, "source revision"));
        CaptureAnalysisRecipe recipe = ToDomain(Require(document.Recipe, "analysis recipe"));
        var analyses = new List<CapabilityAnalysis>();
        var opaqueEntries = new List<JsonElement>();

        foreach (JsonElement element in Require(document.CapabilityEntries, "capability entries"))
        {
            CapabilityDefinition definition = ReadCapabilityDefinition(element);
            if (!IsKnownCapability(definition))
            {
                opaqueEntries.Add(element.Clone());
                continue;
            }

            CaptureAnalysisCapabilityEntryDocument entry = JsonSerializer.Deserialize(
                element,
                CaptureAnalysisJsonContext.Default.CaptureAnalysisCapabilityEntryDocument)
                ?? throw new InvalidDataException("A capability entry cannot be null.");
            CapabilityDefinition entryDefinition = ToDomain(
                Require(entry.Capability, "capability definition"));
            if (entryDefinition != definition)
            {
                throw new InvalidDataException("A capability entry changed identity while being read.");
            }

            CanonicalCapabilityResult? canonicalResult = entry.CanonicalResult == null
                ? null
                : ToDomain(
                    captureId,
                    sourceRevision,
                    definition,
                    entry.CanonicalResult);
            CapabilityOutcome? latestOutcome = entry.LatestOutcome == null
                ? null
                : ToDomain(
                    captureId,
                    sourceRevision,
                    definition,
                    entry.LatestOutcome);
            analyses.Add(new(definition, canonicalResult, latestOutcome));
        }

        var record = new CaptureAnalysisRecord(
            captureId,
            document.MediaKind,
            document.CapturedAtUtc,
            sourceRevision,
            recipe,
            analyses);
        return new(
            new CaptureAnalysisStoreSnapshot(document.DocumentRevision, record),
            opaqueEntries.AsReadOnly());
    }

    private static CaptureAnalysisPolicyDocument ToDocument(CaptureAnalysisPolicy policy)
    {
        return new()
        {
            ConsentState = policy.ConsentState,
            PolicyRevision = policy.PolicyRevision,
            ControlGeneration = policy.ControlGeneration,
            AuthorizationScope = policy.AuthorizationScope == null
                ? null
                : ToDocument(policy.AuthorizationScope),
            IsFutureCaptureAdmissionEnabled = policy.IsFutureCaptureAdmissionEnabled,
            FutureCaptureSequenceWatermark = policy.FutureCaptureSequenceWatermark,
            BackfillState = policy.BackfillState,
            BackfillUpperSequence = policy.BackfillUpperSequence,
            BackfillCheckpoint = policy.BackfillCheckpoint,
        };
    }

    private static CaptureAnalysisPolicy ToDomain(CaptureAnalysisPolicyDocument document)
    {
        return new(
            document.ConsentState,
            document.PolicyRevision,
            document.ControlGeneration,
            document.AuthorizationScope == null ? null : ToDomain(document.AuthorizationScope),
            document.IsFutureCaptureAdmissionEnabled,
            document.FutureCaptureSequenceWatermark,
            document.BackfillState,
            document.BackfillUpperSequence,
            document.BackfillCheckpoint);
    }

    private static CaptureAnalysisAuthorizationScopeDocument ToDocument(
        CaptureAnalysisAuthorizationScope scope)
    {
        return new()
        {
            Purpose = ToDocument(scope.Purpose),
            ProcessingPolicy = ToDocument(scope.ProcessingPolicy),
            Capabilities = scope.Capabilities.Select(ToDocument).ToList(),
        };
    }

    private static CaptureAnalysisAuthorizationScope ToDomain(
        CaptureAnalysisAuthorizationScopeDocument document)
    {
        AnalysisPurpose purpose = ToDomain(Require(document.Purpose, "authorization purpose"));
        AnalysisProcessingPolicy processingPolicy = ToDomain(
            Require(document.ProcessingPolicy, "processing policy"));
        return new(
            purpose,
            processingPolicy,
            Require(document.Capabilities, "authorized capabilities").Select(ToDomain));
    }

    private static AnalysisPurposeDocument ToDocument(AnalysisPurpose purpose)
    {
        return new() { Id = purpose.Id, Version = purpose.Version };
    }

    private static AnalysisPurpose ToDomain(AnalysisPurposeDocument document)
    {
        return new(document.Id, document.Version);
    }

    private static AnalysisProcessingPolicyDocument ToDocument(AnalysisProcessingPolicy policy)
    {
        return new()
        {
            AuthorizedPurpose = ToDocument(policy.AuthorizedPurpose),
            AllowedBoundaries = [.. policy.AllowedBoundaries],
            AllowedRemoteProviderIds = [.. policy.AllowedRemoteProviderIds],
        };
    }

    private static AnalysisProcessingPolicy ToDomain(AnalysisProcessingPolicyDocument document)
    {
        return new(
            ToDomain(Require(document.AuthorizedPurpose, "processing-policy purpose")),
            Require(document.AllowedBoundaries, "processing boundaries"),
            Require(document.AllowedRemoteProviderIds, "remote provider IDs"));
    }

    private static CaptureAnalysisEnrollmentDocument ToDocument(CaptureAnalysisEnrollment enrollment)
    {
        return new()
        {
            CaptureId = enrollment.CaptureId.ToString(),
            State = enrollment.State,
            ExclusionReason = enrollment.ExclusionReason,
            EnrollmentGeneration = enrollment.EnrollmentGeneration,
            TombstoneGeneration = enrollment.TombstoneGeneration,
            AssetFinalizationSequence = enrollment.AssetFinalizationSequence,
            RequestedRecipeId = enrollment.RequestedRecipeId?.Value,
            RequestedRecipeVersion = enrollment.RequestedRecipeVersion?.Value,
        };
    }

    private static CaptureAnalysisEnrollment ToDomain(CaptureAnalysisEnrollmentDocument document)
    {
        return new(
            CaptureId.Parse(document.CaptureId),
            document.State,
            document.ExclusionReason,
            document.EnrollmentGeneration,
            document.TombstoneGeneration,
            document.AssetFinalizationSequence,
            document.RequestedRecipeId == null
                ? null
                : new AnalysisRecipeId(document.RequestedRecipeId),
            document.RequestedRecipeVersion.HasValue
                ? new AnalysisRecipeVersion(document.RequestedRecipeVersion.Value)
                : null);
    }

    private static SourceRevisionDocument ToDocument(SourceRevision revision)
    {
        return new()
        {
            Length = revision.Length,
            LastWriteTimeUtc = revision.LastWriteTimeUtc,
            FingerprintAlgorithm = revision.Fingerprint.Algorithm,
            FingerprintValue = revision.Fingerprint.Value,
        };
    }

    private static SourceRevision ToDomain(SourceRevisionDocument document)
    {
        return new(
            document.Length,
            document.LastWriteTimeUtc,
            new ContentFingerprint(document.FingerprintAlgorithm, document.FingerprintValue));
    }

    private static CaptureAnalysisRecipeDocument ToDocument(CaptureAnalysisRecipe recipe)
    {
        return new()
        {
            Id = recipe.Id.Value,
            Version = recipe.Version.Value,
            MediaKind = recipe.MediaKind,
            Capabilities = recipe.Capabilities.Select(capability => new RecipeCapabilityDocument
            {
                Capability = ToDocument(capability.Capability),
                Requirement = capability.Requirement,
            }).ToList(),
        };
    }

    private static CaptureAnalysisRecipe ToDomain(CaptureAnalysisRecipeDocument document)
    {
        return new(
            new AnalysisRecipeId(document.Id),
            new AnalysisRecipeVersion(document.Version),
            document.MediaKind,
            Require(document.Capabilities, "recipe capabilities").Select(capability =>
                new RecipeCapability(
                    ToDomain(Require(capability.Capability, "recipe capability")),
                    capability.Requirement)));
    }

    private static CapabilityDefinitionDocument ToDocument(CapabilityDefinition definition)
    {
        return new()
        {
            Id = definition.Id.Value,
            SchemaVersion = definition.SchemaVersion.Value,
            Classification = definition.Classification,
        };
    }

    private static CapabilityDefinition ToDomain(CapabilityDefinitionDocument document)
    {
        return new(
            new AnalysisCapabilityId(document.Id),
            new CapabilitySchemaVersion(document.SchemaVersion),
            document.Classification);
    }

    private static JsonElement ToJsonElement(CapabilityAnalysis analysis)
    {
        var document = new CaptureAnalysisCapabilityEntryDocument
        {
            Capability = ToDocument(analysis.Capability),
            CanonicalResult = analysis.CanonicalResult == null
                ? null
                : ToDocument(analysis.CanonicalResult),
            LatestOutcome = analysis.LatestOutcome == null
                ? null
                : ToDocument(analysis.LatestOutcome),
        };
        return JsonSerializer.SerializeToElement(
            document,
            CaptureAnalysisJsonContext.Default.CaptureAnalysisCapabilityEntryDocument);
    }

    private static CanonicalCapabilityResultDocument ToDocument(CanonicalCapabilityResult result)
    {
        return new()
        {
            Analyzer = ToDocument(result.Analyzer),
            ProcessingBoundary = result.ProcessingBoundary,
            GeneratedAtUtc = result.GeneratedAtUtc,
            Payload = ToJsonElement(result.Payload),
        };
    }

    private static CanonicalCapabilityResult ToDomain(
        CaptureId captureId,
        SourceRevision sourceRevision,
        CapabilityDefinition capability,
        CanonicalCapabilityResultDocument document)
    {
        return new(
            captureId,
            sourceRevision,
            ToPayload(capability, document.Payload),
            ToDomain(Require(document.Analyzer, "result analyzer")),
            document.ProcessingBoundary,
            document.GeneratedAtUtc);
    }

    private static CapabilityOutcomeDocument ToDocument(CapabilityOutcome outcome)
    {
        return new()
        {
            Analyzer = ToDocument(outcome.Analyzer),
            ProcessingBoundary = outcome.ProcessingBoundary,
            State = outcome.State,
            Failure = new()
            {
                Code = outcome.Failure.Code,
                Disposition = outcome.Failure.Disposition,
            },
            GeneratedAtUtc = outcome.GeneratedAtUtc,
        };
    }

    private static CapabilityOutcome ToDomain(
        CaptureId captureId,
        SourceRevision sourceRevision,
        CapabilityDefinition capability,
        CapabilityOutcomeDocument document)
    {
        AnalysisFailureDocument failure = Require(document.Failure, "capability failure");
        return new(
            captureId,
            sourceRevision,
            capability,
            ToDomain(Require(document.Analyzer, "outcome analyzer")),
            document.ProcessingBoundary,
            document.State,
            new AnalysisFailure(failure.Code, failure.Disposition),
            document.GeneratedAtUtc);
    }

    private static AnalyzerIdentityDocument ToDocument(AnalyzerIdentity analyzer)
    {
        return new()
        {
            AnalyzerId = analyzer.AnalyzerId,
            ProviderId = analyzer.ProviderId,
            ModelId = analyzer.ModelId,
            ModelVersion = analyzer.ModelVersion,
            AdapterVersion = analyzer.AdapterVersion,
            RuntimeId = analyzer.RuntimeId,
            RuntimeVersion = analyzer.RuntimeVersion,
            PackageVersion = analyzer.PackageVersion,
            ConfigurationFingerprint = analyzer.ConfigurationFingerprint,
        };
    }

    private static AnalyzerIdentity ToDomain(AnalyzerIdentityDocument document)
    {
        return new(
            document.AnalyzerId,
            document.ProviderId,
            document.ModelId,
            document.ModelVersion,
            document.AdapterVersion,
            document.RuntimeId,
            document.RuntimeVersion,
            document.PackageVersion,
            document.ConfigurationFingerprint);
    }

    private static JsonElement ToJsonElement(CapabilityPayload payload)
    {
        return payload switch
        {
            MediaPropertiesV1 mediaProperties => JsonSerializer.SerializeToElement(
                ToDocument(mediaProperties),
                CaptureAnalysisJsonContext.Default.MediaPropertiesPayloadDocument),
            OcrDocumentV1 ocrDocument => JsonSerializer.SerializeToElement(
                ToDocument(ocrDocument),
                CaptureAnalysisJsonContext.Default.OcrDocumentPayloadDocument),
            ImageDescriptionV1 imageDescription => JsonSerializer.SerializeToElement(
                ToDocument(imageDescription),
                CaptureAnalysisJsonContext.Default.ImageDescriptionPayloadDocument),
            _ => throw new InvalidDataException(
                $"Unsupported compiled capability payload '{payload.GetType().FullName}'."),
        };
    }

    private static CapabilityPayload ToPayload(
        CapabilityDefinition capability,
        JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A known capability payload must be a JSON object.");
        }

        if (capability == AnalysisCapabilities.MediaPropertiesV1)
        {
            MediaPropertiesPayloadDocument document = JsonSerializer.Deserialize(
                payload,
                CaptureAnalysisJsonContext.Default.MediaPropertiesPayloadDocument)
                ?? throw new InvalidDataException("Media properties payload cannot be null.");
            return ToDomain(document);
        }

        if (capability == AnalysisCapabilities.OcrDocumentV1)
        {
            OcrDocumentPayloadDocument document = JsonSerializer.Deserialize(
                payload,
                CaptureAnalysisJsonContext.Default.OcrDocumentPayloadDocument)
                ?? throw new InvalidDataException("OCR payload cannot be null.");
            return ToDomain(document);
        }

        if (capability == AnalysisCapabilities.ImageDescriptionV1)
        {
            ImageDescriptionPayloadDocument document = JsonSerializer.Deserialize(
                payload,
                CaptureAnalysisJsonContext.Default.ImageDescriptionPayloadDocument)
                ?? throw new InvalidDataException("Image-description payload cannot be null.");
            return ToDomain(document);
        }

        throw new InvalidDataException($"Capability '{capability.Id}' has no compiled payload reader.");
    }

    private static MediaPropertiesPayloadDocument ToDocument(MediaPropertiesV1 payload)
    {
        return new()
        {
            MediaKind = payload.MediaKind,
            PixelSize = payload.PixelSize.HasValue
                ? ToDocument(payload.PixelSize.Value)
                : null,
            DurationTicks = payload.Duration?.Ticks,
            MimeType = payload.MimeType,
            Container = payload.Container,
            VideoCodec = payload.VideoCodec,
            AudioCodec = payload.AudioCodec,
            AudioChannelCount = payload.AudioChannelCount,
            SampleRateHz = payload.SampleRateHz,
            BitRate = payload.BitRate,
            FrameRate = payload.FrameRate,
        };
    }

    private static MediaPropertiesV1 ToDomain(MediaPropertiesPayloadDocument document)
    {
        return new(
            document.MediaKind,
            document.PixelSize == null ? null : ToDomain(document.PixelSize),
            document.DurationTicks.HasValue ? TimeSpan.FromTicks(document.DurationTicks.Value) : null,
            document.MimeType,
            document.Container,
            document.VideoCodec,
            document.AudioCodec,
            document.AudioChannelCount,
            document.SampleRateHz,
            document.BitRate,
            document.FrameRate);
    }

    private static OcrDocumentPayloadDocument ToDocument(OcrDocumentV1 payload)
    {
        return new()
        {
            RasterSize = ToDocument(payload.RasterSize),
            FullText = payload.FullText,
            Languages = payload.Languages.Select(language => new OcrLanguageCandidateDocument
            {
                LanguageTag = language.LanguageTag,
                Confidence = language.Confidence,
            }).ToList(),
            Regions = payload.Regions.Select(region => new OcrRegionDocument
            {
                Bounds = ToDocument(region.Bounds),
                Confidence = region.Confidence,
                Lines = region.Lines.Select(line => new OcrLineDocument
                {
                    Text = line.Text,
                    Bounds = ToDocument(line.Bounds),
                    Confidence = line.Confidence,
                    Words = line.Words.Select(word => new OcrWordDocument
                    {
                        Text = word.Text,
                        Bounds = ToDocument(word.Bounds),
                        Confidence = word.Confidence,
                    }).ToList(),
                }).ToList(),
            }).ToList(),
        };
    }

    private static OcrDocumentV1 ToDomain(OcrDocumentPayloadDocument document)
    {
        return new(
            ToDomain(Require(document.RasterSize, "OCR raster size")),
            document.FullText,
            Require(document.Languages, "OCR languages").Select(language =>
                new OcrLanguageCandidateV1(language.LanguageTag, language.Confidence)),
            Require(document.Regions, "OCR regions").Select(region =>
                new OcrRegionV1(
                    ToDomain(Require(region.Bounds, "OCR region bounds")),
                    Require(region.Lines, "OCR lines").Select(line =>
                        new OcrLineV1(
                            line.Text,
                            ToDomain(Require(line.Bounds, "OCR line bounds")),
                            Require(line.Words, "OCR words").Select(word =>
                                new OcrWordV1(
                                    word.Text,
                                    ToDomain(Require(word.Bounds, "OCR word bounds")),
                                    word.Confidence)),
                            line.Confidence)),
                    region.Confidence)));
    }

    private static ImageDescriptionPayloadDocument ToDocument(ImageDescriptionV1 payload)
    {
        return new()
        {
            Description = payload.Description,
            Purpose = payload.Purpose,
            Style = payload.Style,
            Confidence = payload.Confidence,
        };
    }

    private static ImageDescriptionV1 ToDomain(ImageDescriptionPayloadDocument document)
    {
        return new(document.Description, document.Purpose, document.Style, document.Confidence);
    }

    private static PixelSizeDocument ToDocument(PixelSize size)
    {
        return new() { Width = size.Width, Height = size.Height };
    }

    private static PixelSize ToDomain(PixelSizeDocument document)
    {
        return new(document.Width, document.Height);
    }

    private static PixelRectDocument ToDocument(PixelRect rectangle)
    {
        return new()
        {
            X = rectangle.X,
            Y = rectangle.Y,
            Width = rectangle.Width,
            Height = rectangle.Height,
        };
    }

    private static PixelRect ToDomain(PixelRectDocument document)
    {
        return new(document.X, document.Y, document.Width, document.Height);
    }

    private static CapabilityDefinition ReadCapabilityDefinition(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("capability", out JsonElement capabilityElement))
        {
            throw new InvalidDataException("A capability entry requires a capability header.");
        }

        CapabilityDefinitionDocument document = JsonSerializer.Deserialize(
            capabilityElement,
            CaptureAnalysisJsonContext.Default.CapabilityDefinitionDocument)
            ?? throw new InvalidDataException("A capability header cannot be null.");
        return ToDomain(document);
    }

    private static bool IsKnownCapability(CapabilityDefinition capability)
    {
        return capability == AnalysisCapabilities.MediaPropertiesV1 ||
            capability == AnalysisCapabilities.OcrDocumentV1 ||
            capability == AnalysisCapabilities.ImageDescriptionV1;
    }

    private static T Require<T>(T? value, string name)
        where T : class
    {
        return value ?? throw new InvalidDataException($"The {name} is missing.");
    }
}
