using CaptureTool.Application.Abstractions.Audio;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections;
using System.Windows.Input;
using Windows.System;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class AudioInputSelector : UserControlBase
{
    public static readonly DependencyProperty AudioInputSourcesProperty = DependencyProperty.Register(
        nameof(AudioInputSources),
        typeof(IEnumerable),
        typeof(AudioInputSelector),
        new PropertyMetadata(Array.Empty<AudioInputSource>(), OnBindablePropertyChanged));

    public static readonly DependencyProperty SelectedAudioInputSourceProperty = DependencyProperty.Register(
        nameof(SelectedAudioInputSource),
        typeof(AudioInputSource),
        typeof(AudioInputSelector),
        new PropertyMetadata(null, OnBindablePropertyChanged));

    public static readonly DependencyProperty SelectedAudioInputSourceIndexProperty = DependencyProperty.Register(
        nameof(SelectedAudioInputSourceIndex),
        typeof(int),
        typeof(AudioInputSelector),
        new PropertyMetadata(-1, OnBindablePropertyChanged));

    public static readonly DependencyProperty IsAvailableProperty = DependencyProperty.Register(
        nameof(IsAvailable),
        typeof(bool),
        typeof(AudioInputSelector),
        new PropertyMetadata(false, OnBindablePropertyChanged));

    public static readonly DependencyProperty IsMutedProperty = DependencyProperty.Register(
        nameof(IsMuted),
        typeof(bool),
        typeof(AudioInputSelector),
        new PropertyMetadata(false, OnBindablePropertyChanged));

    public static readonly DependencyProperty SelectionChangedCommandProperty = DependencyProperty.Register(
        nameof(SelectionChangedCommand),
        typeof(ICommand),
        typeof(AudioInputSelector),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ToggleMuteCommandProperty = DependencyProperty.Register(
        nameof(ToggleMuteCommand),
        typeof(ICommand),
        typeof(AudioInputSelector),
        new PropertyMetadata(null, OnBindablePropertyChanged));

    public AudioInputSelector()
    {
        InitializeComponent();
        AudioInputListView.ItemClick += AudioInputListView_ItemClick;
    }

    private bool _isApplyingSelection;

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

    public bool IsAvailable
    {
        get => Get<bool>(IsAvailableProperty);
        set => Set(IsAvailableProperty, value);
    }

    public bool IsMuted
    {
        get => Get<bool>(IsMutedProperty);
        set => Set(IsMutedProperty, value);
    }

    public ICommand SelectionChangedCommand
    {
        get => Get<ICommand>(SelectionChangedCommandProperty);
        set => Set(SelectionChangedCommandProperty, value);
    }

    public ICommand ToggleMuteCommand
    {
        get => Get<ICommand>(ToggleMuteCommandProperty);
        set => Set(ToggleMuteCommandProperty, value);
    }

    private static void OnBindablePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioInputSelector selector)
        {
            selector.RaisePropertyChanged(GetPropertyName(e.Property));

            if (e.Property == AudioInputSourcesProperty || e.Property == SelectedAudioInputSourceIndexProperty)
            {
                selector.ApplySelectedAudioInputSourceIndex();
            }
        }
    }

    private void AudioInputListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingSelection)
        {
            return;
        }

        if (e.AddedItems.FirstOrDefault() is AudioInputSource source &&
            SelectionChangedCommand?.CanExecute(source) == true)
        {
            SelectedAudioInputSourceIndex = AudioInputListView.SelectedIndex;
            SelectionChangedCommand.Execute(source);
            AudioInputFlyoutButton.Flyout.Hide();
            return;
        }

        if (e.AddedItems.Count == 0 && e.RemovedItems.Count > 0)
        {
            RestoreSelectedAudioInputSourceIfStillAvailable();
        }
    }

    private void AudioInputListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AudioInputSource source && source == SelectedAudioInputSource)
        {
            AudioInputFlyoutButton.Flyout.Hide();
        }
    }

    private async void ChangeDefaultSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("ms-settings:sound"));
    }

    private void ApplySelectedAudioInputSourceIndex()
    {
        _ = DispatcherQueue.TryEnqueue(ApplySelectedAudioInputSourceIndexCore);
    }

    private void ApplySelectedAudioInputSourceIndexCore()
    {
        _isApplyingSelection = true;

        try
        {
            int selectedIndex = SelectedAudioInputSourceIndex;
            AudioInputListView.SelectedIndex = selectedIndex >= 0 && selectedIndex < AudioInputListView.Items.Count
                ? selectedIndex
                : -1;
        }
        finally
        {
            _isApplyingSelection = false;
        }
    }

    private void RestoreSelectedAudioInputSourceIfStillAvailable()
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (_isApplyingSelection || AudioInputListView.SelectedIndex != -1)
            {
                return;
            }

            AudioInputSource? selectedAudioInputSource = SelectedAudioInputSource;

            if (selectedAudioInputSource is null)
            {
                return;
            }

            foreach (object item in AudioInputListView.Items)
            {
                if (item is AudioInputSource source && source == selectedAudioInputSource)
                {
                    _isApplyingSelection = true;

                    try
                    {
                        AudioInputListView.SelectedItem = source;
                    }
                    finally
                    {
                        _isApplyingSelection = false;
                    }

                    return;
                }
            }
        });
    }

    private string GetToolTipText(bool isAvailable, AudioInputSource? selectedAudioInputSource)
    {
        return isAvailable
            ? selectedAudioInputSource?.DisplayName ?? string.Empty
            : "No input device";
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

        if (property == IsAvailableProperty)
        {
            return nameof(IsAvailable);
        }

        if (property == IsMutedProperty)
        {
            return nameof(IsMuted);
        }

        if (property == ToggleMuteCommandProperty)
        {
            return nameof(ToggleMuteCommand);
        }

        return string.Empty;
    }
}
