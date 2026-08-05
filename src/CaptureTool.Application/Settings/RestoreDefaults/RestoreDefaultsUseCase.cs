using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.RestoreDefaults;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.RestoreDefaults;

internal sealed class RestoreDefaultsUseCase : IRestoreDefaultsUseCase
{
    private const string ActivityId = "RestoreDefaults";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly ITelemetryConsentService _telemetryConsentService;
    private readonly IThemeService _themeService;

    public RestoreDefaultsUseCase(
        ISettingsService settingsService,
        ILocalizationService localizationService,
        ITelemetryConsentService telemetryConsentService,
        IThemeService themeService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _settingsService = settingsService;
        _localizationService = localizationService;
        _telemetryConsentService = telemetryConsentService;
        _themeService = themeService;
    }

    public bool CanExecute(RestoreDefaultsRequest request) => true;

    public Task<UseCaseResponse<RestoreDefaultsResponse>> ExecuteAsync(RestoreDefaultsRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                _themeService.ResetCurrentTheme();
                _settingsService.ClearAllSettings();
                _telemetryConsentService.SetState(TelemetryConsentState.Unknown);
                _localizationService.OverrideLanguage(null);
                await _settingsService.TrySaveAsync(cancellationToken);
                return new RestoreDefaultsResponse();
            },
            cancellationToken: cancellationToken);
    }
}
