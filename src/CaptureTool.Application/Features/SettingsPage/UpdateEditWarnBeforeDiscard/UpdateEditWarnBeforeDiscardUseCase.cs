using CaptureTool.Application.Abstractions.Features.Settings.UpdateEditWarnBeforeDiscard;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.Settings;

namespace CaptureTool.Application.Features.SettingsPage.UpdateEditWarnBeforeDiscard;

public sealed class UpdateEditWarnBeforeDiscardUseCase : IUpdateEditWarnBeforeDiscardUseCase
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
                _settingsService.Set(CaptureToolSettings.Settings_Edit_WarnBeforeDiscard, request.IsEnabled);
                await _settingsService.TrySaveAsync(cancellationToken);
                return new UpdateEditWarnBeforeDiscardResponse();
            },
            cancellationToken: cancellationToken);
    }
}
