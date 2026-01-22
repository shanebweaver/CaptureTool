using CaptureTool.Common;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Localization;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class CaptureTypeViewModel : ViewModelBase
{
    public CaptureType CaptureType { get; }
    public string DisplayName { get; }
    public string AutomationName { get; }
    public string IconGlyphName { get; }

    public CaptureTypeViewModel(
        CaptureType captureType,
        ILocalizationService localizationService)
    {
        CaptureType = captureType;

        string captureTypeString = localizationService.GetString($"CaptureType_{Enum.GetName(captureType)}");
        DisplayName = captureTypeString;
        AutomationName = captureTypeString;

        IconGlyphName = captureType switch
        {
            CaptureType.Rectangle => "RectangularClipping",
            CaptureType.Window => "WindowSnipping",
            CaptureType.FullScreen => "FitPage",
            CaptureType.AllScreens => "Project",
            _ => string.Empty,
        };
    }
}
