using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Domain.Audio.Interfaces;
using Microsoft.UI.Xaml;
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

    public static readonly DependencyProperty AvailableMicrophonesProperty = DependencyProperty.Register(
        nameof(AvailableMicrophones),
        typeof(IReadOnlyList<AudioInputDevice>),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(Array.Empty<AudioInputDevice>()));

    public static readonly DependencyProperty SelectedMicrophoneProperty = DependencyProperty.Register(
        nameof(SelectedMicrophone),
        typeof(AudioInputDevice),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(null));

    public CaptureOverlayToolbar()
    {
        InitializeComponent();

        if (AppServiceLocator.FeatureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture_LocalAudio))
        {
            LocalAudioToggleButton.Visibility = Visibility.Visible;
        }

        if (AppServiceLocator.FeatureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture_MicrophoneSelection))
        {
            MicrophoneSelection.Visibility = Visibility.Visible;
        }
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

    public IReadOnlyList<AudioInputDevice> AvailableMicrophones
    {
        get => Get<IReadOnlyList<AudioInputDevice>>(AvailableMicrophonesProperty);
        set => Set(AvailableMicrophonesProperty, value);
    }

    public AudioInputDevice? SelectedMicrophone
    {
        get => Get<AudioInputDevice>(SelectedMicrophoneProperty);
        set => Set(SelectedMicrophoneProperty, value);
    }
}
