using CaptureTool.Domain.Edit.Interfaces.ChromaKey;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

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
        set => Set(SelectedColorOptionIndexProperty, value);
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

    public event EventHandler<int>? SelectedColorOptionIndexChanged;
    public event EventHandler<int>? ToleranceChanged;
    public event EventHandler<int>? DesaturationChanged;

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

    private void UpdateSelectedColorOptionIndex(int value)
    {
        if (SelectedColorOptionIndex == value)
        {
            return;
        }

        SelectedColorOptionIndex = value;
        SelectedColorOptionIndexChanged?.Invoke(this, value);
        UpdateSliderEnablement();
    }

    private void UpdateTolerance(int value)
    {
        if (Tolerance == value)
        {
            return;
        }

        Tolerance = value;
        ToleranceChanged?.Invoke(this, value);
    }

    private void UpdateDesaturation(int value)
    {
        if (Desaturation == value)
        {
            return;
        }

        Desaturation = value;
        DesaturationChanged?.Invoke(this, value);
    }

    private void KeyColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            UpdateSelectedColorOptionIndex(comboBox.SelectedIndex);
        }
    }

    private void ToleranceNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (sender is NumberBox numberBox)
        {
            UpdateTolerance((int)numberBox.Value);
        }
    }

    private void DesaturationNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (sender is NumberBox numberBox)
        {
            UpdateDesaturation((int)numberBox.Value);
        }
    }
}
