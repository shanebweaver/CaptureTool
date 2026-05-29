using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.UpdateVideoCaptureAutoSave;

public sealed class UpdateVideoCaptureAutoSaveUseCase : IUseCase<UpdateVideoCaptureAutoSaveRequest, UpdateVideoCaptureAutoSaveResponse>, IConditional<UpdateVideoCaptureAutoSaveRequest>
{
    private readonly ISettingsService _settingsService;

    public UpdateVideoCaptureAutoSaveUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateVideoCaptureAutoSaveRequest request) => true;

    public async Task<UpdateVideoCaptureAutoSaveResponse> ExecuteAsync(UpdateVideoCaptureAutoSaveRequest request, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_AutoSave, request.IsEnabled);
        await _settingsService.TrySaveAsync(cancellationToken);
        return new UpdateVideoCaptureAutoSaveResponse();
    }
}