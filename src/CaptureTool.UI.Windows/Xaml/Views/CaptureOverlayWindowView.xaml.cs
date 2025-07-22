using CaptureTool.Capture;
using CaptureTool.Services.Themes;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Drawing;
using System.Runtime.InteropServices.WindowsRuntime;

namespace CaptureTool.UI.Windows.Xaml.Views;

public sealed partial class CaptureOverlayWindowView : CaptureOverlayWindowViewBase
{
    public CaptureOverlayWindowView()
    {
        InitializeComponent();

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateRequestedAppTheme();
            LoadBackgroundImage();
        });
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CaptureOverlayWindowViewModel.SelectedCaptureType):
                SelectionOverlay.CaptureType = ViewModel.SelectedCaptureType;
                break;
            case nameof(CaptureOverlayWindowViewModel.CaptureArea):
                SelectionOverlay.SelectionRect = ViewModel.CaptureArea;
                break;
            case nameof(CaptureOverlayWindowViewModel.MonitorWindows):
                SelectionOverlay.WindowRects = ViewModel.MonitorWindows;
                break;
        }
    }

    private void LoadBackgroundImage()
    {
        if (ViewModel.Monitor is MonitorCaptureResult monitor && RootPanel is not null)
        {
            var monitorBounds = monitor.MonitorBounds;

            var writeableBitmap = new WriteableBitmap(monitorBounds.Width, monitorBounds.Height);
            using (var stream = writeableBitmap.PixelBuffer.AsStream())
            {
                stream.Write(monitor.PixelBuffer, 0, monitor.PixelBuffer.Length);
            }

            RootPanel.Background = new Microsoft.UI.Xaml.Media.ImageBrush
            {
                ImageSource = writeableBitmap,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
            };
        }
    }

    private void UpdateRequestedAppTheme()
    {
        object theme = ViewModel.CurrentAppTheme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            AppTheme.SystemDefault => ConvertToElementTheme(ViewModel.DefaultAppTheme),
            _ => DependencyProperty.UnsetValue
        };

        RootPanel.SetValue(FrameworkElement.RequestedThemeProperty, theme);
    }

    private static ElementTheme ConvertToElementTheme(AppTheme appTheme)
    {
        return appTheme switch
        {
            AppTheme.SystemDefault => ElementTheme.Default,
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private void SelectionOverlay_SelectionComplete(object sender, Rectangle captureArea)
    {
        ViewModel.CaptureArea = captureArea;

        if (captureArea.Height >= 40 && captureArea.Width >= 40)
        {
            ViewModel.RequestCaptureCommand.Execute(null);
        }
    }

    private void OnCloseRequested(object sender, EventArgs e)
    {
        ViewModel.CloseOverlayCommand.Execute(null);
    }

    private void OnEscapeRequested(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseOverlayCommand.Execute(null);
    }
}
