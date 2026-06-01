using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.MetadataScanner.Windows.WinUI.Metadata;

internal static class MetadataScannerRegistrationExtensions
{
    public static IServiceProvider RegisterMetadataScanners(this IServiceProvider serviceProvider)
    {
        var registry = serviceProvider.GetRequiredService<IMetadataScannerRegistry>();

        foreach (var scanner in serviceProvider.GetServices<IMediaFileMetadataScanner>())
        {
            registry.RegisterMediaFileScanner(scanner);
        }

        return serviceProvider;
    }
}
