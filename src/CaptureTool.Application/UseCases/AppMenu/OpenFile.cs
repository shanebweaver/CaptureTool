using CaptureTool.Application.Abstractions.Messaging.Commands;
using CaptureTool.Application.UseCases.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.UseCases.AppMenu;

internal class OpenFile : IAsyncAppCommand
{
    private readonly IFilePickerService _filePickerService;
    private readonly INavigationService _navigationService;
    private readonly IWindowHandleProvider _windowHandleProvider;

    public OpenFile(
        IFilePickerService filePickerService,
        INavigationService navigationService,
        IWindowHandleProvider windowHandleProvider)
    {
        _filePickerService = filePickerService;
        _navigationService = navigationService;
        _windowHandleProvider = windowHandleProvider;
    }

    public bool IsExecuting { get; protected set; }

    public bool CanExecute()
    {
        return !IsExecuting;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        IsExecuting = true;
        
        try
        {
            nint hwnd = _windowHandleProvider.GetMainWindowHandle();
            IFile file = await _filePickerService.PickFileAsync(hwnd, FilePickerType.Image, UserFolder.Pictures)
                ?? throw new OperationCanceledException("No file was selected.");
            cancellationToken.ThrowIfCancellationRequested();
            _navigationService.Navigate(NavigationRoute.ImageEdit, new ImageFile(file.FilePath));
        }
        finally
        {
            IsExecuting = false;
        }
    }
}
