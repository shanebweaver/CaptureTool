using CaptureTool.Application.Abstractions.AppMenu;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.AppMenu;

internal class OpenFileAsyncAppCommand : IOpenFileAsyncAppCommand
{
    private readonly IFilePickerService _filePickerService;
    private readonly IGoToImageEditAppCommand _goToImageEditAppCommand;
    private readonly IWindowHandleProvider _windowHandleProvider;

    public OpenFileAsyncAppCommand(
        IFilePickerService filePickerService,
        IGoToImageEditAppCommand goToImageEditAppCommand,
        IWindowHandleProvider windowHandleProvider)
    {
        _filePickerService = filePickerService;
        _goToImageEditAppCommand = goToImageEditAppCommand;
        _windowHandleProvider = windowHandleProvider;
    }

    public bool IsExecuting { get; protected set; }

    public event EventHandler? CanExecuteChanged;

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
            _goToImageEditAppCommand.Execute(new ImageFile(file.FilePath));
        }
        finally
        {
            IsExecuting = false;
        }
    }
}
