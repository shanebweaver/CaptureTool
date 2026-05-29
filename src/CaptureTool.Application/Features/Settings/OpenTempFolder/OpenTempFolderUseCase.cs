using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Storage;
using System.Diagnostics;

namespace CaptureTool.Application.Features.Settings.OpenTempFolder;

public sealed class OpenTempFolderUseCase : IUseCase<OpenTempFolderRequest, OpenTempFolderResponse>, IConditional<OpenTempFolderRequest>
{
    private readonly IStorageService _storageService;

    public OpenTempFolderUseCase(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public Task<bool> CanExecuteAsync(OpenTempFolderRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<OpenTempFolderResponse> ExecuteAsync(OpenTempFolderRequest request, CancellationToken cancellationToken = default)
    {
        var tempFolderPath = _storageService.GetApplicationTemporaryFolderPath();
        if (Directory.Exists(tempFolderPath))
        {
            Process.Start("explorer.exe", $"/open, {tempFolderPath}");
        }
        else
        {
            throw new DirectoryNotFoundException($"The temporary folder path '{tempFolderPath}' does not exist.");
        }

        return Task.FromResult(new OpenTempFolderResponse());
    }
}