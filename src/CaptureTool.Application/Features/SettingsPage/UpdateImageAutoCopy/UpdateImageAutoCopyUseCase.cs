using CaptureTool.Application.Abstractions.Features.Settings.UpdateImageAutoCopy;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.UpdateImageAutoCopy;

public sealed class UpdateImageAutoCopyUseCase : IUpdateImageAutoCopyUseCase
{
    private const string ActivityId = "UpdateImageAutoCopy";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;

    public UpdateImageAutoCopyUseCase(ISettingsService settingsService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateImageAutoCopyRequest request) => true;

    public Task<UseCaseResponse<UpdateImageAutoCopyResponse>> ExecuteAsync(UpdateImageAutoCopyRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoCopy, request.IsEnabled);
                await _settingsService.TrySaveAsync(cancellationToken);
                return new UpdateImageAutoCopyResponse();
            },
            cancellationToken: cancellationToken);
    }
}
