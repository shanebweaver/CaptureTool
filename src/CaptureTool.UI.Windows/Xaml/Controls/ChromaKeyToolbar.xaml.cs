using CaptureTool.Domains.Edit.Interfaces.ChromaKey;
using Microsoft.UI.Xaml;

namespace CaptureTool.UI.Windows.Xaml.Controls;

public sealed partial class ChromaKeyToolbar : UserControlBase
{
    public static readonly DependencyProperty ColorOptionsProperty = DependencyProperty.Register(
        nameof(ColorOptions),
        typeof(IEnumerable<ChromaKeyColorOption>),
        typeof(ChromaKeyToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty SelectedColorOptionIndexProperty = DependencyProperty.Register(
        nameof(SelectedColorOptionIndex),
        typeof(int),
        typeof(ChromaKeyToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty ToleranceProperty = DependencyProperty.Register(
        nameof(Tolerance),
        typeof(int),
        typeof(ChromaKeyToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public static readonly DependencyProperty DesaturationProperty = DependencyProperty.Register(
        nameof(Desaturation),
        typeof(int),
        typeof(ChromaKeyToolbar),
        new PropertyMetadata(DependencyProperty.UnsetValue));

    public IEnumerable<ChromaKeyColorOption> ColorOptions
    {
        get => Get<IEnumerable<ChromaKeyColorOption>>(ColorOptionsProperty);
        set
        {
            Set(ColorOptionsProperty, value);
            UpdateSliderEnablement();
        }
    }

    public int SelectedColorOptionIndex
    {
        get => Get<int>(SelectedColorOptionIndexProperty);
        set
        {
            Set(SelectedColorOptionIndexProperty, value);
            UpdateSliderEnablement();
        }
    }

    public int Tolerance
    {
        get => Get<int>(ToleranceProperty);
        set => Set(ToleranceProperty, value);
    }

    public int Desaturation
    {
        get => Get<int>(DesaturationProperty);
        set => Set(DesaturationProperty, value);
    }

    private bool _areEffectOptionsEnabled;
    public bool AreEffectOptionsEnabled
    {
        get => _areEffectOptionsEnabled;
        private set => Set(ref _areEffectOptionsEnabled, value);
    }

    public int[] ToleranceOptions = [.. Enumerable.Range(1, 99)];
    public int[] DesaturationOptions = [.. Enumerable.Range(1, 99)];

    public ChromaKeyToolbar()
    {
        InitializeComponent();
    }

    private void UpdateSliderEnablement()
    {
        AreEffectOptionsEnabled =
            ColorOptions != null &&
            ColorOptions.Any() &&
            ColorOptions.Count() > SelectedColorOptionIndex &&
            SelectedColorOptionIndex >= 0 &&
            !ColorOptions.ElementAt(SelectedColorOptionIndex).IsEmpty;
    }
}
