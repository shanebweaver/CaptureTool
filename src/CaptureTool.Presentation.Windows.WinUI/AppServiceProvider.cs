using CaptureTool.Application.DependencyInjection;
using CaptureTool.FeatureManagement.DependencyInjection;
using CaptureTool.Infrastructure.Capture.Windows.DependencyInjection;
using CaptureTool.Infrastructure.DependencyInjection;
using CaptureTool.Infrastructure.Edit.Windows.DependencyInjection;
using CaptureTool.Infrastructure.Analysis.Windows.DependencyInjection;
#if CAPTURETOOL_EXPERIMENTAL_WINDOWS_AI
using CaptureTool.Infrastructure.Analysis.Windows.Experimental.DependencyInjection;
#endif
using CaptureTool.Infrastructure.Analysis.FoundryLocal.DependencyInjection;
using CaptureTool.Infrastructure.Windows.DependencyInjection;
using CaptureTool.Presentation.DependencyInjection;
using CaptureTool.Presentation.Windows.WinUI.DependencyInjection;
using CaptureTool.Presentation.Windows.WinUI.UiTests;
using CaptureTool.FeatureManagement;
#if DEBUG
using CaptureTool.Presentation.Windows.WinUI.Debugging;
#endif
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
#if DEBUG
        collection.AddSingleton<IFeatureManager, DeveloperCaptureAnalysisFeatureManager>();
#endif

        // Generic services
        collection.AddGenericServices();

        // Windows services
        collection.AddWindowsServices(App.Current.DispatcherQueue);

        // Windows domains
        collection.AddWindowsCaptureDomains();
        collection.AddWindowsAnalysisDomains();
#if CAPTURETOOL_EXPERIMENTAL_WINDOWS_AI
        collection.AddExperimentalWindowsAiAnalysis();
#endif
        collection.AddFoundryLocalAnalysisProvider();
        collection.AddWindowsEditDomains();

        // Application layer
        collection.AddApplicationServices();
#if DEBUG
        collection.AddDeveloperCaptureAnalyzerSelection();
#endif

        // ViewModels
        collection.AddViewModels();

        // App specific handlers
        collection.AddAppWindowsServices();

        if (UiTestLaunchOptions.Current.IsEnabled)
        {
            collection.AddUiTestServices(UiTestLaunchOptions.Current);
        }

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
