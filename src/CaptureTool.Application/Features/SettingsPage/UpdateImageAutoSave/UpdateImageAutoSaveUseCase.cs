using CaptureTool.Application.Abstractions.Features.Settings.UpdateImageAutoSave;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.UpdateImageAutoSave;

public sealed class UpdateImageAutoSaveUseCase : IUpdateImageAutoSaveUseCase
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
                _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoSave, request.IsEnabled);
                await _settingsService.TrySaveAsync(cancellationToken);
                return new UpdateImageAutoSaveResponse();
            },
            cancellationToken: cancellationToken);
    }
}
