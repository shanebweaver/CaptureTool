namespace CaptureTool.Application.Activation;

internal interface IApplicationStartupInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
