using CaptureTool.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace CaptureTool.UI.Windows.Xaml.Windows;

public sealed partial class CaptureOverlayWindow : Window
{
    public CaptureOverlayWindowViewModel ViewModel { get; } = ViewModelLocator.GetViewModel<CaptureOverlayWindowViewModel>();

    public CaptureOverlayWindow()
    {
        InitializeComponent();

        AppWindow.IsShownInSwitchers = false;
        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CaptureOverlayWindowViewModel.WindowBounds))
        {
            AppWindow.MoveAndResize(new(ViewModel.WindowBounds.X, ViewModel.WindowBounds.Y, ViewModel.WindowBounds.Width, ViewModel.WindowBounds.Height));

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
                presenter.Maximize();
            }
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
