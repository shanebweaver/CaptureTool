using CaptureTool.Infrastructure.Abstractions.Audio;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections;
using System.Windows.Input;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class AudioInputSelector : UserControlBase
{
    public static readonly DependencyProperty AudioInputSourcesProperty = DependencyProperty.Register(
        nameof(AudioInputSources),
        typeof(IEnumerable),
        typeof(AudioInputSelector),
        new PropertyMetadata(Array.Empty<AudioInputSource>()));

    public static readonly DependencyProperty SelectedAudioInputSourceProperty = DependencyProperty.Register(
        nameof(SelectedAudioInputSource),
        typeof(AudioInputSource),
        typeof(AudioInputSelector),
        new PropertyMetadata(null));

    public static readonly DependencyProperty IsAvailableProperty = DependencyProperty.Register(
        nameof(IsAvailable),
        typeof(bool),
        typeof(AudioInputSelector),
        new PropertyMetadata(false));

    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText),
        typeof(string),
        typeof(AudioInputSelector),
        new PropertyMetadata(string.Empty, OnStatusTextChanged));

    public static readonly DependencyProperty SelectionChangedCommandProperty = DependencyProperty.Register(
        nameof(SelectionChangedCommand),
        typeof(ICommand),
        typeof(AudioInputSelector),
        new PropertyMetadata(null));

    public AudioInputSelector()
    {
        InitializeComponent();
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

    public bool IsAvailable
    {
        get => Get<bool>(IsAvailableProperty);
        set => Set(IsAvailableProperty, value);
    }

    public string StatusText
    {
        get => Get<string>(StatusTextProperty);
        set => Set(StatusTextProperty, value);
    }

    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    public ICommand SelectionChangedCommand
    {
        get => Get<ICommand>(SelectionChangedCommandProperty);
        set => Set(SelectionChangedCommandProperty, value);
    }

    private static void OnStatusTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioInputSelector selector)
        {
            selector.RaisePropertyChanged(nameof(HasStatusText));
        }
    }

    private void AudioInputComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is AudioInputSource source &&
            SelectionChangedCommand?.CanExecute(source) == true)
        {
            SelectionChangedCommand.Execute(source);
        }
    }
}
