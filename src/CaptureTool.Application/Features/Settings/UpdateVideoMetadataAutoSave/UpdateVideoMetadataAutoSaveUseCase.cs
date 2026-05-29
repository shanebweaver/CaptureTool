using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.UpdateVideoMetadataAutoSave;

public sealed class UpdateVideoMetadataAutoSaveUseCase : IUseCase<UpdateVideoMetadataAutoSaveRequest, UpdateVideoMetadataAutoSaveResponse>, IConditional<UpdateVideoMetadataAutoSaveRequest>
{
    private readonly ISettingsService _settingsService;

    public UpdateVideoMetadataAutoSaveUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public Task<bool> CanExecuteAsync(UpdateVideoMetadataAutoSaveRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public async Task<UpdateVideoMetadataAutoSaveResponse> ExecuteAsync(UpdateVideoMetadataAutoSaveRequest request, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_MetadataAutoSave, request.IsEnabled);
        await _settingsService.TrySaveAsync(cancellationToken);
        return new UpdateVideoMetadataAutoSaveResponse();
    }
}