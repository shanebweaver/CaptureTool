using Microsoft.UI.Xaml;
using CaptureTool.Infrastructure.Abstractions.Audio;
using System.Collections;
using System.Windows.Input;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class CaptureOverlayToolbar : UserControlBase
{
    public static readonly DependencyProperty IsLocalAudioEnabledProperty = DependencyProperty.Register(
        nameof(IsLocalAudioEnabled),
        typeof(bool),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty IsRecordingProperty = DependencyProperty.Register(
        nameof(IsRecording),
        typeof(bool),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty IsPausedProperty = DependencyProperty.Register(
        nameof(IsPaused),
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

    public static readonly DependencyProperty ToggleLocalAudioCommandProperty = DependencyProperty.Register(
        nameof(ToggleLocalAudioCommand),
        typeof(ICommand),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty TogglePauseResumeCommandProperty = DependencyProperty.Register(
        nameof(TogglePauseResumeCommand),
        typeof(ICommand),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty CaptureTimeProperty = DependencyProperty.Register(
        nameof(CaptureTime),
        typeof(TimeSpan),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(TimeSpan.Zero));

    public static readonly DependencyProperty AudioInputSourcesProperty = DependencyProperty.Register(
        nameof(AudioInputSources),
        typeof(IEnumerable),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(Array.Empty<AudioInputSource>()));

    public static readonly DependencyProperty SelectedAudioInputSourceProperty = DependencyProperty.Register(
        nameof(SelectedAudioInputSource),
        typeof(AudioInputSource),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(null));

    public static readonly DependencyProperty IsAudioInputSelectionAvailableProperty = DependencyProperty.Register(
        nameof(IsAudioInputSelectionAvailable),
        typeof(bool),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(false));

    public static readonly DependencyProperty AudioInputSelectionStatusProperty = DependencyProperty.Register(
        nameof(AudioInputSelectionStatus),
        typeof(string),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SelectAudioInputSourceCommandProperty = DependencyProperty.Register(
        nameof(SelectAudioInputSourceCommand),
        typeof(ICommand),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(null));

    public CaptureOverlayToolbar()
    {
        InitializeComponent();
    }

    public bool IsRunning => IsRecording && !IsPaused;

    public bool IsLocalAudioEnabled
    {
        get => Get<bool>(IsLocalAudioEnabledProperty);
        set => Set(IsLocalAudioEnabledProperty, value);
    }

    public bool IsRecording
    {
        get => Get<bool>(IsRecordingProperty);
        set
        {
            Set(IsRecordingProperty, value);
            RaisePropertyChanged(nameof(IsRunning));
        }
    }

    public bool IsPaused
    {
        get => Get<bool>(IsPausedProperty);
        set
        {
            Set(IsPausedProperty, value);
            RaisePropertyChanged(nameof(IsRunning));
        }
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

    public ICommand ToggleLocalAudioCommand
    {
        get => Get<ICommand>(ToggleLocalAudioCommandProperty);
        set => Set(ToggleLocalAudioCommandProperty, value);
    }

    public ICommand TogglePauseResumeCommand
    {
        get => Get<ICommand>(TogglePauseResumeCommandProperty);
        set => Set(TogglePauseResumeCommandProperty, value);
    }

    public TimeSpan CaptureTime
    {
        get => Get<TimeSpan>(CaptureTimeProperty);
        set => Set(CaptureTimeProperty, value);
    }

    public IEnumerable AudioInputSources
    {
        get => Get<IEnumerable>(AudioInputSourcesProperty);
        set => Set(AudioInputSourcesProperty, value);
    }

    public AudioInputSource? SelectedAudioInputSource
    {
        get => Get<AudioInputSource?>(SelectedAudioInputSourceProperty);
        set => Set(SelectedAudioInputSourceProperty, value);
    }

    public bool IsAudioInputSelectionAvailable
    {
        get => Get<bool>(IsAudioInputSelectionAvailableProperty);
        set => Set(IsAudioInputSelectionAvailableProperty, value);
    }

    public string AudioInputSelectionStatus
    {
        get => Get<string>(AudioInputSelectionStatusProperty);
        set => Set(AudioInputSelectionStatusProperty, value);
    }

    public ICommand SelectAudioInputSourceCommand
    {
        get => Get<ICommand>(SelectAudioInputSourceCommandProperty);
        set => Set(SelectAudioInputSourceCommandProperty, value);
    }
}
