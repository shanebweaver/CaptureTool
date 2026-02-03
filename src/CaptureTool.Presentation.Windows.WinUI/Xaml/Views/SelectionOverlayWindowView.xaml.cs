using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Themes;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Windows;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Core;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Views;

public sealed partial class SelectionOverlayWindowView : SelectionOverlayWindowViewBase
{
    private WriteableBitmap? _backgroundBitmap;
    private SelectionOverlayWindow? _parentWindow;

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

    public void SetParentWindow(SelectionOverlayWindow window)
    {
        _parentWindow = window;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.CaptureOptionsUpdated += ViewModel_CaptureOptionsUpdated;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        SelectionOverlay.SelectionComplete += SelectionOverlay_SelectionComplete;

        if (ViewModel.IsLoaded)
        {
            SelectionOverlay.WindowRects = ViewModel.MonitorWindows;
            SelectionOverlay.SelectionRect = ViewModel.CaptureArea;

            CaptureType? selectedCaptureType = ViewModel.GetSelectedCaptureType();
            if (selectedCaptureType != null)
            {
                SelectionOverlay.CaptureType = selectedCaptureType.Value;
            }

            LoadBackgroundImage();
        }
    }

    private void ViewModel_CaptureOptionsUpdated(object? sender, CaptureOptions e)
    {
        var selectedCaptureType = ViewModel.GetSelectedCaptureType();
        if (selectedCaptureType != null)
        {
            SelectionOverlay.CaptureType = selectedCaptureType.Value;
        }

        SelectionOverlay.UpdateSelectionRect(Rectangle.Empty);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;

        ViewModel.CaptureOptionsUpdated -= ViewModel_CaptureOptionsUpdated;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        SelectionOverlay.SelectionComplete -= SelectionOverlay_SelectionComplete;

        CleanupBackgroundImage();
    }

    private void CleanupBackgroundImage()
    {
        if (RootPanel != null && RootPanel.Background is Microsoft.UI.Xaml.Media.ImageBrush imageBrush)
        {
            imageBrush.ImageSource = null;
            RootPanel.Background = null;
        }

        _backgroundBitmap = null;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ISelectionOverlayWindowViewModel.SelectedCaptureTypeIndex):
                var selectedCaptureType = ViewModel.GetSelectedCaptureType();
                if (selectedCaptureType != null)
                {
                    SelectionOverlay.CaptureType = selectedCaptureType.Value;
                }
                break;
            case nameof(ISelectionOverlayWindowViewModel.CaptureArea):
                SelectionOverlay.SelectionRect = ViewModel.CaptureArea;
                break;
            case nameof(ISelectionOverlayWindowViewModel.MonitorWindows):
                SelectionOverlay.WindowRects = ViewModel.MonitorWindows;
                break;
            case nameof(ISelectionOverlayWindowViewModel.Monitor):
                LoadBackgroundImage();
                break;
        }
    }

    private void LoadBackgroundImage()
    {
        CleanupBackgroundImage();

        if (ViewModel.Monitor is MonitorCaptureResult monitor && RootPanel is not null)
        {
            var monitorBounds = monitor.MonitorBounds;

            _backgroundBitmap = new WriteableBitmap(monitorBounds.Width, monitorBounds.Height);
            using (var stream = _backgroundBitmap.PixelBuffer.AsStream())
            {
                stream.Write(monitor.PixelBuffer, 0, monitor.PixelBuffer.Length);
            }

            RootPanel.Background = new Microsoft.UI.Xaml.Media.ImageBrush
            {
                ImageSource = _backgroundBitmap,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
            };

            // Force layout update to ensure background is measured and arranged
            RootPanel.UpdateLayout();
            
            // Wait for the composition/rendering to actually paint the frame
            // Use a one-shot rendering event handler to show window after next frame is rendered
            int renderingHandlerInvoked = 0;
            void OnRenderingForShow(object? sender, object args)
            {
                // Ensure this handler only executes once, even if multiple rendering events queue
                if (Interlocked.CompareExchange(ref renderingHandlerInvoked, 1, 0) != 0)
                {
                    return;
                }

                try
                {
                    _parentWindow?.ShowWindowWhenReady();
                }
                finally
                {
                    // Always unsubscribe, even if ShowWindowWhenReady throws
                    Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnRenderingForShow;
                }
            }

            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnRenderingForShow;
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

        RootPanel.SetValue(RequestedThemeProperty, theme);
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

    private void SelectionOverlay_SelectionComplete(object? _, Rectangle captureArea)
    {
        ViewModel.UpdateCaptureAreaCommand.Execute(captureArea);

        if (captureArea.Height >= 40 && captureArea.Width >= 40)
        {
            ViewModel.RequestCaptureCommand.Execute();
        }
    }

    private void SelectionToolbar_CaptureModeSelectionChanged(object _, int e)
    {
        ViewModel.UpdateSelectedCaptureModeCommand.Execute(e);
    }

    private void SelectionToolbar_CaptureTypeSelectionChanged(object _, int e)
    {
        ViewModel.UpdateSelectedCaptureTypeCommand.Execute(e);
    }

    private void SelectionOverlayContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = InputCursor.CreateFromCoreCursor(new CoreCursor(CoreCursorType.Cross, 1));
    }

    private void ToolbarContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = InputCursor.CreateFromCoreCursor(new CoreCursor(0, 1));
    }
}
