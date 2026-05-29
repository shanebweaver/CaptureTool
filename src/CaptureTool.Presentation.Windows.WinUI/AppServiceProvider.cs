using CaptureTool.Application.DependencyInjection;
using CaptureTool.Domain.Capture.Windows.DependencyInjection;
using CaptureTool.Domain.Edit.Windows.DependencyInjection;
using CaptureTool.FeatureManagement.DependencyInjection;
using CaptureTool.Infrastructure.DependencyInjection;
using CaptureTool.Infrastructure.Windows.DependencyInjection;
using CaptureTool.Presentation.DependencyInjection;
using CaptureTool.Presentation.Windows.WinUI.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Presentation.Windows.WinUI;

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

        // Windows services
        collection.AddWindowsServices(App.Current.DispatcherQueue);

        // Windows domains
        collection.AddWindowsCaptureDomains();
        collection.AddWindowsEditDomains();

        // Application layer
        collection.AddApplicationServices();

        // ViewModels
        collection.AddViewModels();

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
