using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using CaptureTool.Edit.Image.Win2D;
using CaptureTool.Edit.Image.Win2D.Drawable;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace CaptureTool.UI.Xaml.Controls.ImageCanvas;

public sealed partial class ImageCanvas : UserControl
{
    private static readonly DependencyProperty DrawablesProperty = DependencyProperty.Register(
        nameof(Drawables),
        typeof(IEnumerable<IDrawable>),
        typeof(ImageCanvas),
        new PropertyMetadata(null));

    private static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation),
        typeof(RotateFlipType),
        typeof(ImageCanvas),
        new PropertyMetadata(RotateFlipType.RotateNoneFlipNone, OnOrientationPropertyChanged));

    private static readonly DependencyProperty CanvasSizeProperty = DependencyProperty.Register(
        nameof(CanvasSize),
        typeof(Size),
        typeof(ImageCanvas),
        new PropertyMetadata(new Size(0,0), OnCanvasSizePropertyChanged));

    private static void OnOrientationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control)
        {
            if (e.NewValue is RotateFlipType orientation)
            {
                // Check if orientation is turned by 90 or 270 degrees
                bool isTurned =
                    orientation == RotateFlipType.Rotate90FlipNone ||
                    orientation == RotateFlipType.Rotate90FlipX ||
                    orientation == RotateFlipType.Rotate90FlipY ||
                    orientation == RotateFlipType.Rotate90FlipXY;

                var height = control.CanvasSize.Height;
                var width = control.CanvasSize.Width;

                control.DrawingCanvas.Height = isTurned ? width : height;
                control.DrawingCanvas.Width = isTurned ? height : width;
                control.DrawingCanvas.Invalidate();
            }
        }
    }

    private static void OnCanvasSizePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas control)
        {
            if (e.NewValue is Size newSize)
            {
                // Check if orientation is turned by 90 or 270 degrees
                RotateFlipType orientation = control.Orientation;
                bool isTurned =
                    orientation == RotateFlipType.Rotate90FlipNone ||
                    orientation == RotateFlipType.Rotate90FlipX ||
                    orientation == RotateFlipType.Rotate90FlipY ||
                    orientation == RotateFlipType.Rotate90FlipXY;

                control.DrawingCanvas.Height = isTurned ? newSize.Width : newSize.Height;
                control.DrawingCanvas.Width = isTurned ? newSize.Height : newSize.Width;
            }
        }
    }

    public IEnumerable<IDrawable> Drawables
    {
        get => (IEnumerable<IDrawable>)GetValue(DrawablesProperty);
        set => SetValue(DrawablesProperty, value);
    }

    public RotateFlipType Orientation
    {
        get => (RotateFlipType)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public Size CanvasSize
    {
        get => (Size)GetValue(CanvasSizeProperty);
        set => SetValue(CanvasSizeProperty, value);
    }

    private bool _isPointerDown;
    private Windows.Foundation.Point _lastPointerPosition;

    public ImageCanvas()
    {
        InitializeComponent();
    }

    #region Drawing
    private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        lock (this)
        {
            ImageCanvasRenderOptions options = new(Orientation, CanvasSize);
            ImageCanvasRenderer.Render([.. Drawables], options, args.DrawingSession);
        }
    }

    private void CanvasControl_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        // Create any resources needed by the Draw event handler.

        // Asynchronous work can be tracked with TrackAsyncAction:
        args.TrackAsyncAction(CreateResourcesAsync(sender).AsAsyncAction());
    }

    private async Task CreateResourcesAsync(CanvasControl sender)
    {
        // Load bitmaps, create brushes, etc.
        List<Task> preparationTasks = [];

        foreach (IDrawable drawable in Drawables)
        {
            if (drawable is ImageDrawable imageDrawable)
            {
                Task prepTask = imageDrawable.PrepareAsync(sender);
                preparationTasks.Add(prepTask);
            }
        }

        await Task.WhenAll(preparationTasks);
    }
    #endregion

    #region Panning
    private void CanvasContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = true;
        _lastPointerPosition = e.GetCurrentPoint(CanvasContainer).Position;
        CanvasContainer.CapturePointer(e.Pointer);
    }

    private void CanvasContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isPointerDown)
        {
            var currentPosition = e.GetCurrentPoint(CanvasContainer).Position;
            double deltaX = _lastPointerPosition.X - currentPosition.X;
            double deltaY = _lastPointerPosition.Y - currentPosition.Y;

            if (deltaX == 0 && deltaY == 0)
            {
                return;
            }

            CanvasScrollView.ScrollBy(
                deltaX,
                deltaY,
                new(ScrollingAnimationMode.Disabled)
            );

            _lastPointerPosition = currentPosition;
        }
    }

    private void CanvasContainer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
        CanvasContainer.ReleasePointerCaptures();
    }

    private void CanvasContainer_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
        CanvasContainer.ReleasePointerCaptures();
    }
    #endregion
}
