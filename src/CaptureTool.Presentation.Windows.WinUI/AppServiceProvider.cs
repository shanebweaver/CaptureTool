using CaptureTool.Application.Implementations.DependencyInjection;
using CaptureTool.Domain.Capture.Implementations.Windows.DependencyInjection;
using CaptureTool.Domain.Edit.Implementations.Windows.DependencyInjection;
using CaptureTool.Infrastructure.Implementations.DependencyInjection;
using CaptureTool.Infrastructure.Implementations.FeatureManagement.DependencyInjection;
using CaptureTool.Infrastructure.Implementations.Windows.DependencyInjection;
using CaptureTool.Presentation.Windows.WinUI.DependencyInjection;
using CaptureTool.Application.Implementations.ViewModels.Factories.DependencyInjection;
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

        // Application services
        collection.AddApplicationServices();
        collection.AddApplicationCaptureServices();

        // Windows services
        collection.AddWindowsServices(App.Current.DispatcherQueue);

        // Windows domains
        collection.AddWindowsCaptureDomains();
        collection.AddWindowsEditDomains();

        // Action handlers
        collection
            .AddCaptureOverlayUseCases()
            .AddAboutUseCases()
            .AddAddOnsUseCases()
            .AddErrorUseCases()
            .AddLoadingUseCases()
            .AddHomeUseCases()
            .AddSettingsUseCases()
            .AddVideoEditUseCases()
            .AddAppMenuUseCases()
            .AddDiagnosticsUseCases();

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
