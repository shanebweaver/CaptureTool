using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using CaptureTool.Edit.Image.Win2D;
using CaptureTool.Edit.Image.Win2D.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace CaptureTool.UI.Xaml.Controls.ImageCanvas;

public sealed partial class ImageCanvas : UserControl
{
    private static readonly DependencyProperty DrawablesProperty = DependencyProperty.Register(
        nameof(Drawables),
        typeof(IEnumerable<IDrawable>),
        typeof(ImageCanvas),
        new PropertyMetadata(null));

    private static readonly DependencyProperty CanvasSizeProperty = DependencyProperty.Register(
        nameof(CanvasSize),
        typeof(Size),
        typeof(ImageCanvas),
        new PropertyMetadata(null, OnCanvasSizePropertyChanged));

    private static void OnCanvasSizePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas canvas)
        {
            if (e.NewValue is Size canvasSize)
            {
                canvas.UpdateCanvasSize(canvasSize);
            }
        }
    }

    public IEnumerable<IDrawable> Drawables
    {
        get => (IEnumerable<IDrawable>)GetValue(DrawablesProperty);
        set => SetValue(DrawablesProperty, value);
    }

    public Size CanvasSize
    {
        get => (Size)GetValue(CanvasSizeProperty);
        set => SetValue(CanvasSizeProperty, value);
    }

    private bool _isPointerDown;
    private Point _lastPointerPosition;

    public ImageCanvas()
    {
        InitializeComponent();
    }

    private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        lock (this)
        {
            CanvasCommandList commandList = ImageCanvasRenderer.Render([.. Drawables]);

            Vector2 sceneTopLeft = new(0, 0);
            args.DrawingSession.DrawImage(commandList, sceneTopLeft);
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

    private void UpdateCanvasSize(Size newSize)
    {
        AnnotationCanvas.Height = newSize.Height;
        AnnotationCanvas.Width = newSize.Width;
    }

    #region Panning
    private void CanvasContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = true;
        _lastPointerPosition = e.GetCurrentPoint(CanvasContainer).Position;
        CanvasContainer.CapturePointer(e.Pointer); // Capture the pointer for consistent tracking
    }

    private void CanvasContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isPointerDown)
        {
            // Get the current pointer position
            Point currentPosition = e.GetCurrentPoint(CanvasContainer).Position;

            // Calculate the difference between the current and last pointer positions
            double deltaX = _lastPointerPosition.X - currentPosition.X;
            double deltaY = _lastPointerPosition.Y - currentPosition.Y;

            if (deltaX == 0 && deltaY == 0)
            {
                return;
            }

            // Update the scroll offsets of the CanvasScrollView
            CanvasScrollView.ScrollBy(
                deltaX,
                deltaY,
                new(ScrollingAnimationMode.Disabled)
            );

            // Update the last pointer position
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
