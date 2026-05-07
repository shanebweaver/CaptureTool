using Microsoft.UI.Xaml.Controls;
using System.Drawing;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class TextToolbar : UserControlBase
{
    public static readonly Microsoft.UI.Xaml.DependencyProperty TextContentProperty = Microsoft.UI.Xaml.DependencyProperty.Register(
        nameof(TextContent),
        typeof(string),
        typeof(TextToolbar),
        new Microsoft.UI.Xaml.PropertyMetadata(string.Empty));

    public static readonly Microsoft.UI.Xaml.DependencyProperty TextFontColorProperty = Microsoft.UI.Xaml.DependencyProperty.Register(
        nameof(TextFontColor),
        typeof(Color),
        typeof(TextToolbar),
        new Microsoft.UI.Xaml.PropertyMetadata(Color.Black));

    public static readonly Microsoft.UI.Xaml.DependencyProperty TextFontFamilyProperty = Microsoft.UI.Xaml.DependencyProperty.Register(
        nameof(TextFontFamily),
        typeof(string),
        typeof(TextToolbar),
        new Microsoft.UI.Xaml.PropertyMetadata("Segoe UI"));

    public static readonly Microsoft.UI.Xaml.DependencyProperty TextFontSizeProperty = Microsoft.UI.Xaml.DependencyProperty.Register(
        nameof(TextFontSize),
        typeof(int),
        typeof(TextToolbar),
        new Microsoft.UI.Xaml.PropertyMetadata(24));

    public string TextContent
    {
        get => Get<string>(TextContentProperty) ?? string.Empty;
        set => Set(TextContentProperty, value);
    }

    public Color TextFontColor
    {
        get => Get<Color>(TextFontColorProperty);
        set => Set(TextFontColorProperty, value);
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

    public event EventHandler<string>? TextContentChanged;
    public event EventHandler<Color>? TextFontColorChanged;
    public event EventHandler<string>? TextFontFamilyChanged;
    public event EventHandler<int>? TextFontSizeChanged;

    public TextToolbar()
    {
        InitializeComponent();
    }

    private void UpdateTextContent(string value)
    {
        if (TextContent.Equals(value, StringComparison.Ordinal))
        {
            return;
        }

        TextContent = value;
        TextContentChanged?.Invoke(this, value);
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

    private void TextContentBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdateTextContent(textBox.Text);
        }
    }

    private void FontColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (sender is ColorPicker colorPicker)
        {
            var color = colorPicker.Color;
            UpdateTextFontColor(Color.FromArgb(color.A, color.R, color.G, color.B));
        }
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
        if (sender is NumberBox numberBox && !double.IsNaN(numberBox.Value))
        {
            UpdateTextFontSize((int)numberBox.Value);
        }
    }
}
