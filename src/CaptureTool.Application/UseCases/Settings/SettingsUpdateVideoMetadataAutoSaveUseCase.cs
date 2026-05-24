using CaptureTool.Application.Settings;
using CaptureTool.Application.Abstractions.UseCases.Settings;
using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Application.UseCases.Settings;

public sealed partial class SettingsUpdateVideoMetadataAutoSaveUseCase : AsyncUseCase<bool>, ISettingsUpdateVideoMetadataAutoSaveUseCase
{
    private readonly ISettingsService _settingsService;

    public SettingsUpdateVideoMetadataAutoSaveUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public override async Task ExecuteAsync(bool parameter, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_MetadataAutoSave, parameter);
        await _settingsService.TrySaveAsync(cancellationToken);
    }
}
