using CaptureTool.Domain.Audio.Interfaces;
using Microsoft.UI.Xaml;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class MicrophoneSelectionControl : UserControlBase
{
    public static readonly DependencyProperty AvailableMicrophonesProperty = DependencyProperty.Register(
        nameof(AvailableMicrophones),
        typeof(IReadOnlyList<AudioInputDevice>),
        typeof(MicrophoneSelectionControl),
        new PropertyMetadata(Array.Empty<AudioInputDevice>()));

    public static readonly DependencyProperty SelectedMicrophoneProperty = DependencyProperty.Register(
        nameof(SelectedMicrophone),
        typeof(AudioInputDevice),
        typeof(MicrophoneSelectionControl),
        new PropertyMetadata(null));

    public MicrophoneSelectionControl()
    {
        InitializeComponent();
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
