using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Features.AudioEdit.CopyAudioFile;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.AudioEdit.CopyAudioFile;

public sealed class CopyAudioFileUseCase : ICopyAudioFileUseCase
{
    private const string ActivityId = "CopyAudioFile";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IClipboardService _clipboardService;

    public CopyAudioFileUseCase(IClipboardService clipboardService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _clipboardService = clipboardService;
    }

    public bool CanExecute(CopyAudioFileRequest request)
    {
        string audioPath = request.AudioPath;
        bool canExecute = !string.IsNullOrEmpty(audioPath) && File.Exists(audioPath);
        return canExecute;
    }

    public Task<UseCaseResponse<CopyAudioFileResponse>> ExecuteAsync(CopyAudioFileRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (string.IsNullOrEmpty(request.AudioPath) || !File.Exists(request.AudioPath))
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
