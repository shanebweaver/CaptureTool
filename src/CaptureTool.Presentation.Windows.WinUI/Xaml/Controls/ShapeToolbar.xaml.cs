using CaptureTool.Domain.Edit;
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
        new PropertyMetadata(0, OnSelectedShapeTypeIndexChanged));

    public static readonly DependencyProperty StrokeColorProperty = DependencyProperty.Register(
        nameof(StrokeColor),
        typeof(Color),
        typeof(ShapeToolbar),
        new PropertyMetadata(Color.Black, OnStrokeColorChanged));

    public static readonly DependencyProperty FillColorProperty = DependencyProperty.Register(
        nameof(FillColor),
        typeof(Color),
        typeof(ShapeToolbar),
        new PropertyMetadata(Color.Transparent, OnFillColorChanged));

    public static readonly DependencyProperty StrokeColorOptionsProperty = DependencyProperty.Register(
        nameof(StrokeColorOptions),
        typeof(IEnumerable<Color>),
        typeof(ShapeToolbar),
        new PropertyMetadata(Array.Empty<Color>(), OnStrokeColorOptionsChanged));

    public static readonly DependencyProperty FillColorOptionsProperty = DependencyProperty.Register(
        nameof(FillColorOptions),
        typeof(IEnumerable<Color>),
        typeof(ShapeToolbar),
        new PropertyMetadata(Array.Empty<Color>(), OnFillColorOptionsChanged));

    public static readonly DependencyProperty StrokeWidthProperty = DependencyProperty.Register(
        nameof(StrokeWidth),
        typeof(int),
        typeof(ShapeToolbar),
        new PropertyMetadata(3, OnStrokeWidthChanged));

    public static readonly DependencyProperty StrokeOpacityProperty = DependencyProperty.Register(
        nameof(StrokeOpacity),
        typeof(int),
        typeof(ShapeToolbar),
        new PropertyMetadata(100, OnStrokeOpacityChanged));

    public static readonly DependencyProperty FillOpacityProperty = DependencyProperty.Register(
        nameof(FillOpacity),
        typeof(int),
        typeof(ShapeToolbar),
        new PropertyMetadata(100, OnFillOpacityChanged));

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

    public IEnumerable<Color> StrokeColorOptions
    {
        get => Get<IEnumerable<Color>>(StrokeColorOptionsProperty);
        set => Set(StrokeColorOptionsProperty, value);
    }

    public IEnumerable<Color> FillColorOptions
    {
        get => Get<IEnumerable<Color>>(FillColorOptionsProperty);
        set => Set(FillColorOptionsProperty, value);
    }

    public int StrokeWidth
    {
        get => Get<int>(StrokeWidthProperty);
        set => Set(StrokeWidthProperty, value);
    }

    public int StrokeOpacity
    {
        get => Get<int>(StrokeOpacityProperty);
        set => Set(StrokeOpacityProperty, value);
    }

    public int FillOpacity
    {
        get => Get<int>(FillOpacityProperty);
        set => Set(FillOpacityProperty, value);
    }

    public bool IsFillColorEnabled => (ShapeType)SelectedShapeTypeIndex is not ShapeType.Line and not ShapeType.Arrow;

    public event EventHandler<int>? SelectedShapeTypeIndexChanged;
    public event EventHandler<Color>? StrokeColorChanged;
    public event EventHandler<Color>? FillColorChanged;
    public event EventHandler<int>? StrokeWidthChanged;
    public event EventHandler<int>? StrokeOpacityChanged;
    public event EventHandler<int>? FillOpacityChanged;
    public event EventHandler? StyleInteractionStarted;
    public event EventHandler? StyleInteractionCompleted;

    public ShapeToolbar()
    {
        InitializeComponent();
    }

    private static void OnSelectedShapeTypeIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShapeToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(nameof(SelectedShapeTypeIndex));
            toolbar.RaisePropertyChanged(nameof(IsFillColorEnabled));
        }
    }

    private static void OnStrokeColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShapeToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(nameof(StrokeColor));
        }
    }

    private static void OnFillColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShapeToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(nameof(FillColor));
        }
    }

    private static void OnStrokeColorOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShapeToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(nameof(StrokeColorOptions));
        }
    }

    private static void OnFillColorOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShapeToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(nameof(FillColorOptions));
        }
    }

    private static void OnStrokeWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShapeToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(nameof(StrokeWidth));
        }
    }

    private static void OnStrokeOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShapeToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(nameof(StrokeOpacity));
        }
    }

    private static void OnFillOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ShapeToolbar toolbar)
        {
            toolbar.RaisePropertyChanged(nameof(FillOpacity));
        }
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

    private void UpdateStrokeOpacity(int value)
    {
        if (StrokeOpacity == value)
        {
            return;
        }

        StrokeOpacity = value;
        StrokeOpacityChanged?.Invoke(this, value);
    }

    private void UpdateFillOpacity(int value)
    {
        if (FillOpacity == value)
        {
            return;
        }

        FillOpacity = value;
        FillOpacityChanged?.Invoke(this, value);
    }

    private void ShapeTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            UpdateSelectedShapeTypeIndex(comboBox.SelectedIndex);
        }
    }

    private void StrokeColorPalette_SelectedColorChanged(object? sender, Color color)
    {
        UpdateStrokeColor(color);
    }

    private void FillColorPalette_SelectedColorChanged(object? sender, Color color)
    {
        UpdateFillColor(color);
    }

    private void StrokeColorPalette_ThicknessChanged(object? sender, int thickness)
    {
        UpdateStrokeWidth(thickness);
    }

    private void StrokeColorPalette_OpacityPercentageChanged(object? sender, int opacityPercentage)
    {
        UpdateStrokeOpacity(opacityPercentage);
    }

    private void FillColorPalette_OpacityPercentageChanged(object? sender, int opacityPercentage)
    {
        UpdateFillOpacity(opacityPercentage);
    }

    private void ColorPalette_SliderInteractionStarted(object? sender, EventArgs e)
    {
        StyleInteractionStarted?.Invoke(this, EventArgs.Empty);
    }

    private void ColorPalette_SliderInteractionCompleted(object? sender, EventArgs e)
    {
        StyleInteractionCompleted?.Invoke(this, EventArgs.Empty);
    }
}
