using CaptureTool.Capture;
using CaptureTool.Services.Interfaces.Themes;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices.WindowsRuntime;

namespace CaptureTool.UI.Windows.Xaml.Views;

public sealed partial class SelectionOverlayWindowView : SelectionOverlayWindowViewBase
{
    public SelectionOverlayWindowView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateRequestedAppTheme();
        });
    }

    ~SelectionOverlayWindowView()
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        SelectionOverlay.SelectionComplete += SelectionOverlay_SelectionComplete;

        if (ViewModel.IsLoaded)
        {
            SelectionOverlay.WindowRects = ViewModel.MonitorWindows;
            SelectionOverlay.SelectionRect = ViewModel.CaptureArea;
            SelectionOverlay.CaptureType = ViewModel.SelectedCaptureType.CaptureType;

            LoadBackgroundImage();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        SelectionOverlay.SelectionComplete -= SelectionOverlay_SelectionComplete;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SelectionOverlayWindowViewModel.SelectedCaptureTypeIndex):
                if (ViewModel.SelectedCaptureTypeIndex != -1)
                {
                    SelectionOverlay.CaptureType = ViewModel.SelectedCaptureType.CaptureType;
                }
                break;
            case nameof(SelectionOverlayWindowViewModel.CaptureArea):
                SelectionOverlay.SelectionRect = ViewModel.CaptureArea;
                break;
            case nameof(SelectionOverlayWindowViewModel.MonitorWindows):
                SelectionOverlay.WindowRects = ViewModel.MonitorWindows;
                break;
            case nameof(SelectionOverlayWindowViewModel.Monitor):
                LoadBackgroundImage();
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

    private void SelectionOverlay_SelectionComplete(object? sender, Rectangle captureArea)
    {
        ViewModel.CaptureArea = captureArea;

        if (captureArea.Height >= 40 && captureArea.Width >= 40)
        {
            ViewModel.RequestCaptureCommand.Execute(null);
        }
    }
}
