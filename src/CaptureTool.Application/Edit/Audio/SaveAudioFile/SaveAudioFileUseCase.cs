using CaptureTool.Application.Abstractions.Edit.Audio.SaveAudioFile;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Edit.Audio.SaveAudioFile;

internal sealed class SaveAudioFileUseCase : ISaveAudioFileUseCase
{
    private const string ActivityId = "SaveAudioFile";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFilePickerService _filePickerService;
    private readonly IFileSystem _fileSystem;

    public SaveAudioFileUseCase(IFilePickerService filePickerService,
        IFileSystem fileSystem,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _filePickerService = filePickerService;
        _fileSystem = fileSystem;
    }

    public bool CanExecute(SaveAudioFileRequest request)
    {
        string filePath = request.AudioFilePath;
        bool canExecute = !string.IsNullOrEmpty(filePath) && _fileSystem.FileExists(filePath);
        return canExecute;
    }

    public Task<UseCaseResponse<SaveAudioFileResponse>> ExecuteAsync(SaveAudioFileRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                string filePath = request.AudioFilePath;
                if (string.IsNullOrEmpty(filePath) || !_fileSystem.FileExists(filePath))
                {
                    return new SaveAudioFileResponse(false);
                }

                FileReference? file = await _filePickerService.PickSaveFileAsync(FilePickerType.Audio, UserFolder.Music);
                if (file is null)
                {
                    return new SaveAudioFileResponse(false);
                }

                _fileSystem.CopyFile(filePath, file.FilePath, true);
                return new SaveAudioFileResponse();
            },
            cancellationToken: cancellationToken);
    }
}
