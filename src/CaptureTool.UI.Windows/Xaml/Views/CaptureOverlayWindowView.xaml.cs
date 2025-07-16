using CaptureTool.Capture;
using CaptureTool.Services.Themes;
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

        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateRequestedAppTheme();
            LoadBackgroundImage();
            //LoadToolbar();
        });
    }

    //private void LoadToolbar()
    //{
    //    if (ViewModel.IsPrimary)
    //    {
    //        Toolbar.Visibility = Visibility.Visible;
    //        Toolbar.IsVideoCaptureEnabled = ViewModel.IsVideoCaptureEnabled;
    //        Toolbar.SupportedCaptureModes = ViewModel.SupportedCaptureModes;
    //        Toolbar.SelectedCaptureModeIndex = ViewModel.SelectedCaptureModeIndex;
    //        Toolbar.SupportedCaptureTypes = ViewModel.SupportedCaptureTypes;
    //        Toolbar.SelectedCaptureTypeIndex = ViewModel.SelectedCaptureTypeIndex;

    //        Toolbar.CaptureTypeSelectionChanged += Toolbar_CaptureTypeSelectionChanged;
    //        Toolbar.CaptureModeSelectionChanged += Toolbar_CaptureModeSelectionChanged;
    //    }
    //}

    private void Toolbar_CaptureModeSelectionChanged(object? sender, int e)
    {
        ViewModel.SelectedCaptureModeIndex = e;
    }

    private void Toolbar_CaptureTypeSelectionChanged(object? sender, int e)
    {
        ViewModel.SelectedCaptureTypeIndex = e;
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
