using Microsoft.UI.Xaml;
using System.Windows.Input;

namespace CaptureTool.UI.Windows.Xaml.Controls;

public sealed partial class CaptureOverlayToolbar : UserControlBase
{
    public static readonly DependencyProperty IsDesktopAudioEnabledProperty = DependencyProperty.Register(
        nameof(IsDesktopAudioEnabled),
        typeof(bool),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty IsRecordingProperty = DependencyProperty.Register(
           nameof(IsRecording),
           typeof(bool),
           typeof(CaptureOverlayToolbar),
           new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(
        nameof(CloseCommand),
        typeof(ICommand),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty GoBackCommandProperty = DependencyProperty.Register(
        nameof(GoBackCommand),
        typeof(ICommand),
        typeof(SelectionOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty StartVideoCaptureCommandProperty = DependencyProperty.Register(
        nameof(StartVideoCaptureCommand),
        typeof(ICommand),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty StopVideoCaptureCommandProperty = DependencyProperty.Register(
        nameof(StopVideoCaptureCommand),
        typeof(ICommand),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty ToggleDesktopAudioCommandProperty = DependencyProperty.Register(
        nameof(ToggleDesktopAudioCommand),
        typeof(ICommand),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public bool IsDesktopAudioEnabled
    {
        get => Get<bool>(IsDesktopAudioEnabledProperty);
        set => Set(IsDesktopAudioEnabledProperty, value);
    }

    public bool IsRecording
    {
        get => Get<bool>(IsRecordingProperty);
        set => Set(IsRecordingProperty, value);
    }

    public ICommand CloseCommand
    {
        get => Get<ICommand>(CloseCommandProperty);
        set => Set(CloseCommandProperty, value);
    }

    public ICommand GoBackCommand
    {
        get => Get<ICommand>(GoBackCommandProperty);
        set => Set(GoBackCommandProperty, value);
    }

    public ICommand StartVideoCaptureCommand
    {
        get => Get<ICommand>(StartVideoCaptureCommandProperty);
        set => Set(StartVideoCaptureCommandProperty, value);
    }

    public ICommand StopVideoCaptureCommand
    {
        get => Get<ICommand>(StopVideoCaptureCommandProperty);
        set => Set(StopVideoCaptureCommandProperty, value);
    }

    public ICommand ToggleDesktopAudioCommand
    {
        get => Get<ICommand>(ToggleDesktopAudioCommandProperty);
        set => Set(ToggleDesktopAudioCommandProperty, value);
    }

    public CaptureOverlayToolbar()
    {
        InitializeComponent();
    }
}
