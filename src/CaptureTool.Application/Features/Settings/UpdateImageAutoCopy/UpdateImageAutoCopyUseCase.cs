using CaptureTool.Application.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.UpdateImageAutoCopy;

public sealed class UpdateImageAutoCopyUseCase : IUseCase<UpdateImageAutoCopyRequest, UpdateImageAutoCopyResponse>, IConditional<UpdateImageAutoCopyRequest>
{
    private readonly ISettingsService _settingsService;

    public UpdateImageAutoCopyUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public Task<bool> CanExecuteAsync(UpdateImageAutoCopyRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public async Task<UpdateImageAutoCopyResponse> ExecuteAsync(UpdateImageAutoCopyRequest request, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoCopy, request.IsEnabled);
        await _settingsService.TrySaveAsync(cancellationToken);
        return new UpdateImageAutoCopyResponse();
    }
}