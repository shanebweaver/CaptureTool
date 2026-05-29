using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.Features.Settings.ChangeScreenshotsFolder;

public sealed class ChangeScreenshotsFolderUseCase : IUseCase<ChangeScreenshotsFolderRequest, ChangeScreenshotsFolderResponse>, IConditional<ChangeScreenshotsFolderRequest>
{
    private readonly IWindowHandleProvider _windowing;
    private readonly IFilePickerService _picker;
    private readonly ISettingsService _settings;

    public ChangeScreenshotsFolderUseCase(IWindowHandleProvider windowing, IFilePickerService picker, ISettingsService settings)
    {
        _windowing = windowing;
        _picker = picker;
        _settings = settings;
    }

    public Task<bool> CanExecuteAsync(ChangeScreenshotsFolderRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public async Task<ChangeScreenshotsFolderResponse> ExecuteAsync(ChangeScreenshotsFolderRequest request, CancellationToken cancellationToken = default)
    {
        var hwnd = _windowing.GetMainWindowHandle();
        var folder = await _picker.PickFolderAsync(hwnd, UserFolder.Pictures) ?? throw new OperationCanceledException("No folder was selected.");
        _settings.Set(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder, folder.FolderPath);
        await _settings.TrySaveAsync(cancellationToken);
        return new ChangeScreenshotsFolderResponse();
    }
}