using CaptureTool.Infrastructure.Interfaces.Loading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Drawing;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class ImageEditPage : ImageEditPageBase
{
    public ImageEditPage()
    {
        InitializeComponent();
        ViewModel.LoadStateChanged += ViewModel_LoadStateChanged;
        ViewModel.InvalidateCanvasRequested += ViewModel_InvalidateCanvasRequested;
        ViewModel.ZoomLevelChanged += ViewModel_ZoomLevelChanged;
        ViewModel.ForceZoomAndCenterRequested += ViewModel_ForceZoomAndCenterRequested;
    }

    ~ImageEditPage()
    {
        ViewModel.LoadStateChanged -= ViewModel_LoadStateChanged;
        ViewModel.InvalidateCanvasRequested -= ViewModel_InvalidateCanvasRequested;
        ViewModel.ZoomLevelChanged -= ViewModel_ZoomLevelChanged;
        ViewModel.ForceZoomAndCenterRequested -= ViewModel_ForceZoomAndCenterRequested;
    }

    private string FormatZoomPercentage(double zoomLevel)
    {
        return $"{(int)(zoomLevel * 100)}%";
    }

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

    private void ViewModel_ZoomLevelChanged(object? _, double zoomLevel)
    {
        ImageCanvas.SetZoomLevel(zoomLevel);
    }

    private void ViewModel_ForceZoomAndCenterRequested(object? _, EventArgs __)
    {
        ImageCanvas.ForceZoomAndCenter();
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

    private void ZoomSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        ViewModel.UpdateZoomLevelCommand.Execute(e.NewValue);
    }

    private void AutoZoomLockToggle_IsCheckedChanged(object sender, RoutedEventArgs _)
    {
        if (sender is ToggleButton toggleButton)
        {
            ViewModel.UpdateAutoZoomLockCommand.Execute(toggleButton.IsChecked ?? false);
        }
    }
}
