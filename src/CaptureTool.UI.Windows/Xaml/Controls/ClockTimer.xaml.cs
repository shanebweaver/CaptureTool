using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Globalization;

namespace CaptureTool.UI.Windows.Xaml.Controls;

public sealed partial class ClockTimer : UserControlBase
{
    private static readonly double DefaultFontSize = 16d;
    private static readonly FontFamily DefaultFontFamily = FontFamily.XamlAutoFontFamily;
    private static readonly double DefaultDigitWidth = 9d;

    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
        nameof(Time), typeof(TimeSpan), typeof(ClockTimer), new PropertyMetadata(TimeSpan.Zero, OnTimeChanged));

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

    public TimeSpan Time
    {
        get => Get<TimeSpan>(TimeProperty);
        set => Set(TimeProperty, value);
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

    public ClockTimer()
    {
        InitializeComponent();
        UpdateDisplay(TimeSpan.Zero);
    }

    private static void OnTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClockTimer timer && e.NewValue is TimeSpan timeSpan)
        {
            timer.UpdateDisplay(timeSpan);
        }
    }

    private void UpdateDisplay(TimeSpan time)
    {
        var separator = CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator;
        int hours = (int)time.TotalHours;
        int minutes = time.Minutes;
        int seconds = time.Seconds;

        HoursTensText.Text = (hours / 10).ToString();
        HoursOnesText.Text = (hours % 10).ToString();
        MinutesTensText.Text = (minutes / 10).ToString();
        MinutesOnesText.Text = (minutes % 10).ToString();
        SecondsTensText.Text = (seconds / 10).ToString();
        SecondsOnesText.Text = (seconds % 10).ToString();
        Separator1.Text = separator;
        Separator2.Text = separator;
    }
}