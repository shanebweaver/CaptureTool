using CaptureTool.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace CaptureTool.UI.Xaml.Windows;

public sealed partial class ImageCaptureWindow : Window
{
    public ImageCaptureWindowViewModel ViewModel { get; } = ViewModelLocator.GetViewModel<ImageCaptureWindowViewModel>();

    public ImageCaptureWindow()
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
