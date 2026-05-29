using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.Features.AppMenu.OpenFile;

public sealed class OpenFileUseCase : IUseCase<OpenFileRequest, OpenFileResponse>
{
    private readonly IFilePickerService _filePickerService;
    private readonly INavigationService _navigationService;
    private readonly IWindowHandleProvider _windowHandleProvider;

    public OpenFileUseCase(
        IFilePickerService filePickerService,
        INavigationService navigationService,
        IWindowHandleProvider windowHandleProvider)
    {
        _filePickerService = filePickerService;
        _navigationService = navigationService;
        _windowHandleProvider = windowHandleProvider;
    }

    public async Task<OpenFileResponse> ExecuteAsync(OpenFileRequest request, CancellationToken cancellationToken = default)
    {
        nint hwnd = _windowHandleProvider.GetMainWindowHandle();
        IFile file = await _filePickerService.PickFileAsync(hwnd, FilePickerType.Image, UserFolder.Pictures)
            ?? throw new OperationCanceledException("No file was selected.");
        cancellationToken.ThrowIfCancellationRequested();
        _navigationService.Navigate(NavigationRoute.ImageEdit, new ImageFile(file.FilePath));
        return new OpenFileResponse();
    }
}
