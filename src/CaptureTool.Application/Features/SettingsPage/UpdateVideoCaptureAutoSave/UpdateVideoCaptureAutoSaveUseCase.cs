using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Features.Settings;

namespace CaptureTool.Application.Features.SettingsPage.UpdateVideoCaptureAutoSave;

public sealed class UpdateVideoCaptureAutoSaveUseCase : IUpdateVideoCaptureAutoSaveUseCase
{
    private readonly ISettingsService _settingsService;

    public UpdateVideoCaptureAutoSaveUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateVideoCaptureAutoSaveRequest request) => true;

    public async Task<UpdateVideoCaptureAutoSaveResponse> ExecuteAsync(UpdateVideoCaptureAutoSaveRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_AutoSave, request.IsEnabled);
            await _settingsService.TrySaveAsync(cancellationToken);
            return new UpdateVideoCaptureAutoSaveResponse();
        }
        catch (Exception)
        {
            return new UpdateVideoCaptureAutoSaveResponse(false);
        }
    }
}
