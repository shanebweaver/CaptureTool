using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Clipboard;

namespace CaptureTool.Application.Features.AudioEdit.CopyAudioFile;

public sealed class CopyAudioFileUseCase : IUseCase<CopyAudioFileRequest, CopyAudioFileResponse>, IConditional<CopyAudioFileRequest>
{
    private readonly IClipboardService _clipboardService;

    public CopyAudioFileUseCase(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public bool CanExecute(CopyAudioFileRequest request)
    {
        string audioPath = request.AudioPath;
        bool canExecute = !string.IsNullOrEmpty(audioPath) && File.Exists(audioPath);
        return canExecute;
    }

    public async Task<CopyAudioFileResponse> ExecuteAsync(CopyAudioFileRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.AudioPath))
        {
            throw new InvalidOperationException("Cannot copy audio to clipboard without a valid filepath.");
        }

        ClipboardFile clipboardAudio = new(request.AudioPath);
        await _clipboardService.CopyFileAsync(clipboardAudio);
        return new CopyAudioFileResponse();
    }
}
