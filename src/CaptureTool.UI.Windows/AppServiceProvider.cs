using CaptureTool.Core.Implementations.DependencyInjection;
using CaptureTool.Core.Implementations.Windows.DependencyInjection;
using CaptureTool.Domains.Capture.Implementations.Windows.DependencyInjection;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Domains.Edit.Implementations.Windows.DependencyInjection;
using CaptureTool.Services.Implementations.DependencyInjection;
using CaptureTool.Services.Implementations.FeatureManagement.DependencyInjection;
using CaptureTool.Services.Implementations.Windows.DependencyInjection;
using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Navigation;
using CaptureTool.Services.Interfaces.Themes;
using CaptureTool.Services.Interfaces.Windowing;
using CaptureTool.ViewModels;
using CaptureTool.ViewModels.Factories;
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
        collection.AddServiceServices();

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
            .AddCoreWindowsSettingsActions();

        // ViewModels
        collection.AddTransient<MainWindowViewModel>();
        collection.AddTransient<SelectionOverlayWindowViewModel>();
        collection.AddTransient<ErrorPageViewModel>();
        collection.AddTransient<AboutPageViewModel>();
        collection.AddTransient<AddOnsPageViewModel>();
        collection.AddTransient<HomePageViewModel>();
        collection.AddTransient<SettingsPageViewModel>();
        collection.AddTransient<LoadingPageViewModel>();
        collection.AddTransient<ImageEditPageViewModel>();
        collection.AddTransient<VideoEditPageViewModel>();
        collection.AddTransient<AppMenuViewModel>();
        collection.AddTransient<DiagnosticsViewModel>();
        collection.AddTransient<SelectionOverlayHostViewModel>();
        collection.AddTransient<CaptureOverlayViewModel>();

        // ViewModel factories
        collection.AddTransient<IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?>, AppLanguageViewModelFactory>();
        collection.AddTransient<IFactoryServiceWithArgs<AppThemeViewModel, AppTheme>, AppThemeViewModelFactory>();
        collection.AddTransient<IFactoryServiceWithArgs<CaptureModeViewModel, CaptureMode>, CaptureModeViewModelFactory>();
        collection.AddTransient<IFactoryServiceWithArgs<CaptureTypeViewModel, CaptureType>, CaptureTypeViewModelFactory>();
        collection.AddTransient<IFactoryServiceWithArgs<RecentCaptureViewModel, string>, RecentCaptureViewModelFactory>();

        // App specific handlers
        collection.AddSingleton<AppNavigationHandler>();
        collection.AddSingleton<INavigationHandler>(sp => sp.GetRequiredService<AppNavigationHandler>());
        collection.AddSingleton<IWindowHandleProvider>(sp => sp.GetRequiredService<AppNavigationHandler>());

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
