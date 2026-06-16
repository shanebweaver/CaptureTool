using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.UpdateVideoCaptureAutoCopy;

public sealed class UpdateVideoCaptureAutoCopyUseCase : IUpdateVideoCaptureAutoCopyUseCase
{
    private const string ActivityId = "UpdateVideoCaptureAutoCopy";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;

    public UpdateVideoCaptureAutoCopyUseCase(ISettingsService settingsService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateVideoCaptureAutoCopyRequest request) => true;

    public Task<UseCaseResponse<UpdateVideoCaptureAutoCopyResponse>> ExecuteAsync(UpdateVideoCaptureAutoCopyRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_AutoCopy, request.IsEnabled);
                await _settingsService.TrySaveAsync(cancellationToken);
                return new UpdateVideoCaptureAutoCopyResponse();
            },
            cancellationToken: cancellationToken);
    }
}
