using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Text;

namespace CaptureTool.Infrastructure.Analysis.Windows.Analyzers;

public sealed class WindowsImageDescriptionAnalyzer : IPreparableCaptureAnalyzer
{
    public const string AdapterVersion = "1.0.0";

    private readonly IImageDescriptionAnalysisService _imageDescription;

    public WindowsImageDescriptionAnalyzer(IImageDescriptionAnalysisService? imageDescription = null)
    {
        _imageDescription = imageDescription ?? UnavailableImageDescriptionAnalysisService.Instance;

        ImageDescriptionModelDescriptor model = _imageDescription.ModelDescriptor;
        var identity = new AnalyzerIdentity(
            analyzerId: "windows-image-description",
            providerId: model.ProducerId,
            modelId: model.ModelId,
            modelVersion: model.ModelVersion,
            adapterVersion: AdapterVersion,
            runtimeId: model.RuntimeId,
            runtimeVersion: model.RuntimeVersion,
            packageVersion: model.PackageVersion,
            configurationFingerprint: null);
        Descriptor = new CaptureAnalyzerDescriptor(
            AnalysisCapabilities.ImageDescriptionV1,
            identity,
            [CaptureMediaKind.Image],
            ProcessingBoundary.OnDevice,
            CaptureAnalyzerDataKind.None,
            CaptureAnalyzerRequirement.OperatingSystemCapability |
                CaptureAnalyzerRequirement.ModelPackage |
                CaptureAnalyzerRequirement.UserInitiatedPreparation,
            CaptureAnalyzerWorkloadClass.AiIntensive,
            maximumSourceBytes: null,
            qualityTier: 100);
    }

    public CaptureAnalyzerDescriptor Descriptor { get; }

    public ValueTask<CaptureAnalyzerAvailability> GetAvailabilityAsync(
        CaptureAnalyzerAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!request.IsEligibleFor(Descriptor))
        {
            throw new ArgumentException("The availability request targets another analyzer.", nameof(request));
        }

        try
        {
            CaptureAnalyzerAvailability availability = _imageDescription.GetReadyState() switch
            {
                ImageDescriptionReadyState.Ready => CaptureAnalyzerAvailability.Available,
                ImageDescriptionReadyState.PreparationNeeded =>
                    CaptureAnalyzerAvailability.PreparationRequired,
                ImageDescriptionReadyState.NotSupported => CaptureAnalyzerAvailability.Unsupported(
                    new AnalysisFailure(
                        AnalysisFailureCode.CapabilityUnavailable,
                        AnalysisFailureDisposition.Terminal)),
                ImageDescriptionReadyState.Disabled => CaptureAnalyzerAvailability.Disabled,
                _ => CaptureAnalyzerAvailability.TemporarilyUnavailable(
                    new AnalysisFailure(
                        AnalysisFailureCode.ProviderUnavailable,
                        AnalysisFailureDisposition.Transient)),
            };
            return ValueTask.FromResult(availability);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return ValueTask.FromResult(CaptureAnalyzerAvailability.TemporarilyUnavailable(
                new AnalysisFailure(
                    AnalysisFailureCode.ProviderUnavailable,
                    AnalysisFailureDisposition.Transient)));
        }
    }

    public async Task<CaptureAnalyzerPreparationResult> PrepareAsync(
        IProgress<AnalysisCapabilityPreparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IProgress<double>? providerProgress = progress == null
                ? null
                : new InlineProgress<double>(fraction =>
                    progress.Report(new AnalysisCapabilityPreparationProgress(fraction)));
            ImageDescriptionAnalysisPreparationResult result = await _imageDescription
                .PrepareAnalysisAsync(providerProgress, cancellationToken)
                .ConfigureAwait(false);
            return result.Status switch
            {
                ImageDescriptionAnalysisPreparationStatus.Succeeded =>
                    CaptureAnalyzerPreparationResult.Succeeded,
                ImageDescriptionAnalysisPreparationStatus.Unsupported =>
                    CaptureAnalyzerPreparationResult.Unsupported(new AnalysisFailure(
                        AnalysisFailureCode.CapabilityUnavailable,
                        AnalysisFailureDisposition.Terminal)),
                ImageDescriptionAnalysisPreparationStatus.Disabled =>
                    CaptureAnalyzerPreparationResult.Disabled,
                ImageDescriptionAnalysisPreparationStatus.Cancelled =>
                    CaptureAnalyzerPreparationResult.Cancelled,
                ImageDescriptionAnalysisPreparationStatus.TransientFailure =>
                    CaptureAnalyzerPreparationResult.Failed(new AnalysisFailure(
                        AnalysisFailureCode.ProviderUnavailable,
                        AnalysisFailureDisposition.Transient)),
                ImageDescriptionAnalysisPreparationStatus.TerminalFailure =>
                    CaptureAnalyzerPreparationResult.Failed(new AnalysisFailure(
                        AnalysisFailureCode.InternalError,
                        AnalysisFailureDisposition.Terminal)),
                _ => CaptureAnalyzerPreparationResult.Failed(new AnalysisFailure(
                    AnalysisFailureCode.InvalidResponse,
                    AnalysisFailureDisposition.Terminal)),
            };
        }
        catch (OperationCanceledException)
        {
            return CaptureAnalyzerPreparationResult.Cancelled;
        }
        catch
        {
            return CaptureAnalyzerPreparationResult.Failed(new AnalysisFailure(
                AnalysisFailureCode.ProviderUnavailable,
                AnalysisFailureDisposition.Transient));
        }
    }

    public async Task<CaptureAnalyzerOutput> AnalyzeAsync(
        CaptureAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IsEligibleFor(Descriptor))
        {
            throw new ArgumentException("The analysis request targets another analyzer.", nameof(request));
        }

        try
        {
            await using Stream source = await request.Source.OpenReadAsync(cancellationToken)
                .ConfigureAwait(false);
            ImageDescriptionAnalysisResult result = await _imageDescription
                .DescribeAnalysisAsync(source, cancellationToken)
                .ConfigureAwait(false);
            return result.Status switch
            {
                ImageDescriptionAnalysisStatus.Succeeded => CreateSuccessfulOutput(result.Description),
                ImageDescriptionAnalysisStatus.PreparationRequired => CaptureAnalyzerOutput.Failed(
                    new AnalysisFailure(
                        AnalysisFailureCode.ModelNotReady,
                        AnalysisFailureDisposition.Transient)),
                ImageDescriptionAnalysisStatus.Unsupported or
                ImageDescriptionAnalysisStatus.Disabled => CaptureAnalyzerOutput.Unsupported(
                    new AnalysisFailure(
                        AnalysisFailureCode.CapabilityUnavailable,
                        AnalysisFailureDisposition.Terminal)),
                ImageDescriptionAnalysisStatus.BlockedByPolicy => CaptureAnalyzerOutput.Failed(
                    new AnalysisFailure(
                        AnalysisFailureCode.AuthorizationDenied,
                        AnalysisFailureDisposition.Terminal)),
                ImageDescriptionAnalysisStatus.BlockedByContentSafety => CaptureAnalyzerOutput.Failed(
                    new AnalysisFailure(
                        AnalysisFailureCode.InvalidResponse,
                        AnalysisFailureDisposition.Terminal)),
                ImageDescriptionAnalysisStatus.InputTooLarge => CaptureAnalyzerOutput.Failed(
                    new AnalysisFailure(
                        AnalysisFailureCode.InputTooLarge,
                        AnalysisFailureDisposition.Terminal)),
                ImageDescriptionAnalysisStatus.Cancelled => CaptureAnalyzerOutput.Cancelled,
                ImageDescriptionAnalysisStatus.TransientFailure => CaptureAnalyzerOutput.Failed(
                    new AnalysisFailure(
                        AnalysisFailureCode.ProviderUnavailable,
                        AnalysisFailureDisposition.Transient)),
                ImageDescriptionAnalysisStatus.TerminalFailure => CaptureAnalyzerOutput.Failed(
                    new AnalysisFailure(
                        AnalysisFailureCode.InvalidResponse,
                        AnalysisFailureDisposition.Terminal)),
                _ => CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                    AnalysisFailureCode.InvalidResponse,
                    AnalysisFailureDisposition.Terminal)),
            };
        }
        catch (OperationCanceledException)
        {
            return CaptureAnalyzerOutput.Cancelled;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException)
        {
            return CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                AnalysisFailureCode.InvalidSource,
                AnalysisFailureDisposition.Terminal));
        }
        catch
        {
            return CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                AnalysisFailureCode.ProviderUnavailable,
                AnalysisFailureDisposition.Transient));
        }
    }

    private static CaptureAnalyzerOutput CreateSuccessfulOutput(string description)
    {
        try
        {
            string normalized = description
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Trim()
                .Normalize(NormalizationForm.FormC);
            return CaptureAnalyzerOutput.Succeeded(new ImageDescriptionV1(
                normalized,
                ImageDescriptionPurpose.Brief));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                AnalysisFailureCode.InvalidResponse,
                AnalysisFailureDisposition.Terminal));
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value)
        {
            report(value);
        }
    }

    private sealed class UnavailableImageDescriptionAnalysisService : IImageDescriptionAnalysisService
    {
        public static UnavailableImageDescriptionAnalysisService Instance { get; } = new();

        public ImageDescriptionModelDescriptor ModelDescriptor { get; } = new(
            "microsoft-windows",
            "windows-app-sdk-image-description",
            ModelVersion: null,
            "windows-app-sdk-ai",
            RuntimeVersion: null,
            PackageVersion: null);

        public ImageDescriptionReadyState GetReadyState()
        {
            return ImageDescriptionReadyState.NotSupported;
        }

        public Task<ImageDescriptionAnalysisPreparationResult> PrepareAnalysisAsync(
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ImageDescriptionAnalysisPreparationResult.Unsupported);
        }

        public Task<ImageDescriptionAnalysisResult> DescribeAnalysisAsync(
            Stream sourceImage,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sourceImage);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ImageDescriptionAnalysisResult.Unsupported);
        }
    }
}
