using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.Features.Settings.ChangeVideosFolder;

public sealed class ChangeVideosFolderUseCase : IUseCase<ChangeVideosFolderRequest, ChangeVideosFolderResponse>, IConditional<ChangeVideosFolderRequest>
{
    private readonly IWindowHandleProvider _windowing;
    private readonly IFilePickerService _picker;
    private readonly ISettingsService _settings;

    public ChangeVideosFolderUseCase(IWindowHandleProvider windowing, IFilePickerService picker, ISettingsService settings)
    {
        _windowing = windowing;
        _picker = picker;
        _settings = settings;
    }

    public Task<bool> CanExecuteAsync(ChangeVideosFolderRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public async Task<ChangeVideosFolderResponse> ExecuteAsync(ChangeVideosFolderRequest request, CancellationToken cancellationToken = default)
    {
        var hwnd = _windowing.GetMainWindowHandle();
        var folder = await _picker.PickFolderAsync(hwnd, UserFolder.Videos) ?? throw new OperationCanceledException("No folder was selected.");
        _settings.Set(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder, folder.FolderPath);
        await _settings.TrySaveAsync(cancellationToken);
        return new ChangeVideosFolderResponse();
    }
}