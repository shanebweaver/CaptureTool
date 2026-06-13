using CaptureTool.Application.Abstractions.Features.ImageEdit.ChromaKey;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Drawing;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class ChromaKeyToolbar : UserControlBase
{
    private const int MinimumEffectPercentage = 0;
    private const int MaximumEffectPercentage = 100;
    private static readonly TimeSpan SliderCommitDelay = TimeSpan.FromMilliseconds(300);

    public static readonly DependencyProperty ColorOptionsProperty = DependencyProperty.Register(
        nameof(ColorOptions),
        typeof(IEnumerable<ChromaKeyColorOption>),
        typeof(ChromaKeyToolbar),
        new PropertyMetadata(null, OnColorOptionsChanged));

    public static readonly DependencyProperty SelectedColorOptionIndexProperty = DependencyProperty.Register(
        nameof(SelectedColorOptionIndex),
        typeof(int),
        typeof(ChromaKeyToolbar),
        new PropertyMetadata(-1, OnSelectedColorOptionIndexChanged));

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
        set => Set(ToleranceProperty, Math.Clamp(value, MinimumEffectPercentage, MaximumEffectPercentage));
    }

    public int Desaturation
    {
        get => Get<int>(DesaturationProperty);
        set => Set(DesaturationProperty, Math.Clamp(value, MinimumEffectPercentage, MaximumEffectPercentage));
    }

    private readonly ObservableCollection<ChromaKeyColorOption> _bindableColorOptions = [];
    private INotifyCollectionChanged? _colorOptionsCollection;
    private readonly DispatcherQueueTimer _sliderCommitTimer;
    private IReadOnlyList<Color> _paletteColorOptions = [];
    private Color _selectedColor = Color.Empty;
    private bool _hasColorOptions;
    private bool _areEffectOptionsEnabled;
    private bool _isSliderInteractionActive;
    private bool _isChromaKeyInteractionOpen;

    public IReadOnlyList<Color> PaletteColorOptions
    {
        get => _paletteColorOptions;
        private set => Set(ref _paletteColorOptions, value);
    }

    public Color SelectedColor
    {
        get => _selectedColor;
        private set => Set(ref _selectedColor, value);
    }

    public bool HasColorOptions
    {
        get => _hasColorOptions;
        private set => Set(ref _hasColorOptions, value);
    }

    public bool AreEffectOptionsEnabled
    {
        get => _areEffectOptionsEnabled;
        private set => Set(ref _areEffectOptionsEnabled, value);
    }

    public event EventHandler<int>? SelectedColorOptionIndexChanged;
    public event EventHandler<int>? ToleranceChanged;
    public event EventHandler<int>? DesaturationChanged;
    public event EventHandler? ChromaKeyInteractionStarted;
    public event EventHandler? ChromaKeyInteractionCompleted;

    public ChromaKeyToolbar()
    {
        InitializeComponent();
        _sliderCommitTimer = DispatcherQueue.CreateTimer();
        _sliderCommitTimer.Interval = SliderCommitDelay;
        _sliderCommitTimer.Tick += SliderCommitTimer_Tick;
        RegisterSliderInteractionHandlers(ToleranceSlider);
        RegisterSliderInteractionHandlers(DesaturationSlider);
    }

    private void RegisterSliderInteractionHandlers(Slider slider)
    {
        slider.AddHandler(PointerPressedEvent, new PointerEventHandler(Slider_PointerPressed), true);
        slider.AddHandler(PointerReleasedEvent, new PointerEventHandler(Slider_PointerInteractionCompleted), true);
        slider.AddHandler(PointerCanceledEvent, new PointerEventHandler(Slider_PointerInteractionCompleted), true);
        slider.AddHandler(PointerCaptureLostEvent, new PointerEventHandler(Slider_PointerInteractionCompleted), true);
    }

    private static void OnColorOptionsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ChromaKeyToolbar toolbar)
        {
            toolbar.UpdateColorOptions(args.NewValue as IEnumerable<ChromaKeyColorOption>);
        }
    }

    private static void OnSelectedColorOptionIndexChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ChromaKeyToolbar toolbar)
        {
            toolbar.UpdateSelectedColor();
            toolbar.UpdateSliderEnablement();
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

        PaletteColorOptions = [.. _bindableColorOptions.Select(option => option.Color)];
        HasColorOptions = _bindableColorOptions.Count > 0;
        UpdateSelectedColor();
        UpdateSliderEnablement();
    }

    private void UpdateSelectedColor()
    {
        SelectedColor =
            _bindableColorOptions.Count > SelectedColorOptionIndex && SelectedColorOptionIndex >= 0
                ? _bindableColorOptions[SelectedColorOptionIndex].Color
                : Color.Empty;
    }

    private void UpdateSliderEnablement()
    {
        AreEffectOptionsEnabled =
            _bindableColorOptions.Count > SelectedColorOptionIndex &&
            SelectedColorOptionIndex >= 0 &&
            !_bindableColorOptions[SelectedColorOptionIndex].IsEmpty;
    }

    private bool UpdateSelectedColorOptionIndex(int value)
    {
        if (SelectedColorOptionIndex == value)
        {
            return false;
        }

        SelectedColorOptionIndex = value;
        SelectedColorOptionIndexChanged?.Invoke(this, value);
        UpdateSelectedColor();
        UpdateSliderEnablement();
        return true;
    }

    private bool UpdateTolerance(int value)
    {
        value = Math.Clamp(value, MinimumEffectPercentage, MaximumEffectPercentage);
        if (Tolerance == value)
        {
            return false;
        }

        Tolerance = value;
        ToleranceChanged?.Invoke(this, value);
        return true;
    }

    private bool UpdateDesaturation(int value)
    {
        value = Math.Clamp(value, MinimumEffectPercentage, MaximumEffectPercentage);
        if (Desaturation == value)
        {
            return false;
        }

        Desaturation = value;
        DesaturationChanged?.Invoke(this, value);
        return true;
    }

    private void KeyColorPalette_SelectedColorChanged(object? sender, Color color)
    {
        int colorOptionIndex = FindColorOptionIndex(color);
        RunDiscreteChromaKeyInteraction(() => UpdateSelectedColorOptionIndex(colorOptionIndex));
        KeyColorButton.Flyout?.Hide();
    }

    private int FindColorOptionIndex(Color color)
    {
        for (int i = 0; i < _bindableColorOptions.Count; i++)
        {
            if (_bindableColorOptions[i].Color.Equals(color))
            {
                return i;
            }
        }

        return -1;
    }

    private void ToleranceSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        UpdateFromSlider(() => UpdateTolerance((int)Math.Round(args.NewValue)));
    }

    private void DesaturationSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        UpdateFromSlider(() => UpdateDesaturation((int)Math.Round(args.NewValue)));
    }

    private void ToleranceNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue))
        {
            sender.Value = Tolerance;
            return;
        }

        int value = (int)Math.Round(Math.Clamp(
            args.NewValue,
            MinimumEffectPercentage,
            MaximumEffectPercentage));
        sender.Value = value;
        RunDiscreteChromaKeyInteraction(() => UpdateTolerance(value));
    }

    private void DesaturationNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue))
        {
            sender.Value = Desaturation;
            return;
        }

        int value = (int)Math.Round(Math.Clamp(
            args.NewValue,
            MinimumEffectPercentage,
            MaximumEffectPercentage));
        sender.Value = value;
        RunDiscreteChromaKeyInteraction(() => UpdateDesaturation(value));
    }

    private void UpdateFromSlider(Func<bool> update)
    {
        bool changed = RunChromaKeySliderInteraction(update);
        if (changed)
        {
            RestartSliderCommitTimer();
        }
    }

    private bool RunChromaKeySliderInteraction(Func<bool> update)
    {
        EnsureChromaKeyInteractionStarted();
        bool changed = update();
        if (!changed && !_isSliderInteractionActive)
        {
            CompleteChromaKeyInteraction();
        }

        return changed;
    }

    private void RunDiscreteChromaKeyInteraction(Func<bool> update)
    {
        EnsureChromaKeyInteractionStarted();
        bool changed = update();
        if (changed)
        {
            CompleteChromaKeyInteraction();
        }
        else
        {
            CancelChromaKeyInteraction();
        }
    }

    private void Slider_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _isSliderInteractionActive)
        {
            return;
        }

        _isSliderInteractionActive = true;
        EnsureChromaKeyInteractionStarted();
    }

    private void Slider_PointerInteractionCompleted(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSliderInteractionActive)
        {
            return;
        }

        _isSliderInteractionActive = false;
        CompleteChromaKeyInteraction();
    }

    private void SliderCommitTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        _isSliderInteractionActive = false;
        CompleteChromaKeyInteraction();
    }

    private void RestartSliderCommitTimer()
    {
        _sliderCommitTimer.Stop();
        _sliderCommitTimer.Start();
    }

    private void EnsureChromaKeyInteractionStarted()
    {
        if (_isChromaKeyInteractionOpen)
        {
            return;
        }

        _isChromaKeyInteractionOpen = true;
        ChromaKeyInteractionStarted?.Invoke(this, EventArgs.Empty);
    }

    private void CompleteChromaKeyInteraction()
    {
        if (!_isChromaKeyInteractionOpen)
        {
            return;
        }

        _sliderCommitTimer.Stop();
        _isChromaKeyInteractionOpen = false;
        ChromaKeyInteractionCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void CancelChromaKeyInteraction()
    {
        if (!_isChromaKeyInteractionOpen)
        {
            return;
        }

        _sliderCommitTimer.Stop();
        _isChromaKeyInteractionOpen = false;
        ChromaKeyInteractionCompleted?.Invoke(this, EventArgs.Empty);
    }
}
