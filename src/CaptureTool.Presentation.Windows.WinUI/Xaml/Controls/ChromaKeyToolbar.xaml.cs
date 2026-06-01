using CaptureTool.Domain.Edit.Abstractions.ChromaKey;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Specialized;
using System.Collections.ObjectModel;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class ChromaKeyToolbar : UserControlBase
{
    public static readonly DependencyProperty ColorOptionsProperty = DependencyProperty.Register(
        nameof(ColorOptions),
        typeof(IEnumerable<ChromaKeyColorOption>),
        typeof(ChromaKeyToolbar),
        new PropertyMetadata(null, OnColorOptionsChanged));

    public static readonly DependencyProperty SelectedColorOptionIndexProperty = DependencyProperty.Register(
        nameof(SelectedColorOptionIndex),
        typeof(int),
        typeof(ChromaKeyToolbar),
        new PropertyMetadata(-1));

    public static readonly DependencyProperty ToleranceProperty = DependencyProperty.Register(
        nameof(Tolerance),
        typeof(int),
        typeof(ChromaKeyToolbar),
        new PropertyMetadata(30));

    public static readonly DependencyProperty DesaturationProperty = DependencyProperty.Register(
        nameof(Desaturation),
        typeof(int),
        typeof(ChromaKeyToolbar),
        new PropertyMetadata(0));

    public IEnumerable<ChromaKeyColorOption>? ColorOptions
    {
        get => GetValue(ColorOptionsProperty) as IEnumerable<ChromaKeyColorOption>;
        set
        {
            Set(ColorOptionsProperty, value);
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

    private readonly ObservableCollection<ChromaKeyColorOption> _bindableColorOptions = [];
    private INotifyCollectionChanged? _colorOptionsCollection;
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
        KeyColorComboBox.ItemsSource = _bindableColorOptions;
    }

    private static void OnColorOptionsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ChromaKeyToolbar toolbar)
        {
            toolbar.UpdateColorOptions(args.NewValue as IEnumerable<ChromaKeyColorOption>);
        }
    }

    private void UpdateColorOptions(IEnumerable<ChromaKeyColorOption>? colorOptions)
    {
        if (_colorOptionsCollection != null)
        {
            _colorOptionsCollection.CollectionChanged -= ColorOptions_CollectionChanged;
            _colorOptionsCollection = null;
        }

        if (colorOptions is INotifyCollectionChanged collectionChanged)
        {
            _colorOptionsCollection = collectionChanged;
            _colorOptionsCollection.CollectionChanged += ColorOptions_CollectionChanged;
        }

        RefreshBindableColorOptions(colorOptions);
    }

    private void ColorOptions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshBindableColorOptions(ColorOptions);
    }

    private void RefreshBindableColorOptions(IEnumerable<ChromaKeyColorOption>? colorOptions)
    {
        _bindableColorOptions.Clear();

        if (colorOptions != null)
        {
            foreach (ChromaKeyColorOption colorOption in colorOptions)
            {
                _bindableColorOptions.Add(colorOption);
            }
        }

        UpdateSliderEnablement();
    }

    private void UpdateSliderEnablement()
    {
        AreEffectOptionsEnabled =
            _bindableColorOptions.Count > SelectedColorOptionIndex &&
            SelectedColorOptionIndex >= 0 &&
            !_bindableColorOptions[SelectedColorOptionIndex].IsEmpty;
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
