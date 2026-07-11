using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Drawing;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class ColorPickerToolbar : UserControlBase
{
    public static readonly DependencyProperty SelectedColorTypeIndexProperty = DependencyProperty.Register(
        nameof(SelectedColorTypeIndex),
        typeof(int),
        typeof(ColorPickerToolbar),
        new PropertyMetadata(0, OnSelectedColorTypeIndexChanged));

    public static readonly DependencyProperty PickedColorProperty = DependencyProperty.Register(
        nameof(PickedColor),
        typeof(Color),
        typeof(ColorPickerToolbar),
        new PropertyMetadata(Color.Empty, OnPickedColorChanged));

    public static readonly DependencyProperty PickedColorValueProperty = DependencyProperty.Register(
        nameof(PickedColorValue),
        typeof(string),
        typeof(ColorPickerToolbar),
        new PropertyMetadata(string.Empty, OnPickedColorValueChanged));

    public int SelectedColorTypeIndex
    {
        get => Get<int>(SelectedColorTypeIndexProperty);
        set => Set(SelectedColorTypeIndexProperty, value);
    }

    public Color PickedColor
    {
        get => Get<Color>(PickedColorProperty);
        set => Set(PickedColorProperty, value);
    }

    public string PickedColorValue
    {
        get => Get<string>(PickedColorValueProperty);
        set => Set(PickedColorValueProperty, value);
    }

    public event EventHandler<int>? SelectedColorTypeIndexChanged;

    public ColorPickerToolbar()
    {
        InitializeComponent();
    }

    private static void OnSelectedColorTypeIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(nameof(SelectedColorTypeIndex));
        }
    }

    private static void OnPickedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(nameof(PickedColor));
        }
    }

    private static void OnPickedColorValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(nameof(PickedColorValue));
        }
    }

    private void UpdateSelectedColorTypeIndex(int value)
    {
        if (SelectedColorTypeIndex == value)
        {
            return;
        }

        SelectedColorTypeIndex = value;
        SelectedColorTypeIndexChanged?.Invoke(this, value);
    }

    private void ColorTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            UpdateSelectedColorTypeIndex(comboBox.SelectedIndex);
        }
    }
}
