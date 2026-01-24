using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Localization;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class CaptureTypeViewModel : ViewModelBase, ICaptureTypeViewModel
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
