using CaptureTool.Domain.Capture.Abstractions.Metadata;
using Windows.Graphics.Imaging;

namespace CaptureTool.Domain.Capture.Windows.Metadata;

internal interface ISoftwareBitmapVideoMetadataScanner
{
    Task<MetadataEntry?> ScanBitmapAsync(
        SoftwareBitmap softwareBitmap,
        long timestamp,
        CancellationToken cancellationToken = default);
}
