using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.UpdateEditWarnBeforeDiscard;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.UpdateEditWarnBeforeDiscard;

internal sealed class UpdateEditWarnBeforeDiscardUseCase : IUpdateEditWarnBeforeDiscardUseCase
{
    private const string ActivityId = "UpdateEditWarnBeforeDiscard";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;

    public UpdateEditWarnBeforeDiscardUseCase(ISettingsService settingsService, IUseCaseExecutor useCaseExecutor)
    {
        _settingsService = settingsService;
        _useCaseExecutor = useCaseExecutor;
    }

    public bool CanExecute(UpdateEditWarnBeforeDiscardRequest request) => true;

    public Task<UseCaseResponse<UpdateEditWarnBeforeDiscardResponse>> ExecuteAsync(UpdateEditWarnBeforeDiscardRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                SettingsMutationResult result = await _settingsService.TrySetAndSaveAsync(
                    CaptureToolSettings.Settings_Edit_WarnBeforeDiscard,
                    request.IsEnabled,
                    cancellationToken);
                return new UpdateEditWarnBeforeDiscardResponse(result.Succeeded);
            },
            cancellationToken: cancellationToken);
    }
}
