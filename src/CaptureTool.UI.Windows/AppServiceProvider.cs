using CaptureTool.Core.Implementations.DependencyInjection;
using CaptureTool.Domains.Capture.Implementations.Windows.DependencyInjection;
using CaptureTool.Domains.Edit.Implementations.Windows.DependencyInjection;
using CaptureTool.Services.Implementations.DependencyInjection;
using CaptureTool.Services.Implementations.FeatureManagement.DependencyInjection;
using CaptureTool.Services.Implementations.Windows.DependencyInjection;
using CaptureTool.UI.Windows.DependencyInjection;
using CaptureTool.ViewModels.DependencyInjection;
using CaptureTool.ViewModels.Factories.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.UI.Windows;

public partial class AppServiceProvider : IServiceProvider, IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public AppServiceProvider()
    {
        ServiceCollection collection = new();

        // Feature management
        collection.AddFeatureManagementServices();

        // Generic services
        collection.AddGenericServices();

        // Core services
        collection.AddCoreServices();
        collection.AddCoreCaptureServices();

        // Windows services
        collection.AddWindowsServices(App.Current.DispatcherQueue);

        // Windows domains
        collection.AddWindowsCaptureDomains();
        collection.AddWindowsEditDomains();

        // Action handlers
        collection
            .AddCaptureOverlayActions()
            .AddAboutActions()
            .AddAddOnsActions()
            .AddErrorActions()
            .AddLoadingActions()
            .AddHomeActions()
            .AddSettingsActions()
            .AddVideoEditActions()
            .AddAppMenuActions()
            .AddDiagnosticsActions();

        // ViewModels
        collection.AddViewModels();

        // ViewModel factories
        collection.AddViewModelFactories();

        // App specific handlers
        collection.AddAppWindowsServices();

        _serviceProvider = collection.BuildServiceProvider();
        
        // Register metadata scanners with the registry
        _serviceProvider.RegisterMetadataScanners();
    }

    public T GetService<T>() where T : notnull => _serviceProvider.GetRequiredService<T>();
    public object GetService(Type t) => _serviceProvider.GetRequiredService(t);

    public void Dispose()
    {
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}
