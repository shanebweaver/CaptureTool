using CaptureTool.Domain.Edit.Operations;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Presentation.Loading;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Drawing;
using Point = global::Windows.Foundation.Point;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class ImageEditPage : ImageEditPageBase
{
    private MenuFlyout? _imageContextMenu;
    private MenuFlyout? _shapeContextMenu;
    private MenuFlyoutItem? _saveMenuItem;
    private MenuFlyoutItem? _copyMenuItem;
    private MenuFlyoutItem? _shareMenuItem;
    private MenuFlyoutItem? _undoMenuItem;
    private MenuFlyoutItem? _redoMenuItem;

    public ImageEditPage()
    {
        InitializeComponent();
        InitializeContextMenus();
        ViewModel.LoadStateChanged += ViewModel_LoadStateChanged;
        ViewModel.InvalidateCanvasRequested += ViewModel_InvalidateCanvasRequested;
        ViewModel.ForceZoomAndCenterRequested += ViewModel_ForceZoomAndCenterRequested;
        ImageCanvas.ZoomFactorChanged += ImageCanvas_ZoomFactorChanged;
        ImageCanvas.ImageContextMenuRequested += ImageCanvas_ImageContextMenuRequested;
        ImageCanvas.ShapeContextMenuRequested += ImageCanvas_ShapeContextMenuRequested;
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
        _imageContextMenu = new MenuFlyout();
        _saveMenuItem = CreateMenuItem(
            "Save",
            new SymbolIcon(Symbol.Save),
            ViewModel.SaveCommand);
        _copyMenuItem = CreateMenuItem(
            "Copy",
            new SymbolIcon(Symbol.Copy),
            ViewModel.CopyCommand);
        _shareMenuItem = CreateMenuItem(
            "Share",
            new SymbolIcon(Symbol.Share),
            ViewModel.ShareCommand);
        _undoMenuItem = CreateMenuItem(
            "Undo",
            new SymbolIcon(Symbol.Undo),
            ViewModel.UndoCommand);
        _redoMenuItem = CreateMenuItem(
            "Redo",
            new SymbolIcon(Symbol.Redo),
            ViewModel.RedoCommand);

        _imageContextMenu.Items.Add(_saveMenuItem!);
        _imageContextMenu.Items.Add(_copyMenuItem!);
        _imageContextMenu.Items.Add(_shareMenuItem!);
        _imageContextMenu.Items.Add(new MenuFlyoutSeparator());
        _imageContextMenu.Items.Add(_undoMenuItem!);
        _imageContextMenu.Items.Add(_redoMenuItem!);

        _shapeContextMenu = new MenuFlyout();
        var deleteMenuItem = new MenuFlyoutItem
        {
            Text = "Delete",
            Icon = new SymbolIcon(Symbol.Delete)
        };
        deleteMenuItem.Click += (_, _) => ImageCanvas.DeleteSelectedShape();
        _shapeContextMenu.Items.Add(deleteMenuItem);
    }

    private void UpdateImageContextMenuState()
    {
        _saveMenuItem?.IsEnabled = ViewModel.IsLoaded;

        _copyMenuItem?.IsEnabled = ViewModel.IsLoaded;

        _shareMenuItem?.IsEnabled = ViewModel.IsLoaded;

        _undoMenuItem?.IsEnabled = ViewModel.HasUndoStack;

        _redoMenuItem?.IsEnabled = ViewModel.HasRedoStack;
    }

    private static MenuFlyoutItem CreateMenuItem(string text, IconElement icon, System.Windows.Input.ICommand command)
    {
        return new()
        {
            Text = text,
            Icon = icon,
            Command = command
        };
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

    private void ImageCanvas_ShapeModified(object? _, (int ShapeIndex, ModifyShapeOperation.ShapeState OldState, ModifyShapeOperation.ShapeState NewState) e)
    {
        ViewModel.OnShapeModified(e.ShapeIndex, e.OldState, e.NewState);
    }

    private void ImageCanvas_TextBoxDrawn(object? _, (System.Numerics.Vector2 Start, System.Numerics.Vector2 End) e)
    {
        ViewModel.OnTextBoxDrawn(e.Start, e.End);
    }

    private void ImageCanvas_TextDrawableSelected(object? _, TextDrawable e)
    {
        ViewModel.OnTextDrawableSelected(e);
    }

    private void ImageCanvas_ImageContextMenuRequested(object? _, Point position)
    {
        UpdateImageContextMenuState();
        _imageContextMenu?.ShowAt(ImageCanvas, new FlyoutShowOptions { Position = position });
    }

    private void ImageCanvas_ShapeContextMenuRequested(object? _, Point position)
    {
        _shapeContextMenu?.ShowAt(ImageCanvas, new FlyoutShowOptions { Position = position });
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
        if (Enum.IsDefined(typeof(CaptureTool.Domain.Edit.ShapeType), e))
        {
            var shapeType = (CaptureTool.Domain.Edit.ShapeType)e;
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

    private void ShapeToolbar_StrokeOpacityChanged(object _, int e)
    {
        ViewModel.UpdateShapeStrokeOpacityCommand.Execute(e);
    }

    private void ShapeToolbar_FillOpacityChanged(object _, int e)
    {
        ViewModel.UpdateShapeFillOpacityCommand.Execute(e);
    }

    private void TextToolbar_FontColorChanged(object _, Color e)
    {
        ViewModel.UpdateTextFontColorCommand.Execute(e);
    }

    private void TextToolbar_FontColorOpacityChanged(object _, int e)
    {
        ViewModel.UpdateTextFontColorOpacityCommand.Execute(e);
    }

    private void TextToolbar_BackgroundColorChanged(object _, Color e)
    {
        ViewModel.UpdateTextBackgroundColorCommand.Execute(e);
    }

    private void TextToolbar_BackgroundColorOpacityChanged(object _, int e)
    {
        ViewModel.UpdateTextBackgroundColorOpacityCommand.Execute(e);
    }

    private void TextToolbar_FontFamilyChanged(object _, string e)
    {
        ViewModel.UpdateTextFontFamilyCommand.Execute(e);
    }

    private void TextToolbar_FontSizeChanged(object _, int e)
    {
        ViewModel.UpdateTextFontSizeCommand.Execute(e);
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
