using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Features.Settings;

namespace CaptureTool.Application.Features.SettingsPage.UpdateVideoCaptureAutoCopy;

public sealed class UpdateVideoCaptureAutoCopyUseCase : IUpdateVideoCaptureAutoCopyUseCase
{
    private readonly ISettingsService _settingsService;

    public UpdateVideoCaptureAutoCopyUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateVideoCaptureAutoCopyRequest request) => true;

    public async Task<UpdateVideoCaptureAutoCopyResponse> ExecuteAsync(UpdateVideoCaptureAutoCopyRequest request, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_AutoCopy, request.IsEnabled);
        await _settingsService.TrySaveAsync(cancellationToken);
        return new UpdateVideoCaptureAutoCopyResponse();
    }
}
