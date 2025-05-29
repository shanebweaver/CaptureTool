using CaptureTool.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;

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
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.Maximize();
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

    private async void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        RootPanel.Opacity = 0;

        // Allow the UI thread to process the opacity change and render.
        // This is not ideal, but there is no deterministic way to ensure that the UI is updated in time for the capture.
        await Task.Yield();
        await Task.Yield();
        await Task.Delay(50);

        DispatcherQueue.TryEnqueue(() =>
        {
            ViewModel.PerformCaptureCommand.Execute(null);
        });
    }
}
