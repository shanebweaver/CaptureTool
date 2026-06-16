using CaptureTool.Application.Abstractions.Features.Settings.UpdateAppLanguage;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.UpdateAppLanguage;

public sealed class UpdateAppLanguageUseCase : IUpdateAppLanguageUseCase
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
                _localization.OverrideLanguage(language);

                if (language?.Value is string value)
                {
                    _settings.Set(CaptureToolSettings.Settings_LanguageOverride, value);
                }
                else
                {
                    _settings.Unset(CaptureToolSettings.Settings_LanguageOverride);
                }

                await _settings.TrySaveAsync(cancellationToken);
                return new UpdateAppLanguageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
