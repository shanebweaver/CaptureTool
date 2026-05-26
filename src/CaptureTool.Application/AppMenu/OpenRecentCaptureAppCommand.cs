using CaptureTool.Application.Abstractions.AppMenu;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Domain.Capture.Abstractions;

namespace CaptureTool.Application.AppMenu;

internal class OpenRecentCaptureAppCommand : IOpenRecentCaptureAppCommand
{
    public OpenRecentCaptureAppCommand(
        IFileTypeDetector fileTypeDetector,
        IGoToAudioEditAppCommand goToAudioEditAppCommand,
        IGoToImageEditAppCommand goToImageEditAppCommand,
        IGoToVideoEditAppCommand goToVideoEditAppCommand)
    {
        _fileTypeDetector = fileTypeDetector;
        _goToAudioEditAppCommand = goToAudioEditAppCommand;
        _goToImageEditAppCommand = goToImageEditAppCommand;
        _goToVideoEditAppCommand = goToVideoEditAppCommand;
    }

    private readonly IFileTypeDetector _fileTypeDetector;
    private readonly IGoToAudioEditAppCommand _goToAudioEditAppCommand;
    private readonly IGoToImageEditAppCommand _goToImageEditAppCommand;
    private readonly IGoToVideoEditAppCommand _goToVideoEditAppCommand;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(string filePath)
    {
        return !string.IsNullOrWhiteSpace(filePath);
    }

    public void Execute(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var fileType = _fileTypeDetector.DetectFileType(filePath);
        switch (fileType)
        {
            case CaptureFileType.Audio:
                AudioFile audioFile = new(filePath);
                _goToAudioEditAppCommand.Execute(audioFile);
                break;

            case CaptureFileType.Image:
                ImageFile imageFile = new(filePath);
                _goToImageEditAppCommand.Execute(imageFile);
                break;

            case CaptureFileType.Video:
                VideoFile videoFile = new(filePath);
                _goToVideoEditAppCommand.Execute(videoFile);
                break;

            default:
                throw new InvalidOperationException($"Unknown file type: {fileType}");
        }
    }
}