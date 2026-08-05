using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.UpdateImageAutoSave;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.UpdateImageAutoSave;

internal sealed class UpdateImageAutoSaveUseCase : IUpdateImageAutoSaveUseCase
{
    private const string ActivityId = "UpdateImageAutoSave";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;

    public UpdateImageAutoSaveUseCase(ISettingsService settingsService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateImageAutoSaveRequest request) => true;

    public Task<UseCaseResponse<UpdateImageAutoSaveResponse>> ExecuteAsync(UpdateImageAutoSaveRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                SettingsMutationResult result = await _settingsService.TrySetAndSaveAsync(
                    CaptureToolSettings.Settings_ImageCapture_AutoSave,
                    request.IsEnabled,
                    cancellationToken);
                return new UpdateImageAutoSaveResponse(result.Succeeded);
            },
            cancellationToken: cancellationToken);
    }
}
