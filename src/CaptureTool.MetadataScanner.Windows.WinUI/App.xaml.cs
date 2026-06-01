using CaptureTool.MetadataScanner.Windows.WinUI.Metadata;
using CaptureTool.MetadataScanner.Windows.WinUI.Metadata.Scanners;
using CaptureTool.MetadataScanner.Windows.WinUI.Services;
using CaptureTool.MetadataScanner.Windows.WinUI.ViewModels;
using CaptureTool.MetadataScanner.Windows.WinUI.Xaml.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace CaptureTool.MetadataScanner.Windows.WinUI;

public partial class App : Application
{
    public App()
    {
        Services = ConfigureServices();
        InitializeComponent();
    }

    internal IServiceProvider Services { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = Services.GetRequiredService<MainWindow>();
        window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IWindowHandleProvider, WindowHandleProvider>();
        services.AddSingleton<IMediaFilePicker, MediaFilePicker>();
        services.AddSingleton<IMetadataScannerRegistry, MetadataScannerRegistry>();
        services.AddSingleton<IMetadataScanningService, MetadataScanningService>();
        services.AddSingleton<IMediaFileMetadataScanner, BasicMediaFileScanner>();
        services.AddTransient<MainPageViewModel>();
        services.AddSingleton<MainWindow>();

        IServiceProvider serviceProvider = services.BuildServiceProvider();
        serviceProvider.RegisterMetadataScanners();

        return serviceProvider;
    }
}
