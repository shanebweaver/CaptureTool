using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Windowing;
using Windows.Storage;
using Windows.System;

namespace CaptureTool.Infrastructure.Windows.Media;

public sealed class WindowsExternalMediaEditorLauncher : IExternalMediaEditorLauncher
{
    private const string PaintPackageFamilyName = "Microsoft.Paint_8wekyb3d8bbwe";
    private const string ClipchampPackageFamilyName = "Clipchamp.Clipchamp_yxz26nhyzhsrt";

    private readonly IWindowHandleProvider _windowHandleProvider;

    public WindowsExternalMediaEditorLauncher(IWindowHandleProvider windowHandleProvider)
    {
        _windowHandleProvider = windowHandleProvider;
    }

    public async Task<bool> TryOpenFileAsync(
        string filePath,
        ExternalMediaEditor editor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) ||
            !File.Exists(filePath) ||
            cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            var options = new LauncherOptions
            {
                TargetApplicationPackageFamilyName = GetPackageFamilyName(editor)
            };

            nint hwnd = _windowHandleProvider.GetMainWindowHandle();
            if (hwnd != 0)
            {
                WinRT.Interop.InitializeWithWindow.Initialize(options, hwnd);
            }

            return await Launcher.LaunchFileAsync(file, options);
        }
        catch
        {
            return false;
        }
    }

    private static string GetPackageFamilyName(ExternalMediaEditor editor)
    {
        return editor switch
        {
            ExternalMediaEditor.Paint => PaintPackageFamilyName,
            ExternalMediaEditor.Clipchamp => ClipchampPackageFamilyName,
            _ => throw new InvalidOperationException("Unexpected external media editor value.")
        };
    }
}
