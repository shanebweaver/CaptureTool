using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.UpdateAppLanguage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.UpdateAppLanguage;

internal sealed class UpdateAppLanguageUseCase : IUpdateAppLanguageUseCase
{
    private const string ActivityId = "UpdateAppLanguage";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;

    public UpdateAppLanguageUseCase(ILocalizationService localization, ISettingsService settings,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _localization = localization;
        _settings = settings;
    }

    public bool CanExecute(UpdateAppLanguageRequest request)
    {
        var languages = _localization.SupportedLanguages;
        return request.LanguageIndex >= 0 && request.LanguageIndex <= languages.Length;
    }

    public Task<UseCaseResponse<UpdateAppLanguageResponse>> ExecuteAsync(UpdateAppLanguageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                var languages = _localization.SupportedLanguages;
                if (request.LanguageIndex < 0 || request.LanguageIndex > languages.Length)
                {
                    return new UpdateAppLanguageResponse(false);
                }

                var language = request.LanguageIndex == languages.Length ? null : languages[request.LanguageIndex];
                SettingsMutationResult result;
                if (language?.Value is string value)
                {
                    result = await _settings.TrySetAndSaveAsync(
                        CaptureToolSettings.Settings_LanguageOverride,
                        value,
                        cancellationToken);
                }
                else
                {
                    result = await _settings.TryUnsetAndSaveAsync(
                        CaptureToolSettings.Settings_LanguageOverride,
                        cancellationToken);
                }

                if (result.Succeeded)
                {
                    _localization.OverrideLanguage(language);
                }

                return new UpdateAppLanguageResponse(result.Succeeded);
            },
            cancellationToken: cancellationToken);
    }
}
