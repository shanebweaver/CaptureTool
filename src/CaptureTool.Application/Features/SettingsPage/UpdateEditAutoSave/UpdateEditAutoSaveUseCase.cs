using CaptureTool.Application.Abstractions.Features.Settings.UpdateEditAutoSave;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.Settings;

namespace CaptureTool.Application.Features.SettingsPage.UpdateEditAutoSave;

public sealed class UpdateEditAutoSaveUseCase : IUpdateEditAutoSaveUseCase
{
    private const string ActivityId = "UpdateEditAutoSave";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;

    public UpdateEditAutoSaveUseCase(ISettingsService settingsService, IUseCaseExecutor useCaseExecutor)
    {
        _settingsService = settingsService;
        _useCaseExecutor = useCaseExecutor;
    }

    public bool CanExecute(UpdateEditAutoSaveRequest request) => true;

    public Task<UseCaseResponse<UpdateEditAutoSaveResponse>> ExecuteAsync(UpdateEditAutoSaveRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                _settingsService.Set(CaptureToolSettings.Settings_Edit_AutoSave, request.IsEnabled);
                await _settingsService.TrySaveAsync(cancellationToken);
                return new UpdateEditAutoSaveResponse();
            },
            cancellationToken: cancellationToken);
    }
}
