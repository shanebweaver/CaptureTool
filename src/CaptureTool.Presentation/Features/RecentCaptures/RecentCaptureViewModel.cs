using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.ViewModels;

namespace CaptureTool.Presentation.Features.RecentCaptures;

public sealed partial class RecentCaptureViewModel : ViewModelBase
{
    public string FilePath
    {
        get;
        private set => Set(ref field, value);
    }

    public string FileName
    {
        get;
        private set => Set(ref field, value);
    }

    public CaptureFileType CaptureFileType
    {
        get;
        private set => Set(ref field, value);
    }

    public string CaptureTypeLabel { get; }

    public string IconGlyph => CaptureFileType switch
    {
        CaptureFileType.Image => "\uE722",
        CaptureFileType.Video => "\uE714",
        CaptureFileType.Audio => "\uE720",
        _ => "\uE7C3"
    };

    public bool CanLoadThumbnail => CaptureFileType is CaptureFileType.Image or CaptureFileType.Video;

    public void SetDisplayName(string name) => FileName = name;

    public RecentCaptureViewModel(string temporaryFilePath, ILocalizationService localization)
    {
        FilePath = temporaryFilePath;
        FileName = Path.GetFileName(temporaryFilePath);
        CaptureFileType = CaptureFileTypeDetector.DetectFileType(temporaryFilePath);
        CaptureTypeLabel = localization.GetString(CaptureFileType switch
        {
            CaptureFileType.Image => "Common_MediaType_Image",
            CaptureFileType.Video => "Common_MediaType_Video",
            CaptureFileType.Audio => "Common_MediaType_Audio",
            _ => "Common_MediaType_File"
        });
    }
}
