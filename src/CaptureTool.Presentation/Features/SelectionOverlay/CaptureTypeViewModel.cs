using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.ViewModels;

namespace CaptureTool.Presentation.Features.SelectionOverlay;

public sealed partial class CaptureTypeViewModel : ViewModelBase
{
    public CaptureType CaptureType { get; }
    public string DisplayName { get; }
    public string AutomationName { get; }

    public CaptureTypeViewModel(
        CaptureType captureType,
        ILocalizationService localizationService)
    {
        CaptureType = captureType;

        string captureTypeString = localizationService.GetString($"CaptureType_{Enum.GetName(captureType)}");
        DisplayName = captureTypeString;
        AutomationName = captureTypeString;
    }
}
