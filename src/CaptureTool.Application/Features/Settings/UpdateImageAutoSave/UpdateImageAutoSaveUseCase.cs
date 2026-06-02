using CaptureTool.Application.Abstractions.Features.Settings;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateImageAutoSave;
using CaptureTool.Application.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.UpdateImageAutoSave;

public sealed class UpdateImageAutoSaveUseCase : IUpdateImageAutoSaveUseCase
{
    private readonly ISettingsService _settingsService;

    public UpdateImageAutoSaveUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateImageAutoSaveRequest request) => true;

    public async Task<UpdateImageAutoSaveResponse> ExecuteAsync(UpdateImageAutoSaveRequest request, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoSave, request.IsEnabled);
        await _settingsService.TrySaveAsync(cancellationToken);
        return new UpdateImageAutoSaveResponse();
    }
}
