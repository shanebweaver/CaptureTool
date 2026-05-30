using CaptureTool.Domain.Capture.Abstractions.Metadata;

namespace CaptureTool.Application.Features.VideoEdit.ScanVideoMetadata;

public sealed record ScanVideoMetadataResponse(IMetadataScanJob ScanJob);
