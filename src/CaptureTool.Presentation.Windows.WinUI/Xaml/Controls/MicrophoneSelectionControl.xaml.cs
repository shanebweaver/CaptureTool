using CaptureTool.Domain.Audio.Interfaces;
using Microsoft.UI.Xaml;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class MicrophoneSelectionControl : UserControlBase
{
    private static readonly AudioInputDevice NoneDevice = new("", "None");

    public static readonly DependencyProperty AvailableMicrophonesProperty = DependencyProperty.Register(
        nameof(AvailableMicrophones),
        typeof(IReadOnlyList<AudioInputDevice>),
        typeof(MicrophoneSelectionControl),
        new PropertyMetadata(Array.Empty<AudioInputDevice>(), OnAvailableMicrophonesChanged));

    public static readonly DependencyProperty SelectedMicrophoneProperty = DependencyProperty.Register(
        nameof(SelectedMicrophone),
        typeof(AudioInputDevice),
        typeof(MicrophoneSelectionControl),
        new PropertyMetadata(null));

    public MicrophoneSelectionControl()
    {
        InitializeComponent();
        UpdateMicrophonesWithNone();
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

    public IReadOnlyList<AudioInputDevice> MicrophonesWithNone
    {
        get => field ?? Array.Empty<AudioInputDevice>();
        private set => Set(ref field, value);
    }

    private static void OnAvailableMicrophonesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MicrophoneSelectionControl control)
        {
            control.UpdateMicrophonesWithNone();
        }
    }

    private void UpdateMicrophonesWithNone()
    {
        var microphones = AvailableMicrophones ?? Array.Empty<AudioInputDevice>();
        var list = new List<AudioInputDevice>(microphones.Count + 1) { NoneDevice };
        list.AddRange(microphones);
        MicrophonesWithNone = list;
    }
}
