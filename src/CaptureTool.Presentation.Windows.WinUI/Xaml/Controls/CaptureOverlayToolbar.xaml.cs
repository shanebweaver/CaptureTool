using CaptureTool.Application.Abstractions.Audio;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Collections;
using System.Windows.Input;
using Windows.Foundation;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class CaptureOverlayToolbar : UserControlBase
{
    private static readonly TimeSpan LocalAudioVolumeCommitDelay = TimeSpan.FromMilliseconds(150);

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

    public static readonly DependencyProperty LocalAudioVolumePercentageProperty = DependencyProperty.Register(
        nameof(LocalAudioVolumePercentage),
        typeof(int),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(100, OnLocalAudioVolumePercentageChanged));

    public static readonly DependencyProperty IsStartingProperty = DependencyProperty.Register(
        nameof(IsStarting),
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

    public static readonly DependencyProperty SetLocalAudioVolumeCommandProperty = DependencyProperty.Register(
        nameof(SetLocalAudioVolumeCommand),
        typeof(ICommand),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(null));

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
        new PropertyMetadata(Array.Empty<AudioInputSource>(), OnAudioInputBindablePropertyChanged));

    public static readonly DependencyProperty SelectedAudioInputSourceProperty = DependencyProperty.Register(
        nameof(SelectedAudioInputSource),
        typeof(AudioInputSource),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(null, OnAudioInputBindablePropertyChanged));

    public static readonly DependencyProperty SelectedAudioInputSourceIndexProperty = DependencyProperty.Register(
        nameof(SelectedAudioInputSourceIndex),
        typeof(int),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(-1, OnAudioInputBindablePropertyChanged));

    public static readonly DependencyProperty IsAudioInputSelectionAvailableProperty = DependencyProperty.Register(
        nameof(IsAudioInputSelectionAvailable),
        typeof(bool),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(false, OnAudioInputBindablePropertyChanged));

    public static readonly DependencyProperty IsAudioInputMutedProperty = DependencyProperty.Register(
        nameof(IsAudioInputMuted),
        typeof(bool),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(false, OnAudioInputBindablePropertyChanged));

    public static readonly DependencyProperty SelectAudioInputSourceCommandProperty = DependencyProperty.Register(
        nameof(SelectAudioInputSourceCommand),
        typeof(ICommand),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ToggleAudioInputMuteCommandProperty = DependencyProperty.Register(
        nameof(ToggleAudioInputMuteCommand),
        typeof(ICommand),
        typeof(CaptureOverlayToolbar),
        new PropertyMetadata(null, OnAudioInputBindablePropertyChanged));

    private readonly DispatcherQueueTimer _localAudioVolumeCommitTimer;
    private int _pendingLocalAudioVolumePercentage = 100;

    public CaptureOverlayToolbar()
    {
        InitializeComponent();
        _localAudioVolumeCommitTimer = DispatcherQueue.CreateTimer();
        _localAudioVolumeCommitTimer.Interval = LocalAudioVolumeCommitDelay;
        _localAudioVolumeCommitTimer.IsRepeating = false;
        _localAudioVolumeCommitTimer.Tick += LocalAudioVolumeCommitTimer_Tick;
        Unloaded += CaptureOverlayToolbar_Unloaded;
    }

    public bool IsRunning => IsRecording && !IsPaused;
    public bool CanStartRecording => !IsStarting;

    public bool IsLocalAudioEnabled
    {
        get => Get<bool>(IsLocalAudioEnabledProperty);
        set => Set(IsLocalAudioEnabledProperty, value);
    }

    public int LocalAudioVolumePercentage
    {
        get => Get<int>(LocalAudioVolumePercentageProperty);
        set => Set(LocalAudioVolumePercentageProperty, Math.Clamp(value, 0, 100));
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

    public bool IsStarting
    {
        get => Get<bool>(IsStartingProperty);
        set
        {
            Set(IsStartingProperty, value);
            RaisePropertyChanged(nameof(CanStartRecording));
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

    public ICommand SetLocalAudioVolumeCommand
    {
        get => Get<ICommand>(SetLocalAudioVolumeCommandProperty);
        set => Set(SetLocalAudioVolumeCommandProperty, value);
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

    public int SelectedAudioInputSourceIndex
    {
        get => Get<int>(SelectedAudioInputSourceIndexProperty);
        set => Set(SelectedAudioInputSourceIndexProperty, value);
    }

    public bool IsAudioInputSelectionAvailable
    {
        get => Get<bool>(IsAudioInputSelectionAvailableProperty);
        set => Set(IsAudioInputSelectionAvailableProperty, value);
    }

    public bool IsAudioInputMuted
    {
        get => Get<bool>(IsAudioInputMutedProperty);
        set => Set(IsAudioInputMutedProperty, value);
    }

    public ICommand SelectAudioInputSourceCommand
    {
        get => Get<ICommand>(SelectAudioInputSourceCommandProperty);
        set => Set(SelectAudioInputSourceCommandProperty, value);
    }

    public ICommand ToggleAudioInputMuteCommand
    {
        get => Get<ICommand>(ToggleAudioInputMuteCommandProperty);
        set => Set(ToggleAudioInputMuteCommandProperty, value);
    }

    public string FormatVolumePercentage(int volumePercentage)
        => $"{Math.Clamp(volumePercentage, 0, 100)}%";

    internal Size MeasureNaturalSize()
    {
        ToolbarPanel.InvalidateMeasure();
        ToolbarPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return ToolbarPanel.DesiredSize;
    }

    private void LocalAudioToggleButton_Click(SplitButton sender, SplitButtonClickEventArgs args)
    {
        if (ToggleLocalAudioCommand?.CanExecute(null) == true)
        {
            ToggleLocalAudioCommand.Execute(null);
        }
    }

    private void LocalAudioVolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        int volumePercentage = Math.Clamp((int)Math.Round(args.NewValue), 0, 100);
        if (volumePercentage == LocalAudioVolumePercentage)
        {
            return;
        }

        // Keep the slider and percentage label responsive while the debounced
        // command is pending. The view model re-applies committed state on failure.
        LocalAudioVolumePercentage = volumePercentage;
        _pendingLocalAudioVolumePercentage = volumePercentage;
        _localAudioVolumeCommitTimer.Stop();
        _localAudioVolumeCommitTimer.Start();
    }

    private void LocalAudioVolumeCommitTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        int volumePercentage = _pendingLocalAudioVolumePercentage;
        if (SetLocalAudioVolumeCommand is null)
        {
            return;
        }

        if (SetLocalAudioVolumeCommand.CanExecute(volumePercentage))
        {
            SetLocalAudioVolumeCommand.Execute(volumePercentage);
            return;
        }

        _localAudioVolumeCommitTimer.Start();
    }

    private void CaptureOverlayToolbar_Unloaded(object sender, RoutedEventArgs e)
    {
        _localAudioVolumeCommitTimer.Stop();
    }

    private static void OnLocalAudioVolumePercentageChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is CaptureOverlayToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(nameof(LocalAudioVolumePercentage));
        }
    }

    private static void OnAudioInputBindablePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CaptureOverlayToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(GetPropertyName(e.Property));
        }
    }

    private static string GetPropertyName(DependencyProperty property)
    {
        if (property == AudioInputSourcesProperty)
        {
            return nameof(AudioInputSources);
        }

        if (property == SelectedAudioInputSourceProperty)
        {
            return nameof(SelectedAudioInputSource);
        }

        if (property == SelectedAudioInputSourceIndexProperty)
        {
            return nameof(SelectedAudioInputSourceIndex);
        }

        if (property == IsAudioInputSelectionAvailableProperty)
        {
            return nameof(IsAudioInputSelectionAvailable);
        }

        if (property == IsAudioInputMutedProperty)
        {
            return nameof(IsAudioInputMuted);
        }

        if (property == ToggleAudioInputMuteCommandProperty)
        {
            return nameof(ToggleAudioInputMuteCommand);
        }

        return string.Empty;
    }
}
