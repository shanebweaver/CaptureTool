using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Implementations.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Localization;

namespace CaptureTool.Presentation.ViewModels;

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
