using CaptureTool.Application.Abstractions.Features.Settings.RestoreDefaults;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.RestoreDefaults;

public sealed class RestoreDefaultsUseCase : IRestoreDefaultsUseCase
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;

    public RestoreDefaultsUseCase(ISettingsService settingsService, ILocalizationService localizationService)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
    }

    public bool CanExecute(RestoreDefaultsRequest request) => true;

    public async Task<RestoreDefaultsResponse> ExecuteAsync(RestoreDefaultsRequest request, CancellationToken cancellationToken = default)
    {
        _settingsService.ClearAllSettings();
        _localizationService.OverrideLanguage(null);
        await _settingsService.TrySaveAsync(cancellationToken);
        return new RestoreDefaultsResponse();
    }
}