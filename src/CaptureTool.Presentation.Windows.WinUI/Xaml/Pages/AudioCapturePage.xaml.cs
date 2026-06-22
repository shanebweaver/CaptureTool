using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;

public sealed partial class AudioCapturePage : AudioCapturePageBase
{
    public AudioCapturePage()
    {
        InitializeComponent();
    }

    private Symbol GetPauseResumeSymbol(bool isPaused)
    {
        return isPaused ? Symbol.Play : Symbol.Pause;
    }
}
