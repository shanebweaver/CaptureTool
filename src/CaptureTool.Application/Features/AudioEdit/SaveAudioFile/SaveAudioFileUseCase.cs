using CaptureTool.Application.Abstractions.Features.AudioEdit.SaveAudioFile;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Capture.Files;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.AudioEdit.SaveAudioFile;

public sealed class SaveAudioFileUseCase : ISaveAudioFileUseCase
{
    private const string ActivityId = "SaveAudioFile";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFilePickerService _filePickerService;

    public SaveAudioFileUseCase(IFilePickerService filePickerService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _filePickerService = filePickerService;
    }

    public bool CanExecute(SaveAudioFileRequest request)
    {
        string filePath = request.AudioFilePath;
        bool canExecute = !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
        return canExecute;
    }

    public Task<UseCaseResponse<SaveAudioFileResponse>> ExecuteAsync(SaveAudioFileRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
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
            },
            cancellationToken: cancellationToken);
    }
}
