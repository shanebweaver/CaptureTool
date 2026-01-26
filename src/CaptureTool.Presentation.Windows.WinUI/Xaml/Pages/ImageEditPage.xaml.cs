using CaptureTool.Infrastructure.Interfaces.Loading;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Drawing;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class ImageEditPage : ImageEditPageBase
{
    private CancellationTokenSource? _sliderDebounceTokenSource;
    private const int SliderDebounceMs = 300;

    public ImageEditPage()
    {
        InitializeComponent();
        ViewModel.LoadStateChanged += ViewModel_LoadStateChanged;
        ViewModel.InvalidateCanvasRequested += ViewModel_InvalidateCanvasRequested;
        ViewModel.ForceZoomAndCenterRequested += ViewModel_ForceZoomAndCenterRequested;
        ImageCanvas.ZoomFactorChanged += ImageCanvas_ZoomFactorChanged;
    }

    ~ImageEditPage()
    {
        ViewModel.LoadStateChanged -= ViewModel_LoadStateChanged;
        ViewModel.InvalidateCanvasRequested -= ViewModel_InvalidateCanvasRequested;
        ViewModel.ForceZoomAndCenterRequested -= ViewModel_ForceZoomAndCenterRequested;
        ImageCanvas.ZoomFactorChanged -= ImageCanvas_ZoomFactorChanged;
        _sliderDebounceTokenSource?.Cancel();
        _sliderDebounceTokenSource?.Dispose();
    }

    private string FormatZoomPercentage(int zoomPercentage)
    {
        return $"{zoomPercentage}%";
    }

    // Conversion utilities
    private static double PercentageToFactor(int percentage) => percentage / 100.0;
    private static int FactorToPercentage(double factor)
    {
        // Round to nearest 5% to match slider step frequency
        int percentage = (int)Math.Round(factor * 100);
        // Snap to nearest multiple of 5
        return (int)(Math.Round(percentage / 5.0) * 5);
    }
    private static int ClampPercentage(int value) => Math.Clamp(value, 25, 200);

    private void ViewModel_LoadStateChanged(object? sender, LoadState e)
    {
        if (ViewModel.IsLoaded)
        {
            ImageCanvas.ForceCanvasRedrawWithResources();
        }
    }

    private void ViewModel_InvalidateCanvasRequested(object? _, EventArgs __)
    {
        ImageCanvas.InvalidateCanvas();
    }

    private void ViewModel_ForceZoomAndCenterRequested(object? _, EventArgs __)
    {
        ImageCanvas.ForceZoomAndCenter();
    }

    private void ImageCanvas_ZoomFactorChanged(object? _, (double ZoomFactor, ZoomUpdateSource Source) args)
    {
        // State machine: Handle zoom factor changes based on source
        switch (args.Source)
        {
            case ZoomUpdateSource.Slider:
                // Slider initiated the change, no propagation needed
                break;

            case ZoomUpdateSource.CanvasGesture:
            case ZoomUpdateSource.ZoomAndCenter:
            case ZoomUpdateSource.Programmatic:
                // Update ViewModel, which will update slider via binding
                int percentage = ClampPercentage(FactorToPercentage(args.ZoomFactor));
                ViewModel.UpdateZoomPercentageCommand.Execute(percentage);
                break;
        }
    }

    private void ImageCanvas_InteractionComplete(object _, Rectangle e)
    {
        ViewModel.OnCropInteractionComplete(e);
    }

    private void ImageCanvas_CropRectChanged(object _, Rectangle e)
    {
        ViewModel.UpdateCropRectCommand.Execute(e);
    }

    private void ChromaKeyAppBarToggleButton_IsCheckedChanged(object sender, RoutedEventArgs _)
    {
        if (sender is AppBarToggleButton toggleButton)
        {
            ViewModel.UpdateShowChromaKeyOptionsCommand.Execute(toggleButton.IsChecked ?? false);
        }
    }

    private void ChromaKeyToolbar_DesaturationChanged(object _, int e)
    {
        ViewModel.UpdateDesaturationCommand.Execute(e);
    }

    private void ChromaKeyToolbar_ToleranceChanged(object _, int e)
    {
        ViewModel.UpdateToleranceCommand.Execute(e);
    }

    private void ChromaKeyToolbar_SelectedColorOptionIndexChanged(object _, int e)
    {
        ViewModel.UpdateSelectedColorOptionIndexCommand.Execute(e);
    }

    private async void ZoomSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        // Debounce: Cancel any pending zoom application
        _sliderDebounceTokenSource?.Cancel();
        _sliderDebounceTokenSource?.Dispose();
        _sliderDebounceTokenSource = new CancellationTokenSource();

        int newPercentage = (int)e.NewValue;
        
        try
        {
            // Wait for user to stop dragging
            await Task.Delay(SliderDebounceMs, _sliderDebounceTokenSource.Token);
            
            // Apply zoom after debounce
            double zoomFactor = PercentageToFactor(newPercentage);
            ImageCanvas.SetZoom(zoomFactor, ZoomUpdateSource.Slider);
            
            // Update ViewModel for consistency (doesn't fire events that would loop back)
            ViewModel.UpdateZoomPercentageCommand.Execute(newPercentage);
        }
        catch (TaskCanceledException)
        {
            // Superseded by newer value, ignore
        }
    }

    private void AutoZoomLockToggle_IsCheckedChanged(object sender, RoutedEventArgs _)
    {
        if (sender is ToggleButton toggleButton)
        {
            ViewModel.UpdateAutoZoomLockCommand.Execute(toggleButton.IsChecked ?? false);
        }
    }
}
