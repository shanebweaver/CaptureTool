using CaptureTool.Application.Abstractions.Features.Settings.RestoreDefaults;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.RestoreDefaults;

public sealed class RestoreDefaultsUseCase : IRestoreDefaultsUseCase
{
    private const string ActivityId = "RestoreDefaults";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;

    public RestoreDefaultsUseCase(ISettingsService settingsService, ILocalizationService localizationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _settingsService = settingsService;
        _localizationService = localizationService;
    }

    public bool CanExecute(RestoreDefaultsRequest request) => true;

    public Task<UseCaseResponse<RestoreDefaultsResponse>> ExecuteAsync(RestoreDefaultsRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                _settingsService.ClearAllSettings();
                _localizationService.OverrideLanguage(null);
                await _settingsService.TrySaveAsync(cancellationToken);
                return new RestoreDefaultsResponse();
            },
            cancellationToken: cancellationToken);
    }
}
