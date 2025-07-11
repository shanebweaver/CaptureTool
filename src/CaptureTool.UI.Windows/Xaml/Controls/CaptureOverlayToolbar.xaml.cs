using CaptureTool.Capture;
using Microsoft.UI.Xaml;
using System;

namespace CaptureTool.UI.Windows.Xaml.Controls;

public sealed partial class CaptureOverlayToolbar : UserControlBase
{
    private static readonly DependencyProperty SelectedCaptureModeProperty = DependencyProperty.Register(
        nameof(SelectedCaptureMode),
        typeof(CaptureMode),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(CaptureMode.Image));

    public CaptureMode SelectedCaptureMode
    {
        get => Get< CaptureMode>(SelectedCaptureModeProperty);
        set => Set(SelectedCaptureModeProperty, value);
    }

    public event EventHandler? CloseRequested;

    public CaptureOverlayToolbar()
    {
        InitializeComponent();
    }
}
