using CaptureTool.Storage;

namespace CaptureTool.Capture.Video;

public sealed partial class VideoCaptureOptions : CaptureOptions
{
    public override CaptureMode CaptureMode { get; } = CaptureMode.Video;

    public VideoCaptureMode VideoCaptureMode { get; }

    public VideoFileType VideoFileType { get; }

    public VideoCaptureOptions(VideoCaptureMode captureMode, VideoFileType fileType, bool autoSave)
        : base(autoSave)
    {
        VideoCaptureMode = captureMode;
        VideoFileType = fileType;
    }
}
