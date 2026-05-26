using CaptureTool.Application.Abstractions.AudioEdit;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.AudioEdit;

public class AudioEditSaveAppCommand : IAudioEditSaveAppCommand
{
    private readonly IFilePickerService _filePickerService;
    private readonly IWindowHandleProvider _windowingService;

    public AudioEditSaveAppCommand(
        IFilePickerService filePickerService,
        IWindowHandleProvider windowingService)
    {
        _filePickerService = filePickerService;
        _windowingService = windowingService;
    }

    public bool IsExecuting { get; protected set; }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(string parameter)
    {
        return !string.IsNullOrEmpty(parameter) && File.Exists(parameter);
    }

    public async Task ExecuteAsync(string audioPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(audioPath))
        {
            throw new InvalidOperationException("Cannot save audio without a valid filepath.");
        }

        nint hwnd = _windowingService.GetMainWindowHandle();
        IFile file = await _filePickerService.PickSaveFileAsync(hwnd, FilePickerType.Audio, UserFolder.Music)
            ?? throw new OperationCanceledException("No file was selected.");

        File.Copy(audioPath, file.FilePath, true);
    }
}
