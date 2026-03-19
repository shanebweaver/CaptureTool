using CaptureTool.Domain.Edit.Interfaces.Drawable;
using CaptureTool.Infrastructure.Interfaces.Loading;
using CaptureTool.Presentation.Windows.WinUI.Helpers;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Drawing;
using WinUIPoint = global::Windows.Foundation.Point;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class ImageEditPage : ImageEditPageBase
{
    private MenuFlyout? _imageContextMenu;
    private MenuFlyout? _shapeContextMenu;

    public ImageEditPage()
    {
        InitializeComponent();
        ViewModel.LoadStateChanged += ViewModel_LoadStateChanged;
        ViewModel.InvalidateCanvasRequested += ViewModel_InvalidateCanvasRequested;
        ViewModel.ForceZoomAndCenterRequested += ViewModel_ForceZoomAndCenterRequested;
        ImageCanvas.ZoomFactorChanged += ImageCanvas_ZoomFactorChanged;
        ImageCanvas.ImageContextMenuRequested += ImageCanvas_ImageContextMenuRequested;
        ImageCanvas.ShapeContextMenuRequested += ImageCanvas_ShapeContextMenuRequested;
        InitializeContextMenus();
    }

    ~ImageEditPage()
    {
        ViewModel.LoadStateChanged -= ViewModel_LoadStateChanged;
        ViewModel.InvalidateCanvasRequested -= ViewModel_InvalidateCanvasRequested;
        ViewModel.ForceZoomAndCenterRequested -= ViewModel_ForceZoomAndCenterRequested;
        ImageCanvas.ZoomFactorChanged -= ImageCanvas_ZoomFactorChanged;
        ImageCanvas.ShapeDrawn -= ImageCanvas_ShapeDrawn;
        ImageCanvas.ShapeDeleted -= ImageCanvas_ShapeDeleted;
        ImageCanvas.ShapeModified -= ImageCanvas_ShapeModified;
        ImageCanvas.ImageContextMenuRequested -= ImageCanvas_ImageContextMenuRequested;
        ImageCanvas.ShapeContextMenuRequested -= ImageCanvas_ShapeContextMenuRequested;
    }

    private void InitializeContextMenus()
    {
        // Image context menu: Save, Copy, Share, Undo, Redo
        _imageContextMenu = new MenuFlyout();

        _imageContextMenu.Items.Add(new MenuFlyoutItem
        {
            Text = "Save",
            Icon = new SymbolIcon(Symbol.Save),
            Command = XamlCommandHelpers.ToICommand(ViewModel.SaveCommand)
        });
        _imageContextMenu.Items.Add(new MenuFlyoutItem
        {
            Text = "Copy",
            Icon = new SymbolIcon(Symbol.Copy),
            Command = XamlCommandHelpers.ToICommand(ViewModel.CopyCommand)
        });
        _imageContextMenu.Items.Add(new MenuFlyoutItem
        {
            Text = "Share",
            Icon = new SymbolIcon(Symbol.Share),
            Command = XamlCommandHelpers.ToICommand(ViewModel.ShareCommand)
        });
        _imageContextMenu.Items.Add(new MenuFlyoutSeparator());
        _imageContextMenu.Items.Add(new MenuFlyoutItem
        {
            Text = "Undo",
            Icon = new SymbolIcon(Symbol.Undo),
            Command = XamlCommandHelpers.ToICommand(ViewModel.UndoCommand)
        });
        _imageContextMenu.Items.Add(new MenuFlyoutItem
        {
            Text = "Redo",
            Icon = new SymbolIcon(Symbol.Redo),
            Command = XamlCommandHelpers.ToICommand(ViewModel.RedoCommand)
        });

        // Shape context menu: Delete
        _shapeContextMenu = new MenuFlyout();

        var deleteItem = new MenuFlyoutItem
        {
            Text = "Delete",
            Icon = new SymbolIcon(Symbol.Delete)
        };
        deleteItem.Click += (_, _) => ImageCanvas.DeleteSelectedShape();
        _shapeContextMenu.Items.Add(deleteItem);
    }

    private void ImageCanvas_ImageContextMenuRequested(object? _, WinUIPoint position)
    {
        _imageContextMenu?.ShowAt(ImageCanvas, new FlyoutShowOptions { Position = position });
    }

    private void ImageCanvas_ShapeContextMenuRequested(object? _, WinUIPoint position)
    {
        _shapeContextMenu?.ShowAt(ImageCanvas, new FlyoutShowOptions { Position = position });
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

    private void ImageCanvas_ShapeModified(object? _, (int ShapeIndex, IDrawable OldState, IDrawable NewState) e)
    {
        ViewModel.OnShapeModified(e.ShapeIndex, e.OldState);
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
