using CaptureTool.Common.Commands;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Domains.Edit.Interfaces;
using CaptureTool.Domains.Edit.Interfaces.ChromaKey;
using CaptureTool.Domains.Edit.Interfaces.Drawable;
using System.Collections.ObjectModel;
using System.Drawing;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IImageEditPageViewModel
{
    event EventHandler? InvalidateCanvasRequested;
    
    AsyncRelayCommand CopyCommand { get; }
    RelayCommand ToggleCropModeCommand { get; }
    AsyncRelayCommand SaveCommand { get; }
    RelayCommand UndoCommand { get; }
    RelayCommand RedoCommand { get; }
    RelayCommand RotateCommand { get; }
    RelayCommand FlipHorizontalCommand { get; }
    RelayCommand FlipVerticalCommand { get; }
    AsyncRelayCommand PrintCommand { get; }
    AsyncRelayCommand ShareCommand { get; }
    RelayCommand<Color> UpdateChromaKeyColorCommand { get; }
    RelayCommand<ImageOrientation> UpdateOrientationCommand { get; }
    RelayCommand<Rectangle> UpdateCropRectCommand { get; }
    RelayCommand<bool> UpdateShowChromaKeyOptionsCommand { get; }
    RelayCommand<int> UpdateDesaturationCommand { get; }
    RelayCommand<int> UpdateToleranceCommand { get; }
    RelayCommand<int> UpdateSelectedColorOptionIndexCommand { get; }
    
    bool HasUndoStack { get; }
    bool HasRedoStack { get; }
    ObservableCollection<IDrawable> Drawables { get; }
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
    ObservableCollection<ChromaKeyColorOption> ChromaKeyColorOptions { get; }
    int SelectedChromaKeyColorOption { get; }
    bool IsChromaKeyAddOnOwned { get; }
    
    Task LoadAsync(ImageFile imageFile, CancellationToken cancellationToken);
    void OnCropInteractionComplete(Rectangle oldCropRect);
}
