using CaptureTool.Application.Implementations.Settings;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Application.Implementations.UseCases.Settings;

public sealed partial class SettingsUpdateImageAutoSaveUseCase : AsyncUseCase<bool>, ISettingsUpdateImageAutoSaveUseCase
{
    private readonly ISettingsService _settingsService;
    public SettingsUpdateImageAutoSaveUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }
    public override async Task ExecuteAsync(bool parameter, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoSave, parameter);
        await _settingsService.TrySaveAsync(cancellationToken);
    }
}
