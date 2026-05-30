using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    public event EventHandler<Color>? SelectedColorChanged;

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

                if (color.Equals(SelectedColor))
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
}
