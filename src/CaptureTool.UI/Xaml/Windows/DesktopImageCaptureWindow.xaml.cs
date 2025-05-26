using CaptureTool.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace CaptureTool.UI.Xaml.Windows;

public sealed partial class DesktopImageCaptureWindow : Window
{
    public DesktopImageCaptureWindowViewModel ViewModel { get; } = ViewModelLocator.GetViewModel<DesktopImageCaptureWindowViewModel>();

    public DesktopImageCaptureWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;

        AppWindow.IsShownInSwitchers = false;
        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }
    }
}
