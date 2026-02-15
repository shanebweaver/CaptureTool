using CaptureTool.Application.Interfaces.UseCases.AudioEdit;
using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Windowing;

namespace CaptureTool.Application.Implementations.UseCases.AudioEdit;

public sealed partial class AudioEditSaveUseCase : AsyncUseCase<string>, IAudioEditSaveUseCase
{
    private readonly IFilePickerService _filePickerService;
    private readonly IWindowHandleProvider _windowingService;

    public AudioEditSaveUseCase(
        IFilePickerService filePickerService,
        IWindowHandleProvider windowingService)
    {
        _filePickerService = filePickerService;
        _windowingService = windowingService;
    }

    public override async Task ExecuteAsync(string audioPath, CancellationToken cancellationToken = default)
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
