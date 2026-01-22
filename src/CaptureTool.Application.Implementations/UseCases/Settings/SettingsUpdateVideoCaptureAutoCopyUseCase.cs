using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Application.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Application.Implementations.UseCases.Settings;

public sealed partial class SettingsUpdateVideoCaptureAutoCopyUseCase : AsyncActionCommand<bool>, ISettingsUpdateVideoCaptureAutoCopyUseCase
{
    private readonly ISettingsService _settingsService;

    public SettingsUpdateVideoCaptureAutoCopyUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public override async Task ExecuteAsync(bool parameter, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_AutoCopy, parameter);
        await _settingsService.TrySaveAsync(cancellationToken);
    }
}
