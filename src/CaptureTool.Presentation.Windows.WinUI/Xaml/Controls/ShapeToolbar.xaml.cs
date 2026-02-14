using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Drawing;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class ShapeToolbar : UserControlBase
{
    public static readonly DependencyProperty SelectedShapeTypeIndexProperty = DependencyProperty.Register(
        nameof(SelectedShapeTypeIndex),
        typeof(int),
        typeof(ShapeToolbar),
        new PropertyMetadata(0));

    public static readonly DependencyProperty StrokeColorProperty = DependencyProperty.Register(
        nameof(StrokeColor),
        typeof(Color),
        typeof(ShapeToolbar),
        new PropertyMetadata(Color.Black));

    public static readonly DependencyProperty FillColorProperty = DependencyProperty.Register(
        nameof(FillColor),
        typeof(Color),
        typeof(ShapeToolbar),
        new PropertyMetadata(Color.Transparent));

    public static readonly DependencyProperty StrokeWidthProperty = DependencyProperty.Register(
        nameof(StrokeWidth),
        typeof(int),
        typeof(ShapeToolbar),
        new PropertyMetadata(3));

    public int SelectedShapeTypeIndex
    {
        get => Get<int>(SelectedShapeTypeIndexProperty);
        set => Set(SelectedShapeTypeIndexProperty, value);
    }

    public Color StrokeColor
    {
        get => Get<Color>(StrokeColorProperty);
        set => Set(StrokeColorProperty, value);
    }

    public Color FillColor
    {
        get => Get<Color>(FillColorProperty);
        set => Set(FillColorProperty, value);
    }

    public int StrokeWidth
    {
        get => Get<int>(StrokeWidthProperty);
        set => Set(StrokeWidthProperty, value);
    }

    public event EventHandler<int>? SelectedShapeTypeIndexChanged;
    public event EventHandler<Color>? StrokeColorChanged;
    public event EventHandler<Color>? FillColorChanged;
    public event EventHandler<int>? StrokeWidthChanged;

    public ShapeToolbar()
    {
        InitializeComponent();
    }

    private void UpdateSelectedShapeTypeIndex(int value)
    {
        if (SelectedShapeTypeIndex == value)
        {
            return;
        }

        SelectedShapeTypeIndex = value;
        SelectedShapeTypeIndexChanged?.Invoke(this, value);
    }

    private void UpdateStrokeColor(Color value)
    {
        if (StrokeColor.Equals(value))
        {
            return;
        }

        StrokeColor = value;
        StrokeColorChanged?.Invoke(this, value);
    }

    private void UpdateFillColor(Color value)
    {
        if (FillColor.Equals(value))
        {
            return;
        }

        FillColor = value;
        FillColorChanged?.Invoke(this, value);
    }

    private void UpdateStrokeWidth(int value)
    {
        if (StrokeWidth == value)
        {
            return;
        }

        StrokeWidth = value;
        StrokeWidthChanged?.Invoke(this, value);
    }

    private void ShapeTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            UpdateSelectedShapeTypeIndex(comboBox.SelectedIndex);
        }
    }

    private void StrokeColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (sender is ColorPicker colorPicker)
        {
            var color = colorPicker.Color;
            UpdateStrokeColor(Color.FromArgb(color.A, color.R, color.G, color.B));
        }
    }

    private void FillColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (sender is ColorPicker colorPicker)
        {
            var color = colorPicker.Color;
            UpdateFillColor(Color.FromArgb(color.A, color.R, color.G, color.B));
        }
    }

    private void StrokeWidthNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (sender is NumberBox numberBox && !double.IsNaN(numberBox.Value))
        {
            UpdateStrokeWidth((int)numberBox.Value);
        }
    }
}
