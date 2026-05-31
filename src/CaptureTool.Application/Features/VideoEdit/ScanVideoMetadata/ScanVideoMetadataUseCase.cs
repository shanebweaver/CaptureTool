using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Application.Features.VideoEdit.ScanVideoMetadata;

public sealed class ScanVideoMetadataUseCase : IUseCase<ScanVideoMetadataRequest, ScanVideoMetadataResponse>, IConditional<ScanVideoMetadataRequest>
{
    private readonly IMetadataScanningService _metadataScanningService;
    private readonly IFeatureManager _featureManager;

    public ScanVideoMetadataUseCase(
        IMetadataScanningService metadataScanningService,
        IFeatureManager featureManager)
    {
        _metadataScanningService = metadataScanningService;
        _featureManager = featureManager;
    }

    public bool CanExecute(ScanVideoMetadataRequest request)
    {
        return _featureManager.IsEnabled(AppFeatures.Feature_VideoCapture_MetadataCollection) &&
               !string.IsNullOrWhiteSpace(request.VideoPath) &&
               File.Exists(request.VideoPath);
    }

    public Task<ScanVideoMetadataResponse> ExecuteAsync(ScanVideoMetadataRequest request, CancellationToken cancellationToken = default)
    {
        if (!_featureManager.IsEnabled(AppFeatures.Feature_VideoCapture_MetadataCollection))
        {
            throw new InvalidOperationException("Cannot scan video metadata when metadata collection is disabled.");
        }

        if (!CanExecute(request))
        {
            throw new FileNotFoundException($"Video file not found: {request.VideoPath}", request.VideoPath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        IMetadataScanJob scanJob = _metadataScanningService.QueueScan(request.VideoPath);
        return Task.FromResult(new ScanVideoMetadataResponse(scanJob));
    }
}
