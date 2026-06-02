using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Drawing;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class TextToolbar : UserControlBase
{
    public static readonly DependencyProperty TextFontColorProperty = DependencyProperty.Register(
        nameof(TextFontColor),
        typeof(Color),
        typeof(TextToolbar),
        new PropertyMetadata(Color.Black));

    public static readonly DependencyProperty TextFontColorOptionsProperty = DependencyProperty.Register(
        nameof(TextFontColorOptions),
        typeof(IEnumerable<Color>),
        typeof(TextToolbar),
        new PropertyMetadata(Array.Empty<Color>()));

    public static readonly DependencyProperty TextFontColorOpacityProperty = DependencyProperty.Register(
        nameof(TextFontColorOpacity),
        typeof(int),
        typeof(TextToolbar),
        new PropertyMetadata(100));

    public static readonly DependencyProperty TextFontFamilyProperty = DependencyProperty.Register(
        nameof(TextFontFamily),
        typeof(string),
        typeof(TextToolbar),
        new PropertyMetadata("Segoe UI"));

    public static readonly DependencyProperty TextFontSizeProperty = DependencyProperty.Register(
        nameof(TextFontSize),
        typeof(int),
        typeof(TextToolbar),
        new PropertyMetadata(24));

    public Color TextFontColor
    {
        get => Get<Color>(TextFontColorProperty);
        set => Set(TextFontColorProperty, value);
    }

    public IEnumerable<Color> TextFontColorOptions
    {
        get => Get<IEnumerable<Color>>(TextFontColorOptionsProperty);
        set => Set(TextFontColorOptionsProperty, value);
    }

    public int TextFontColorOpacity
    {
        get => Get<int>(TextFontColorOpacityProperty);
        set => Set(TextFontColorOpacityProperty, value);
    }

    public string TextFontFamily
    {
        get => Get<string>(TextFontFamilyProperty) ?? "Segoe UI";
        set => Set(TextFontFamilyProperty, value);
    }

    public int TextFontSize
    {
        get => Get<int>(TextFontSizeProperty);
        set => Set(TextFontSizeProperty, value);
    }

    public event EventHandler<Color>? TextFontColorChanged;
    public event EventHandler<int>? TextFontColorOpacityChanged;
    public event EventHandler<string>? TextFontFamilyChanged;
    public event EventHandler<int>? TextFontSizeChanged;

    public TextToolbar()
    {
        InitializeComponent();
    }

    private void UpdateTextFontColor(Color value)
    {
        if (TextFontColor.Equals(value))
        {
            return;
        }

        TextFontColor = value;
        TextFontColorChanged?.Invoke(this, value);
    }

    private void UpdateTextFontColorOpacity(int value)
    {
        if (TextFontColorOpacity == value)
        {
            return;
        }

        TextFontColorOpacity = value;
        TextFontColorOpacityChanged?.Invoke(this, value);
    }

    private void UpdateTextFontFamily(string value)
    {
        if (TextFontFamily.Equals(value, StringComparison.Ordinal))
        {
            return;
        }

        TextFontFamily = value;
        TextFontFamilyChanged?.Invoke(this, value);
    }

    private void UpdateTextFontSize(int value)
    {
        if (TextFontSize == value)
        {
            return;
        }

        TextFontSize = value;
        TextFontSizeChanged?.Invoke(this, value);
    }

    private void FontColorPalette_SelectedColorChanged(object? sender, Color color)
    {
        UpdateTextFontColor(color);
    }

    private void FontColorPalette_OpacityPercentageChanged(object? sender, int opacityPercentage)
    {
        UpdateTextFontColorOpacity(opacityPercentage);
    }

    private void FontFamilyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            UpdateTextFontFamily(textBox.Text);
        }
    }

    private void FontSizeNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!double.IsNaN(args.NewValue))
        {
            UpdateTextFontSize((int)Math.Round(args.NewValue));
        }
    }
}
