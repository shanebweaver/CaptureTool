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

        AppWindow.IsShownInSwitchers = false;
        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        Activated += ImageCaptureWindow_Activated;
    }

    private void ImageCaptureWindow_Activated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState == WindowActivationState.Deactivated)
        {
            Close();
        }
    }
}
