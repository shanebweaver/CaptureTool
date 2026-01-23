using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.UseCases;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Application.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Application.Implementations.UseCases.Settings;

public sealed partial class SettingsUpdateVideoCaptureAutoSaveUseCase : AsyncUseCase<bool>, ISettingsUpdateVideoCaptureAutoSaveUseCase
{
    private readonly ISettingsService _settingsService;

    public SettingsUpdateVideoCaptureAutoSaveUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public override async Task ExecuteAsync(bool parameter, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_AutoSave, parameter);
        await _settingsService.TrySaveAsync(cancellationToken);
    }
}
