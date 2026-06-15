using CaptureTool.Application.Abstractions.Features.AudioEdit.SaveAudioFile;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Application.Features.AudioEdit.SaveAudioFile;

public sealed class SaveAudioFileUseCase : ISaveAudioFileUseCase
{
    private readonly IFilePickerService _filePickerService;

    public SaveAudioFileUseCase(IFilePickerService filePickerService)
    {
        _filePickerService = filePickerService;
    }

    public bool CanExecute(SaveAudioFileRequest request)
    {
        string filePath = request.AudioFilePath;
        bool canExecute = !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
        return canExecute;
    }

    public async Task<SaveAudioFileResponse> ExecuteAsync(SaveAudioFileRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            string filePath = request.AudioFilePath;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return new SaveAudioFileResponse(false);
            }

            IFile? file = await _filePickerService.PickSaveFileAsync(FilePickerType.Audio, UserFolder.Music);
            if (file is null)
            {
                return new SaveAudioFileResponse(false);
            }

            File.Copy(filePath, file.FilePath, true);
            return new SaveAudioFileResponse();
        }
        catch (Exception)
        {
            return new SaveAudioFileResponse(false);
        }
    }
}
