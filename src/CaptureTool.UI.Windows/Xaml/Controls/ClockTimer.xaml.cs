using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Globalization;
using System.Threading;

namespace CaptureTool.UI.Windows.Xaml.Controls;

public sealed partial class ClockTimer : UserControlBase
{
    private static readonly double DefaultFontSize = 16d;
    private static readonly FontFamily DefaultFontFamily = FontFamily.XamlAutoFontFamily;
    private static readonly double DefaultDigitWidth = 9d;

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(ClockTimer), new PropertyMetadata(false, OnIsActiveChanged));

    public static readonly DependencyProperty DigitFontSizeProperty = DependencyProperty.Register(
        nameof(DigitFontSize), typeof(double), typeof(ClockTimer), new PropertyMetadata(DefaultFontSize));

    public static readonly DependencyProperty DigitFontFamilyProperty = DependencyProperty.Register(
        nameof(DigitFontFamily), typeof(FontFamily), typeof(ClockTimer), new PropertyMetadata(DefaultFontFamily));

    public static readonly DependencyProperty SeparatorFontSizeProperty = DependencyProperty.Register(
        nameof(SeparatorFontSize), typeof(double), typeof(ClockTimer), new PropertyMetadata(DefaultFontSize));

    public static readonly DependencyProperty SeparatorFontFamilyProperty = DependencyProperty.Register(
        nameof(SeparatorFontFamily), typeof(FontFamily), typeof(ClockTimer), new PropertyMetadata(DefaultFontFamily));

    public static readonly DependencyProperty DigitWidthProperty = DependencyProperty.Register(
        nameof(DigitWidth), typeof(double), typeof(ClockTimer), new PropertyMetadata(DefaultDigitWidth));

    public bool IsActive
    {
        get => Get<bool>(IsActiveProperty);
        set => Set(IsActiveProperty, value);
    }

    public double DigitFontSize
    {
        get => Get<double>(DigitFontSizeProperty);
        set => Set(DigitFontSizeProperty, value);
    }

    public FontFamily DigitFontFamily
    {
        get => Get<FontFamily>(DigitFontFamilyProperty);
        set => Set(DigitFontFamilyProperty, value);
    }

    public double SeparatorFontSize
    {
        get => Get<double>(SeparatorFontSizeProperty);
        set => Set(SeparatorFontSizeProperty, value);
    }

    public FontFamily SeparatorFontFamily
    {
        get => Get<FontFamily>(SeparatorFontFamilyProperty);
        set => Set(SeparatorFontFamilyProperty, value);
    }

    public double DigitWidth
    {
        get => Get<double>(DigitWidthProperty);
        set => Set(DigitWidthProperty, value);
    }

    private int _hours;
    private int _minutes;
    private int _seconds;
    private Timer? _timer;

    public ClockTimer()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClockTimer timer)
        {
            if ((bool)e.NewValue)
                timer.StartTimer();
            else
                timer.StopTimer();
        }
    }

    private void StartTimer()
    {
        _timer = new Timer(OnTimerTick, null, 1000, 1000);
    }

    private void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void OnTimerTick(object? state)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            _seconds++;
            if (_seconds >= 60)
            {
                _seconds = 0;
                _minutes++;
                if (_minutes >= 60)
                {
                    _minutes = 0;
                    _hours++;
                }
            }
            UpdateDisplay();
        });
    }

    public void Reset()
    {
        _hours = 0;
        _minutes = 0;
        _seconds = 0;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        var separator = CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator;
        HoursTensText.Text = (_hours / 10).ToString();
        HoursOnesText.Text = (_hours % 10).ToString();
        MinutesTensText.Text = (_minutes / 10).ToString();
        MinutesOnesText.Text = (_minutes % 10).ToString();
        SecondsTensText.Text = (_seconds / 10).ToString();
        SecondsOnesText.Text = (_seconds % 10).ToString();
        Separator1.Text = separator;
        Separator2.Text = separator;
    }
}