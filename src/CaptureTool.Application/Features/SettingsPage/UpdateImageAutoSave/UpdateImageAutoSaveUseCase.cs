using CaptureTool.Application.Abstractions.Features.Settings.UpdateImageAutoSave;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Features.Settings;

namespace CaptureTool.Application.Features.SettingsPage.UpdateImageAutoSave;

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
        try
        {
            _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoSave, request.IsEnabled);
            await _settingsService.TrySaveAsync(cancellationToken);
            return new UpdateImageAutoSaveResponse();
        }
        catch (Exception)
        {
            return new UpdateImageAutoSaveResponse(false);
        }
    }
}
