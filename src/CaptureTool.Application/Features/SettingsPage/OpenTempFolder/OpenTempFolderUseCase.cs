using CaptureTool.Application.Abstractions.Features.Settings.OpenTempFolder;
using CaptureTool.Application.Abstractions.Storage;
using System.Diagnostics;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.OpenTempFolder;

public sealed class OpenTempFolderUseCase : IOpenTempFolderUseCase
{
    private const string ActivityId = "OpenTempFolder";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IStorageService _storageService;

    public OpenTempFolderUseCase(IStorageService storageService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _storageService = storageService;
    }

    public bool CanExecute(OpenTempFolderRequest request) => true;

    public Task<UseCaseResponse<OpenTempFolderResponse>> ExecuteAsync(OpenTempFolderRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                var tempFolderPath = _storageService.GetApplicationTemporaryFolderPath();
                if (Directory.Exists(tempFolderPath))
                {
                    Process.Start("explorer.exe", $"/open, {tempFolderPath}");
                }
                else
                {
                    return new OpenTempFolderResponse(false);
                }

                return new OpenTempFolderResponse();
            },
            cancellationToken: cancellationToken);
    }
}
