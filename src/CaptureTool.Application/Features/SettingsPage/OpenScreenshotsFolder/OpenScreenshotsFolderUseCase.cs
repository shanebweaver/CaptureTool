using CaptureTool.Application.Abstractions.Features.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.Settings;
using System.Diagnostics;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.OpenScreenshotsFolder;

public sealed class OpenScreenshotsFolderUseCase : IOpenScreenshotsFolderUseCase
{
    private const string ActivityId = "OpenScreenshotsFolder";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;

    public OpenScreenshotsFolderUseCase(ISettingsService settingsService, IStorageService storageService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _settingsService = settingsService;
        _storageService = storageService;
    }

    public bool CanExecute(OpenScreenshotsFolderRequest request) => true;

    public Task<UseCaseResponse<OpenScreenshotsFolderResponse>> ExecuteAsync(OpenScreenshotsFolderRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                var path = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder);
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = _storageService.GetSystemDefaultScreenshotsFolderPath();
                }

                if (Directory.Exists(path))
                {
                    Process.Start("explorer.exe", $"/open, {path}");
                }
                else
                {
                    return new OpenScreenshotsFolderResponse(false);
                }

                return new OpenScreenshotsFolderResponse();
            },
            cancellationToken: cancellationToken);
    }
}
