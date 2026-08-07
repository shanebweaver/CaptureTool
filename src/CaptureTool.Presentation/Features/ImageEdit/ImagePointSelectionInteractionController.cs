using System.Numerics;

namespace CaptureTool.Presentation.Features.ImageEdit;

internal enum ImagePointSelectionMode
{
    None,
    ForegroundExtraction,
    ObjectErase,
    ObjectExtraction,
}

internal readonly record struct ImagePointSelectionRequest(
    ImagePointSelectionMode Mode,
    Vector2 Position);

internal sealed class ImagePointSelectionInteractionController
{
    public ImagePointSelectionMode ActiveMode { get; private set; }

    public bool IsActive => ActiveMode != ImagePointSelectionMode.None;

    public ImagePointSelectionMode ResolveMode(
        bool isForegroundExtractionEnabled,
        bool isObjectEraseEnabled,
        bool isObjectExtractionEnabled)
    {
        ActiveMode = isObjectExtractionEnabled
            ? ImagePointSelectionMode.ObjectExtraction
            : isObjectEraseEnabled
                ? ImagePointSelectionMode.ObjectErase
                : isForegroundExtractionEnabled
                    ? ImagePointSelectionMode.ForegroundExtraction
                    : ImagePointSelectionMode.None;

        return ActiveMode;
    }

    public bool TrySelect(
        bool isPrimaryButtonPressed,
        bool isInsideImage,
        Vector2 position,
        out ImagePointSelectionRequest request)
    {
        if (!IsActive || !isPrimaryButtonPressed || !isInsideImage)
        {
            request = default;
            return false;
        }

        request = new ImagePointSelectionRequest(ActiveMode, position);
        return true;
    }
}
