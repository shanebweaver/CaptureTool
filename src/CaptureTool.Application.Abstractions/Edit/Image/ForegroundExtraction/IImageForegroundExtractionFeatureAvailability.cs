namespace CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;

public interface IImageForegroundExtractionFeatureAvailability
{
    bool IsImageForegroundExtractionEnabled { get; }
}
