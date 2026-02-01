using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Themes;
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

    public event EventHandler? BackgroundImageLoaded;

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

            // Animate opacity from 0 to 1 for smooth fade-in effect
            AnimateFadeIn();

            // Notify that background image has been loaded
            BackgroundImageLoaded?.Invoke(this, EventArgs.Empty);
        }
    }

    private void AnimateFadeIn()
    {
        if (RootPanel == null)
        {
            return;
        }

        // Create a smooth fade-in animation
        var fadeInAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase
            {
                EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut
            }
        };

        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        storyboard.Children.Add(fadeInAnimation);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fadeInAnimation, RootPanel);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fadeInAnimation, "Opacity");

        storyboard.Begin();
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
