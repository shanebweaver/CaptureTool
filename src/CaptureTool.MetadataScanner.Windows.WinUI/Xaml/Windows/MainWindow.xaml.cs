using CaptureTool.MetadataScanner.Windows.WinUI.Services;
using CaptureTool.MetadataScanner.Windows.WinUI.ViewModels;
using CaptureTool.MetadataScanner.Windows.WinUI.Xaml.Pages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace CaptureTool.MetadataScanner.Windows.WinUI.Xaml.Windows;

public sealed partial class MainWindow : Window
{
    public MainWindow(
        IWindowHandleProvider windowHandleProvider,
        MainPageViewModel mainPageViewModel)
    {
        InitializeComponent();

        Title = "Metadata Scanner";
        Content = new MainPage(mainPageViewModel);

        windowHandleProvider.SetWindowHandle(WindowNative.GetWindowHandle(this));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 640;
            presenter.PreferredMinimumHeight = 480;
        }

        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(900, 640));
    }
}
