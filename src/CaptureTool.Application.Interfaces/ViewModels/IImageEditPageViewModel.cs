using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Domain.Edit.Interfaces;
using CaptureTool.Domain.Edit.Interfaces.ChromaKey;
using CaptureTool.Domain.Edit.Interfaces.Drawable;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.ViewModels;
using System.Drawing;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IImageEditPageViewModel : IViewModel
{
    event EventHandler? InvalidateCanvasRequested;
    event EventHandler<double>? ZoomLevelChanged;
    event EventHandler? ForceZoomAndCenterRequested;

    IAsyncAppCommand CopyCommand { get; }
    IAppCommand ToggleCropModeCommand { get; }
    IAsyncAppCommand SaveCommand { get; }
    IAppCommand UndoCommand { get; }
    IAppCommand RedoCommand { get; }
    IAppCommand RotateCommand { get; }
    IAppCommand FlipHorizontalCommand { get; }
    IAppCommand FlipVerticalCommand { get; }
    IAsyncAppCommand PrintCommand { get; }
    IAsyncAppCommand ShareCommand { get; }
    IAppCommand<Color> UpdateChromaKeyColorCommand { get; }
    IAppCommand<ImageOrientation> UpdateOrientationCommand { get; }
    IAppCommand<Rectangle> UpdateCropRectCommand { get; }
    IAppCommand<bool> UpdateShowChromaKeyOptionsCommand { get; }
    IAppCommand<int> UpdateDesaturationCommand { get; }
    IAppCommand<int> UpdateToleranceCommand { get; }
    IAppCommand<int> UpdateSelectedColorOptionIndexCommand { get; }
    IAppCommand<double> UpdateZoomLevelCommand { get; }
    IAppCommand<bool> UpdateAutoZoomLockCommand { get; }
    IAppCommand ZoomAndCenterCommand { get; }

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
    double ZoomLevel { get; }
    bool IsAutoZoomLocked { get; }

    Task LoadAsync(ImageFile imageFile, CancellationToken cancellationToken);
    void OnCropInteractionComplete(Rectangle oldCropRect);
}
