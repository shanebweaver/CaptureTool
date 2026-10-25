using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestTextExtractionFeatureAvailability : ITextExtractionFeatureAvailability
{
    public bool IsTextExtractionEnabled => UiTestLaunchOptions.AiProfile != "none";
}
