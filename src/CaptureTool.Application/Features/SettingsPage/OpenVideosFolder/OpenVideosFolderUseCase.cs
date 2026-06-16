using CaptureTool.Application.Abstractions.Features.Settings.OpenVideosFolder;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.Settings;
using System.Diagnostics;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.OpenVideosFolder;

public sealed class OpenVideosFolderUseCase : IOpenVideosFolderUseCase
{
    private const string ActivityId = "OpenVideosFolder";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;

    public OpenVideosFolderUseCase(ISettingsService settingsService, IStorageService storageService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _settingsService = settingsService;
        _storageService = storageService;
    }

    public bool CanExecute(OpenVideosFolderRequest request) => true;

    public Task<UseCaseResponse<OpenVideosFolderResponse>> ExecuteAsync(OpenVideosFolderRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                var path = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder);
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = _storageService.GetSystemDefaultVideosFolderPath();
                }

                if (Directory.Exists(path))
                {
                    Process.Start("explorer.exe", $"/open, {path}");
                }
                else
                {
                    return new OpenVideosFolderResponse(false);
                }

                return new OpenVideosFolderResponse();
            },
            cancellationToken: cancellationToken);
    }
}
