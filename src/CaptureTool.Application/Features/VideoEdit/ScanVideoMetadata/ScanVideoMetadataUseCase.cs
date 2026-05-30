using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Capture.Abstractions.Metadata;

namespace CaptureTool.Application.Features.VideoEdit.ScanVideoMetadata;

public sealed class ScanVideoMetadataUseCase : IUseCase<ScanVideoMetadataRequest, ScanVideoMetadataResponse>, IConditional<ScanVideoMetadataRequest>
{
    private readonly IMetadataScanningService _metadataScanningService;

    public ScanVideoMetadataUseCase(IMetadataScanningService metadataScanningService)
    {
        _metadataScanningService = metadataScanningService;
    }

    public bool CanExecute(ScanVideoMetadataRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.VideoPath) && File.Exists(request.VideoPath);
    }

    public Task<ScanVideoMetadataResponse> ExecuteAsync(ScanVideoMetadataRequest request, CancellationToken cancellationToken = default)
    {
        if (!CanExecute(request))
        {
            throw new FileNotFoundException($"Video file not found: {request.VideoPath}", request.VideoPath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        IMetadataScanJob scanJob = _metadataScanningService.QueueScan(request.VideoPath);
        return Task.FromResult(new ScanVideoMetadataResponse(scanJob));
    }
}
