using CaptureTool.Application.Abstractions.Features.Settings;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateImageAutoCopy;
using CaptureTool.Application.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.UpdateImageAutoCopy;

public sealed class UpdateImageAutoCopyUseCase : IUpdateImageAutoCopyUseCase
{
    private readonly ISettingsService _settingsService;

    public UpdateImageAutoCopyUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateImageAutoCopyRequest request) => true;

    public async Task<UpdateImageAutoCopyResponse> ExecuteAsync(UpdateImageAutoCopyRequest request, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoCopy, request.IsEnabled);
        await _settingsService.TrySaveAsync(cancellationToken);
        return new UpdateImageAutoCopyResponse();
    }
}
