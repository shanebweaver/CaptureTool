using CaptureTool.Application.Interfaces.Commands;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Domain.Edit.Interfaces;
using CaptureTool.Domain.Edit.Interfaces.ChromaKey;
using CaptureTool.Domain.Edit.Interfaces.Drawable;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IImageEditPageViewModel
{
    event EventHandler? InvalidateCanvasRequested;
    
    IAsyncCommand CopyCommand { get; }
    ICommand ToggleCropModeCommand { get; }
    IAsyncCommand SaveCommand { get; }
    ICommand UndoCommand { get; }
    ICommand RedoCommand { get; }
    ICommand RotateCommand { get; }
    ICommand FlipHorizontalCommand { get; }
    ICommand FlipVerticalCommand { get; }
    IAsyncCommand PrintCommand { get; }
    IAsyncCommand ShareCommand { get; }
    ICommand UpdateChromaKeyColorCommand { get; }
    ICommand UpdateOrientationCommand { get; }
    ICommand UpdateCropRectCommand { get; }
    ICommand UpdateShowChromaKeyOptionsCommand { get; }
    ICommand UpdateDesaturationCommand { get; }
    ICommand UpdateToleranceCommand { get; }
    ICommand UpdateSelectedColorOptionIndexCommand { get; }
    
    bool HasUndoStack { get; }
    bool HasRedoStack { get; }
    IReadOnlyList<IDrawable> Drawables { get; }
    ImageFile? ImageFile { get; }
    Size ImageSize { get; }
    ImageOrientation Orientation { get; }
    string MirroredDisplayName { get; }
    string RotationDisplayName { get; }
    bool IsInCropMode { get; }
    Rectangle CropRect { get; }
    bool ShowChromaKeyOptions { get; }
    int ChromaKeyTolerance { get; }
    int ChromaKeyDesaturation { get; }
    Color ChromaKeyColor { get; }
    IReadOnlyList<ChromaKeyColorOption> ChromaKeyColorOptions { get; }
    int SelectedChromaKeyColorOption { get; }
    bool IsChromaKeyAddOnOwned { get; }
    
    Task LoadAsync(ImageFile imageFile, CancellationToken cancellationToken);
    void OnCropInteractionComplete(Rectangle oldCropRect);
}
