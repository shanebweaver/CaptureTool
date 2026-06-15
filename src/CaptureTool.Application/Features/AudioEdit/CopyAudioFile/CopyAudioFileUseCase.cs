using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Features.AudioEdit.CopyAudioFile;

namespace CaptureTool.Application.Features.AudioEdit.CopyAudioFile;

public sealed class CopyAudioFileUseCase : ICopyAudioFileUseCase
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
        try
        {
            if (string.IsNullOrEmpty(request.AudioPath) || !File.Exists(request.AudioPath))
            {
                return new CopyAudioFileResponse(false);
            }

            ClipboardFile clipboardAudio = new(request.AudioPath);
            await _clipboardService.CopyFileAsync(clipboardAudio);
            return new CopyAudioFileResponse();
        }
        catch (Exception)
        {
            return new CopyAudioFileResponse(false);
        }
    }
}
