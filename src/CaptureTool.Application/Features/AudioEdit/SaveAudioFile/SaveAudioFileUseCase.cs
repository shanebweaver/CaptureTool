using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.Features.AudioEdit.SaveAudioFile;

public sealed class SaveAudioFileUseCase : IUseCase<SaveAudioFileRequest, SaveAudioFileResponse>, IConditional<SaveAudioFileRequest>
{
    private readonly IFilePickerService _filePickerService;
    private readonly IWindowHandleProvider _windowingService;

    public SaveAudioFileUseCase(
        IFilePickerService filePickerService,
        IWindowHandleProvider windowingService)
    {
        _filePickerService = filePickerService;
        _windowingService = windowingService;
    }

    public bool CanExecute(SaveAudioFileRequest request)
    {
        string filePath = request.AudioFilePath;
        bool canExecute = !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
        return canExecute;
    }

    public async Task<SaveAudioFileResponse> ExecuteAsync(SaveAudioFileRequest request, CancellationToken cancellationToken = default)
    {
        string filePath = request.AudioFilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            throw new InvalidOperationException("Cannot save audio without a valid filepath.");
        }

        nint hwnd = _windowingService.GetMainWindowHandle();
        IFile file = await _filePickerService.PickSaveFileAsync(hwnd, FilePickerType.Audio, UserFolder.Music)
            ?? throw new OperationCanceledException("No file was selected.");

        File.Copy(filePath, file.FilePath, true);
        return new SaveAudioFileResponse();
    }
}
