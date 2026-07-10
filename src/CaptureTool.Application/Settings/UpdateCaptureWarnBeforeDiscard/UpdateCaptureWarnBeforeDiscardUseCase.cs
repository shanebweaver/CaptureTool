using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.UpdateCaptureWarnBeforeDiscard;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.UpdateCaptureWarnBeforeDiscard;

internal sealed class UpdateCaptureWarnBeforeDiscardUseCase : IUpdateCaptureWarnBeforeDiscardUseCase
{
    private const string ActivityId = "UpdateCaptureWarnBeforeDiscard";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;

    public UpdateCaptureWarnBeforeDiscardUseCase(ISettingsService settingsService, IUseCaseExecutor useCaseExecutor)
    {
        _settingsService = settingsService;
        _useCaseExecutor = useCaseExecutor;
    }

    public bool CanExecute(UpdateCaptureWarnBeforeDiscardRequest request) => true;

    public Task<UseCaseResponse<UpdateCaptureWarnBeforeDiscardResponse>> ExecuteAsync(UpdateCaptureWarnBeforeDiscardRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                _settingsService.Set(CaptureToolSettings.Settings_Capture_WarnBeforeDiscard, request.IsEnabled);
                await _settingsService.TrySaveAsync(cancellationToken);
                return new UpdateCaptureWarnBeforeDiscardResponse();
            },
            cancellationToken: cancellationToken);
    }
}
