using CaptureTool.Presentation.ViewModels;

namespace CaptureTool.Presentation.Features.Audio;

public sealed partial class AudioWaveformBarViewModel : ViewModelBase
{
    public const double DefaultWidth = 5;

    public double Height
    {
        get;
        set => Set(ref field, value);
    }

    public double Width
    {
        get;
        set => Set(ref field, value);
    }

    public double Level
    {
        get;
        set => Set(ref field, value);
    }

    public AudioWaveformBarViewModel(double height, double width = DefaultWidth, double level = 0)
    {
        Height = height;
        Width = width;
        Level = level;
    }
}
