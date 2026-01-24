using CaptureTool.Application.Implementations.Settings;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Application.Implementations.UseCases.Settings;

public sealed partial class SettingsUpdateVideoCaptureAutoCopyUseCase : AsyncUseCase<bool>, ISettingsUpdateVideoCaptureAutoCopyUseCase
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
