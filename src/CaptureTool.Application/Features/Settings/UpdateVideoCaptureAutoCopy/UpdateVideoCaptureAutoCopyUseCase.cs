using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.UpdateVideoCaptureAutoCopy;

public sealed class UpdateVideoCaptureAutoCopyUseCase : IUseCase<UpdateVideoCaptureAutoCopyRequest, UpdateVideoCaptureAutoCopyResponse>, IConditional<UpdateVideoCaptureAutoCopyRequest>
{
    private readonly ISettingsService _settingsService;

    public UpdateVideoCaptureAutoCopyUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateVideoCaptureAutoCopyRequest request) => true;

    public async Task<UpdateVideoCaptureAutoCopyResponse> ExecuteAsync(UpdateVideoCaptureAutoCopyRequest request, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_AutoCopy, request.IsEnabled);
        await _settingsService.TrySaveAsync(cancellationToken);
        return new UpdateVideoCaptureAutoCopyResponse();
    }
}