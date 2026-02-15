using CaptureTool.Infrastructure.Interfaces.Loading;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;
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
        ViewModel.ForceZoomAndCenterRequested += ViewModel_ForceZoomAndCenterRequested;
        ImageCanvas.ZoomFactorChanged += ImageCanvas_ZoomFactorChanged;
    }

    ~ImageEditPage()
    {
        ViewModel.LoadStateChanged -= ViewModel_LoadStateChanged;
        ViewModel.InvalidateCanvasRequested -= ViewModel_InvalidateCanvasRequested;
        ViewModel.ForceZoomAndCenterRequested -= ViewModel_ForceZoomAndCenterRequested;
        ImageCanvas.ZoomFactorChanged -= ImageCanvas_ZoomFactorChanged;
        ImageCanvas.ShapeDrawn -= ImageCanvas_ShapeDrawn;
    }

    private string FormatZoomPercentage(int zoomPercentage)
    {
        return $"{zoomPercentage}%";
    }

    // Conversion utilities
    private static double PercentageToFactor(int percentage) => percentage / 100.0;
    private static int FactorToPercentage(double factor)
    {
        return (int)Math.Round(factor * 100);
    }
    private static int ClampPercentage(int value) => Math.Clamp(value, 1, 200);

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

    private void ImageCanvas_ShapeDrawn(object? _, (System.Numerics.Vector2 Start, System.Numerics.Vector2 End) e)
    {
        ViewModel.OnShapeDrawn(e.Start, e.End);
    }

    private void ImageCanvas_ShapeDeleted(object? _, int shapeIndex)
    {
        ViewModel.OnShapeDeleted(shapeIndex);
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

    private void ShapeToolbar_SelectedShapeTypeIndexChanged(object _, int e)
    {
        if (Enum.IsDefined(typeof(CaptureTool.Domain.Edit.Interfaces.ShapeType), e))
        {
            var shapeType = (CaptureTool.Domain.Edit.Interfaces.ShapeType)e;
            ViewModel.UpdateSelectedShapeTypeCommand.Execute(shapeType);
        }
    }

    private void ShapeToolbar_StrokeColorChanged(object _, Color e)
    {
        ViewModel.UpdateShapeStrokeColorCommand.Execute(e);
    }

    private void ShapeToolbar_FillColorChanged(object _, Color e)
    {
        ViewModel.UpdateShapeFillColorCommand.Execute(e);
    }

    private void ShapeToolbar_StrokeWidthChanged(object _, int e)
    {
        ViewModel.UpdateShapeStrokeWidthCommand.Execute(e);
    }

    private async void ZoomSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        int newPercentage = (int)e.NewValue;

        double zoomFactor = PercentageToFactor(newPercentage);
        ImageCanvas.SetZoom(zoomFactor, ZoomUpdateSource.Slider);

        ViewModel.UpdateZoomPercentageCommand.Execute(newPercentage);
    }

    private void AutoZoomLockToggle_IsCheckedChanged(object sender, RoutedEventArgs _)
    {
        if (sender is ToggleButton toggleButton)
        {
            ViewModel.UpdateAutoZoomLockCommand.Execute(toggleButton.IsChecked ?? false);
        }
    }
}
