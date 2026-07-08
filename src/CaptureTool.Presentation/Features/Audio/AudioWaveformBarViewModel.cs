using CaptureTool.Presentation.ViewModels;

namespace CaptureTool.Presentation.Features.Audio;

public sealed partial class AudioWaveformBarViewModel : ViewModelBase
{
    public double Height
    {
        get;
        set => Set(ref field, value);
    }

    public AudioWaveformBarViewModel(double height)
    {
        Height = height;
    }
}
