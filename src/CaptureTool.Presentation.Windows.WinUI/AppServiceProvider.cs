using CaptureTool.Application.DependencyInjection;
using CaptureTool.FeatureManagement;
using CaptureTool.FeatureManagement.DependencyInjection;
using CaptureTool.Infrastructure.Capture.Windows.DependencyInjection;
using CaptureTool.Infrastructure.DependencyInjection;
using CaptureTool.Infrastructure.Edit.Windows.DependencyInjection;
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
        var featureManager = new MicrosoftFeatureManager();
        collection.AddWindowsCaptureDomains(options =>
            options.UseCaptureV2ScreenRecorder = featureManager.IsEnabled(AppFeatures.Feature_Capture_V2));
        collection.AddWindowsEditDomains();

        // Application layer
        collection.AddApplicationServices();

        // ViewModels
        collection.AddViewModels();

        // App specific handlers
        collection.AddAppWindowsServices();

        _serviceProvider = collection.BuildServiceProvider();
    }

    public T GetService<T>() where T : notnull => _serviceProvider.GetRequiredService<T>();
    public object GetService(Type t) => _serviceProvider.GetRequiredService(t);

    public void Dispose()
    {
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}
