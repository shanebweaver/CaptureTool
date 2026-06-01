using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Point = Windows.Foundation.Point;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class VideoTrimRangeSlider : UserControlBase
{
    public static readonly DependencyProperty DurationSecondsProperty = DependencyProperty.Register(
        nameof(DurationSeconds),
        typeof(double),
        typeof(VideoTrimRangeSlider),
        new PropertyMetadata(0d, OnTimelinePropertyChanged));

    public static readonly DependencyProperty StartSecondsProperty = DependencyProperty.Register(
        nameof(StartSeconds),
        typeof(double),
        typeof(VideoTrimRangeSlider),
        new PropertyMetadata(0d, OnTimelinePropertyChanged));

    public static readonly DependencyProperty EndSecondsProperty = DependencyProperty.Register(
        nameof(EndSeconds),
        typeof(double),
        typeof(VideoTrimRangeSlider),
        new PropertyMetadata(0d, OnTimelinePropertyChanged));

    public static readonly DependencyProperty PlayheadSecondsProperty = DependencyProperty.Register(
        nameof(PlayheadSeconds),
        typeof(double),
        typeof(VideoTrimRangeSlider),
        new PropertyMetadata(0d, OnTimelinePropertyChanged));

    private const double ThumbWidth = 14;
    private const double PlayheadWidth = 4;
    private const double TrackHeight = 8;
    private const double VerticalCenter = 24;
    private DragTarget _dragTarget = DragTarget.None;

    public event EventHandler<double>? StartSecondsChanged;
    public event EventHandler<double>? EndSecondsChanged;
    public event EventHandler<double>? PlayheadSecondsChanged;

    public VideoTrimRangeSlider()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateLayoutPositions();
    }

    public double DurationSeconds
    {
        get => Get<double>(DurationSecondsProperty);
        set => Set(DurationSecondsProperty, Math.Max(0, value));
    }

    public double StartSeconds
    {
        get => Get<double>(StartSecondsProperty);
        set => Set(StartSecondsProperty, Math.Max(0, value));
    }

    public double EndSeconds
    {
        get => Get<double>(EndSecondsProperty);
        set => Set(EndSecondsProperty, Math.Max(0, value));
    }

    public double PlayheadSeconds
    {
        get => Get<double>(PlayheadSecondsProperty);
        set => Set(PlayheadSecondsProperty, Math.Max(0, value));
    }

    private static void OnTimelinePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VideoTrimRangeSlider slider)
        {
            slider.UpdateLayoutPositions();
        }
    }

    private void TimelineCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLayoutPositions();
    }

    private void TimelineCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        PlayheadSecondsChanged?.Invoke(this, SecondsFromPointer(e.GetCurrentPoint(TimelineCanvas).Position));
    }

    private void StartThumb_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        BeginDrag(DragTarget.Start, StartThumb, e);
    }

    private void EndThumb_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        BeginDrag(DragTarget.End, EndThumb, e);
    }

    private void Playhead_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        BeginDrag(DragTarget.Playhead, Playhead, e);
    }

    private void Thumb_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragTarget == DragTarget.None || !e.Pointer.IsInContact)
        {
            return;
        }

        double seconds = SecondsFromPointer(e.GetCurrentPoint(TimelineCanvas).Position);
        switch (_dragTarget)
        {
            case DragTarget.Start:
                StartSecondsChanged?.Invoke(this, Math.Clamp(seconds, 0, EndSeconds));
                break;
            case DragTarget.End:
                EndSecondsChanged?.Invoke(this, Math.Clamp(seconds, StartSeconds, DurationSeconds));
                break;
            case DragTarget.Playhead:
                PlayheadSecondsChanged?.Invoke(this, Math.Clamp(seconds, StartSeconds, EndSeconds));
                break;
        }

        e.Handled = true;
    }

    private void Thumb_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }

        _dragTarget = DragTarget.None;
        e.Handled = true;
    }

    private void BeginDrag(DragTarget dragTarget, UIElement element, PointerRoutedEventArgs e)
    {
        _dragTarget = dragTarget;
        element.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private double SecondsFromPointer(Point pointerPosition)
    {
        if (DurationSeconds <= 0)
        {
            return 0;
        }

        double percent = Math.Clamp(pointerPosition.X / TrackWidth, 0, 1);
        return percent * DurationSeconds;
    }

    private void UpdateLayoutPositions()
    {
        double trackWidth = TrackWidth;
        Track.Width = trackWidth;
        SelectedRange.Width = SecondsToX(EndSeconds) - SecondsToX(StartSeconds);

        Canvas.SetLeft(Track, 0);
        Canvas.SetTop(Track, VerticalCenter - TrackHeight / 2);

        Canvas.SetLeft(SelectedRange, SecondsToX(StartSeconds));
        Canvas.SetTop(SelectedRange, VerticalCenter - TrackHeight / 2);

        Canvas.SetLeft(StartThumb, SecondsToX(StartSeconds) - ThumbWidth / 2);
        Canvas.SetTop(StartThumb, VerticalCenter - StartThumb.Height / 2);

        Canvas.SetLeft(EndThumb, SecondsToX(EndSeconds) - ThumbWidth / 2);
        Canvas.SetTop(EndThumb, VerticalCenter - EndThumb.Height / 2);

        Canvas.SetLeft(Playhead, SecondsToX(PlayheadSeconds) - PlayheadWidth / 2);
        Canvas.SetTop(Playhead, VerticalCenter - Playhead.Height / 2);
    }

    private double TrackWidth => Math.Max(1, TimelineCanvas.ActualWidth);

    private double SecondsToX(double seconds)
    {
        if (DurationSeconds <= 0)
        {
            return 0;
        }

        return Math.Clamp(seconds / DurationSeconds, 0, 1) * TrackWidth;
    }

    private enum DragTarget
    {
        None,
        Start,
        End,
        Playhead,
    }
}
