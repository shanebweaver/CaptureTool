using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Collections.ObjectModel;
using System.Drawing;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class ColorPalettePicker : UserControlBase
{
    public static readonly DependencyProperty ColorOptionsProperty = DependencyProperty.Register(
        nameof(ColorOptions),
        typeof(IEnumerable<Color>),
        typeof(ColorPalettePicker),
        new PropertyMetadata(Array.Empty<Color>(), OnColorOptionsPropertyChanged));

    public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
        nameof(SelectedColor),
        typeof(Color),
        typeof(ColorPalettePicker),
        new PropertyMetadata(Color.Empty, OnSelectionPropertyChanged));

    public static readonly DependencyProperty MaximumRowsOrColumnsProperty = DependencyProperty.Register(
        nameof(MaximumRowsOrColumns),
        typeof(int),
        typeof(ColorPalettePicker),
        new PropertyMetadata(6, OnLayoutPropertyChanged));

    public static readonly DependencyProperty PaletteOrientationProperty = DependencyProperty.Register(
        nameof(PaletteOrientation),
        typeof(Orientation),
        typeof(ColorPalettePicker),
        new PropertyMetadata(Orientation.Horizontal, OnLayoutPropertyChanged));

    public static readonly DependencyProperty IsThicknessVisibleProperty = DependencyProperty.Register(
        nameof(IsThicknessVisible),
        typeof(bool),
        typeof(ColorPalettePicker),
        new PropertyMetadata(false));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness),
        typeof(int),
        typeof(ColorPalettePicker),
        new PropertyMetadata(3));

    public static readonly DependencyProperty OpacityPercentageProperty = DependencyProperty.Register(
        nameof(OpacityPercentage),
        typeof(int),
        typeof(ColorPalettePicker),
        new PropertyMetadata(100));

    private readonly ObservableCollection<object> _colorItems = [];
    private bool _isUpdatingSelection;

    public IEnumerable<Color> ColorOptions
    {
        get => Get<IEnumerable<Color>>(ColorOptionsProperty);
        set => Set(ColorOptionsProperty, value);
    }

    public Color SelectedColor
    {
        get => Get<Color>(SelectedColorProperty);
        set => Set(SelectedColorProperty, value);
    }

    public int MaximumRowsOrColumns
    {
        get => Get<int>(MaximumRowsOrColumnsProperty);
        set => Set(MaximumRowsOrColumnsProperty, value);
    }

    public Orientation PaletteOrientation
    {
        get => Get<Orientation>(PaletteOrientationProperty);
        set => Set(PaletteOrientationProperty, value);
    }

    public bool IsThicknessVisible
    {
        get => Get<bool>(IsThicknessVisibleProperty);
        set => Set(IsThicknessVisibleProperty, value);
    }

    public int Thickness
    {
        get => Get<int>(ThicknessProperty);
        set => Set(ThicknessProperty, value);
    }

    public int OpacityPercentage
    {
        get => Get<int>(OpacityPercentageProperty);
        set => Set(OpacityPercentageProperty, value);
    }

    public event EventHandler<Color>? SelectedColorChanged;
    public event EventHandler<int>? ThicknessChanged;
    public event EventHandler<int>? OpacityPercentageChanged;

    public ColorPalettePicker()
    {
        InitializeComponent();
        ColorGridView.ItemsSource = _colorItems;
        RefreshColorItems();
        Loaded += ColorPalettePicker_Loaded;
    }

    private static void OnColorOptionsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPalettePicker colorPalettePicker)
        {
            colorPalettePicker.RefreshColorItems();
            colorPalettePicker.UpdateSelectedItem();
        }
    }

    private static void OnSelectionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPalettePicker colorPalettePicker)
        {
            colorPalettePicker.UpdateSelectedItem();
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPalettePicker colorPalettePicker && colorPalettePicker.IsLoaded)
        {
            colorPalettePicker.UpdateItemsPanelLayout();
        }
    }

    private void ColorPalettePicker_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateItemsPanelLayout();
        UpdateSelectedItem();
    }

    private void RefreshColorItems()
    {
        _colorItems.Clear();

        foreach (Color color in ColorOptions ?? [])
        {
            _colorItems.Add(color);
        }
    }

    private void UpdateItemsPanelLayout()
    {
        if (ColorGridView.ItemsPanelRoot is ItemsWrapGrid itemsWrapGrid)
        {
            itemsWrapGrid.MaximumRowsOrColumns = MaximumRowsOrColumns;
            itemsWrapGrid.Orientation = PaletteOrientation;
        }
    }

    private void UpdateSelectedItem()
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        _isUpdatingSelection = true;
        try
        {
            object? selectedItem = null;
            foreach (object colorItem in _colorItems)
            {
                if (colorItem is not Color color)
                {
                    continue;
                }

                if (color.Equals(SelectedColor) || IsMatchingVisibleColor(color, SelectedColor))
                {
                    selectedItem = color;
                    break;
                }
            }

            ColorGridView.SelectedItem = selectedItem;
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private void ColorGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection || ColorGridView.SelectedItem is not Color color || color.Equals(SelectedColor))
        {
            return;
        }

        SelectedColor = color;
        SelectedColorChanged?.Invoke(this, color);
    }

    private void ThicknessSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        int thickness = (int)Math.Round(e.NewValue);
        if (Thickness == thickness)
        {
            return;
        }

        Thickness = thickness;
        ThicknessChanged?.Invoke(this, thickness);
    }

    private void OpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        int opacityPercentage = (int)Math.Round(e.NewValue);
        if (OpacityPercentage == opacityPercentage)
        {
            return;
        }

        OpacityPercentage = opacityPercentage;
        OpacityPercentageChanged?.Invoke(this, opacityPercentage);
    }

    private string FormatThickness(int thickness)
    {
        return thickness.ToString();
    }

    private string FormatOpacity(int opacityPercentage)
    {
        return $"{opacityPercentage}%";
    }

    private static bool IsMatchingVisibleColor(Color colorOption, Color selectedColor)
    {
        if (selectedColor.Equals(Color.Transparent))
        {
            return false;
        }

        return colorOption.A > 0 &&
            selectedColor.R == colorOption.R &&
            selectedColor.G == colorOption.G &&
            selectedColor.B == colorOption.B;
    }
}
