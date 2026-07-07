using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Edit.Audio.CopyAudioFile;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Edit.Audio.CopyAudioFile;

internal sealed class CopyAudioFileUseCase : ICopyAudioFileUseCase
{
    private const string ActivityId = "CopyAudioFile";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IClipboardService _clipboardService;
    private readonly IFileSystem _fileSystem;

    public CopyAudioFileUseCase(IClipboardService clipboardService,
        IFileSystem fileSystem,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _clipboardService = clipboardService;
        _fileSystem = fileSystem;
    }

    public bool CanExecute(CopyAudioFileRequest request)
    {
        string audioPath = request.AudioPath;
        bool canExecute = !string.IsNullOrEmpty(audioPath) && _fileSystem.FileExists(audioPath);
        return canExecute;
    }

    public Task<UseCaseResponse<CopyAudioFileResponse>> ExecuteAsync(CopyAudioFileRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (string.IsNullOrEmpty(request.AudioPath) || !_fileSystem.FileExists(request.AudioPath))
                {
                    return new CopyAudioFileResponse(false);
                }

                ClipboardFile clipboardAudio = new(request.AudioPath);
                await _clipboardService.CopyFileAsync(clipboardAudio);
                return new CopyAudioFileResponse();
            },
            cancellationToken: cancellationToken);
    }
}
