using CaptureTool.Application.Abstractions.AudioEdit;
using CaptureTool.Application.Abstractions.ImageEdit;
using CaptureTool.Application.Abstractions.RecentCaptures;
using CaptureTool.Application.Abstractions.VideoEdit;
using CaptureTool.Domain.Capture.Abstractions;

namespace CaptureTool.Application.UseCases.RecentCaptures;

internal class OpenRecentCaptureAppCommand : IOpenRecentCaptureAppCommand
{
    public OpenRecentCaptureAppCommand(
        IFileTypeDetector fileTypeDetector,
        IOpenAudioEditPageAppCommand navigateToAudioEditAppCommand,
        IOpenImageEditPageAppCommand navigateToImageEditAppCommand,
        IOpenVideoEditPageAppCommand navigateToVideoEditAppCommand)
    {
        _fileTypeDetector = fileTypeDetector;
        _goToAudioEditAppCommand = navigateToAudioEditAppCommand;
        _goToImageEditAppCommand = navigateToImageEditAppCommand;
        _goToVideoEditAppCommand = navigateToVideoEditAppCommand;
    }

    private readonly IFileTypeDetector _fileTypeDetector;
    private readonly IOpenAudioEditPageAppCommand _goToAudioEditAppCommand;
    private readonly IOpenImageEditPageAppCommand _goToImageEditAppCommand;
    private readonly IOpenVideoEditPageAppCommand _goToVideoEditAppCommand;

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