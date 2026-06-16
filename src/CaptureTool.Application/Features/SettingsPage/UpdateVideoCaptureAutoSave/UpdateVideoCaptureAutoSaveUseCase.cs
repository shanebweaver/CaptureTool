using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.UpdateVideoCaptureAutoSave;

public sealed class UpdateVideoCaptureAutoSaveUseCase : IUpdateVideoCaptureAutoSaveUseCase
{
    private const string ActivityId = "UpdateVideoCaptureAutoSave";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;

    public UpdateVideoCaptureAutoSaveUseCase(ISettingsService settingsService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateVideoCaptureAutoSaveRequest request) => true;

    public Task<UseCaseResponse<UpdateVideoCaptureAutoSaveResponse>> ExecuteAsync(UpdateVideoCaptureAutoSaveRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_AutoSave, request.IsEnabled);
                await _settingsService.TrySaveAsync(cancellationToken);
                return new UpdateVideoCaptureAutoSaveResponse();
            },
            cancellationToken: cancellationToken);
    }
}
