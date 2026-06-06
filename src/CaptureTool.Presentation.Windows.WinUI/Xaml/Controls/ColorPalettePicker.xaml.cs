using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.Drawing;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class ColorPalettePicker : UserControlBase
{
    private const double ColorItemSize = 48;

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

    public static readonly DependencyProperty IsOpacityVisibleProperty = DependencyProperty.Register(
        nameof(IsOpacityVisible),
        typeof(bool),
        typeof(ColorPalettePicker),
        new PropertyMetadata(true));

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

    public bool IsOpacityVisible
    {
        get => Get<bool>(IsOpacityVisibleProperty);
        set => Set(IsOpacityVisibleProperty, value);
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
        UpdateColorGridSize();
        Loading += ColorPalettePicker_Loading;
        Loaded += ColorPalettePicker_Loaded;
    }

    private void ColorPalettePicker_Loading(FrameworkElement sender, object args)
    {
        UpdateColorGridSize();
    }

    private static void OnColorOptionsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPalettePicker colorPalettePicker)
        {
            colorPalettePicker.RefreshColorItems();
            colorPalettePicker.UpdateColorGridSize();
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
        if (d is not ColorPalettePicker colorPalettePicker)
        {
            return;
        }

        colorPalettePicker.UpdateColorGridSize();

        if (colorPalettePicker.IsLoaded)
        {
            colorPalettePicker.UpdateItemsPanelLayout();
        }
    }

    private void ColorPalettePicker_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateItemsPanelLayout();
        UpdateColorGridSize();
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

    private void UpdateColorGridSize()
    {
        int colorCount = Math.Max(1, _colorItems.Count);
        int maximumRowsOrColumns = Math.Max(1, MaximumRowsOrColumns);
        int rows;
        int columns;

        if (PaletteOrientation == Orientation.Horizontal)
        {
            rows = Math.Min(colorCount, maximumRowsOrColumns);
            columns = (int)Math.Ceiling((double)colorCount / rows);
        }
        else
        {
            columns = Math.Min(colorCount, maximumRowsOrColumns);
            rows = (int)Math.Ceiling((double)colorCount / columns);
        }

        ColorGridView.Width = columns * ColorItemSize;
        ColorGridView.Height = rows * ColorItemSize;
        ColorGridView.MinWidth = ColorGridView.Width;
        ColorGridView.MinHeight = ColorGridView.Height;
        ColorGridView.MaxWidth = ColorGridView.Width;
        ColorGridView.MaxHeight = ColorGridView.Height;
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

                bool isSelected = color.Equals(SelectedColor) || IsMatchingVisibleColor(color, SelectedColor);
                if (isSelected && selectedItem == null)
                {
                    selectedItem = colorItem;
                }
            }

            ColorGridView.SelectedItem = selectedItem;
            UpdateSelectionVisuals();
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
        UpdateSelectedItem();
        SelectedColorChanged?.Invoke(this, color);
    }

    private void ColorGridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not GridViewItem item)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() => UpdateSelectionVisual(item, args.Item));
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

    private void UpdateSelectionVisuals()
    {
        foreach (object colorItem in _colorItems)
        {
            if (ColorGridView.ContainerFromItem(colorItem) is GridViewItem item)
            {
                UpdateSelectionVisual(item, colorItem);
            }
        }
    }

    private void UpdateSelectionVisual(GridViewItem item, object colorItem)
    {
        Border? selectionBorder = FindDescendantByName<Border>(item, "SelectionBorder");
        if (selectionBorder == null || colorItem is not Color color)
        {
            return;
        }

        selectionBorder.Visibility = color.Equals(SelectedColor) || IsMatchingVisibleColor(color, SelectedColor)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static T? FindDescendantByName<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T element && element.Name == name)
            {
                return element;
            }

            T? descendant = FindDescendantByName<T>(child, name);
            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }
}
