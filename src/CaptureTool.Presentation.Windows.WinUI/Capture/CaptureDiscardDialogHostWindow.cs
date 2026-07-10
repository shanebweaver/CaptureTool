using CaptureTool.Presentation.Windows.WinUI.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Drawing;

namespace CaptureTool.Presentation.Windows.WinUI.Capture;

internal sealed class CaptureDiscardDialogHostWindow : Window
{
    private const double HostWidth = 560;
    private const double HostHeight = 280;

    private readonly Rectangle? _windowBounds;

    private readonly Grid _root = new()
    {
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
    };

    private readonly TaskCompletionSource _loadedCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CaptureDiscardDialogHostWindow(Rectangle? windowBounds = null)
    {
        _windowBounds = windowBounds;
        Content = _root;
        _root.Loaded += Root_Loaded;
    }

    public async Task<ContentDialogResult> ShowContentDialogAsync(ContentDialog dialog)
    {
        ConfigureWindow();
        Activate();
        this.SetForegroundWindow();

        await _loadedCompletion.Task;

        dialog.XamlRoot = _root.XamlRoot;

        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            CloseHost();
        }
    }

    private void ConfigureWindow()
    {
        this.MakeBorderlessToolWindow();
        this.ExcludeFromScreenCapture();

        if (_windowBounds is { } windowBounds)
        {
            this.MoveAndResize(windowBounds);
        }
        else
        {
            this.CenterOnScreen(HostWidth, HostHeight);
        }
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        _root.Loaded -= Root_Loaded;
        _loadedCompletion.TrySetResult();
    }

    private void CloseHost()
    {
        try
        {
            Close();
        }
        catch
        {
        }
    }
}
