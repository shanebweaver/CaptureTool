using CaptureTool.Infrastructure.Interfaces.Loading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Drawing;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class ImageEditPage : ImageEditPageBase
{
    public ImageEditPage()
    {
        InitializeComponent();
        ViewModel.LoadStateChanged += ViewModel_LoadStateChanged;
        ViewModel.InvalidateCanvasRequested += ViewModel_InvalidateCanvasRequested;
        ShapeTypeComboBox.SelectionChanged += ShapeTypeComboBox_SelectionChanged;
    }

    ~ImageEditPage()
    {
        ViewModel.LoadStateChanged -= ViewModel_LoadStateChanged;
        ViewModel.InvalidateCanvasRequested -= ViewModel_InvalidateCanvasRequested;
        ShapeTypeComboBox.SelectionChanged -= ShapeTypeComboBox_SelectionChanged;
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

    private void ImageCanvas_InteractionComplete(object _, Rectangle e)
    {
        ViewModel.OnCropInteractionComplete(e);
    }

    private void ImageCanvas_CropRectChanged(object _, Rectangle e)
    {
        ViewModel.UpdateCropRectCommand.Execute(e);
    }

    private void ImageCanvas_ShapeDrawn(object _, (System.Numerics.Vector2 Start, System.Numerics.Vector2 End) e)
    {
        ViewModel.OnShapeDrawn(e.Start, e.End);
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

    private void ShapeTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string tag)
        {
            if (int.TryParse(tag, out int shapeTypeValue) && Enum.IsDefined(typeof(CaptureTool.Domain.Edit.Interfaces.ShapeType), shapeTypeValue))
            {
                var shapeType = (CaptureTool.Domain.Edit.Interfaces.ShapeType)shapeTypeValue;
                ViewModel.UpdateSelectedShapeTypeCommand.Execute(shapeType);
            }
        }
    }
}
