using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Drawing;
using System.Drawing.Text;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class TextToolbar : UserControlBase
{
    private static readonly string[] FallbackFontFamilies = [
        "Segoe UI",
        "Arial",
        "Calibri",
        "Cambria",
        "Consolas",
        "Courier New",
        "Georgia",
        "Tahoma",
        "Times New Roman",
        "Verdana",
    ];

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

    public static readonly DependencyProperty TextBackgroundColorProperty = DependencyProperty.Register(
        nameof(TextBackgroundColor),
        typeof(Color),
        typeof(TextToolbar),
        new PropertyMetadata(Color.Transparent));

    public static readonly DependencyProperty TextBackgroundColorOptionsProperty = DependencyProperty.Register(
        nameof(TextBackgroundColorOptions),
        typeof(IEnumerable<Color>),
        typeof(TextToolbar),
        new PropertyMetadata(Array.Empty<Color>()));

    public static readonly DependencyProperty TextBackgroundColorOpacityProperty = DependencyProperty.Register(
        nameof(TextBackgroundColorOpacity),
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

    public Color TextBackgroundColor
    {
        get => Get<Color>(TextBackgroundColorProperty);
        set => Set(TextBackgroundColorProperty, value);
    }

    public IEnumerable<Color> TextBackgroundColorOptions
    {
        get => Get<IEnumerable<Color>>(TextBackgroundColorOptionsProperty);
        set => Set(TextBackgroundColorOptionsProperty, value);
    }

    public int TextBackgroundColorOpacity
    {
        get => Get<int>(TextBackgroundColorOpacityProperty);
        set => Set(TextBackgroundColorOpacityProperty, value);
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

    public IReadOnlyList<string> FontFamilyOptions { get; } = GetInstalledFontFamilies();

    public event EventHandler<Color>? TextFontColorChanged;
    public event EventHandler<int>? TextFontColorOpacityChanged;
    public event EventHandler<Color>? TextBackgroundColorChanged;
    public event EventHandler<int>? TextBackgroundColorOpacityChanged;
    public event EventHandler<string>? TextFontFamilyChanged;
    public event EventHandler<int>? TextFontSizeChanged;

    public TextToolbar()
    {
        InitializeComponent();
    }

    private static IReadOnlyList<string> GetInstalledFontFamilies()
    {
        try
        {
            using InstalledFontCollection installedFonts = new();
            return installedFonts.Families
                .Select(fontFamily => fontFamily.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return FallbackFontFamilies;
        }
    }

    private void UpdateTextFontColor(Color value)
    {
        if (!TextFontColor.Equals(value))
        {
            TextFontColor = value;
            TextFontColorChanged?.Invoke(this, value);
        }
    }

    private void UpdateTextFontColorOpacity(int value)
    {
        if (TextFontColorOpacity != value)
        {
            TextFontColorOpacity = value;
            TextFontColorOpacityChanged?.Invoke(this, value);
        }
    }

    private void UpdateTextBackgroundColor(Color value)
    {
        if (!TextBackgroundColor.Equals(value))
        {
            TextBackgroundColor = value;
            TextBackgroundColorChanged?.Invoke(this, value);
        }
    }

    private void UpdateTextBackgroundColorOpacity(int value)
    {
        if (TextBackgroundColorOpacity != value)
        {
            TextBackgroundColorOpacity = value;
            TextBackgroundColorOpacityChanged?.Invoke(this, value);
        }
    }

    private void UpdateTextFontFamily(string value)
    {
        if (!TextFontFamily.Equals(value, StringComparison.Ordinal))
        {
            TextFontFamily = value;
            TextFontFamilyChanged?.Invoke(this, value);
        }
    }

    private void UpdateTextFontSize(int value)
    {
        if (TextFontSize != value)
        {
            TextFontSize = value;
            TextFontSizeChanged?.Invoke(this, value);
        }
    }

    private void FontColorPalette_SelectedColorChanged(object? sender, Color color)
    {
        UpdateTextFontColor(color);
    }

    private void FontColorPalette_OpacityPercentageChanged(object? sender, int opacityPercentage)
    {
        UpdateTextFontColorOpacity(opacityPercentage);
    }

    private void BackgroundColorPalette_SelectedColorChanged(object? sender, Color color)
    {
        UpdateTextBackgroundColor(color);
    }

    private void BackgroundColorPalette_OpacityPercentageChanged(object? sender, int opacityPercentage)
    {
        UpdateTextBackgroundColorOpacity(opacityPercentage);
    }

    private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: string fontFamily } && !string.IsNullOrWhiteSpace(fontFamily))
        {
            UpdateTextFontFamily(fontFamily);
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
