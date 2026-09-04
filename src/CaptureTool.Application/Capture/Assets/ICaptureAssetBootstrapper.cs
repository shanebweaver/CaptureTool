namespace CaptureTool.Application.Capture.Assets;

internal interface ICaptureAssetBootstrapper
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
