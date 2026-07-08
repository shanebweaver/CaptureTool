using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.Edit.Operations;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Domain.Edit;

public sealed class ImageEditSession
{
    public ImageEditSession(Size imageSize)
        : this(imageSize, ImageOrientation.RotateNoneFlipNone, new Rectangle(Point.Empty, imageSize), [])
    {
    }

    public ImageEditSession(Size imageSize, ImageOrientation orientation, Rectangle cropRect)
        : this(imageSize, orientation, cropRect, [])
    {
    }

    public ImageEditSession(Size imageSize, ImageOrientation orientation, Rectangle cropRect, IEnumerable<IDrawable> drawables)
    {
        ImageSize = imageSize;
        Orientation = orientation;
        CropRect = cropRect;
        _drawables = [.. drawables];
    }

    private readonly List<IDrawable> _drawables;

    public Size ImageSize { get; private set; }

    public ImageOrientation Orientation { get; private set; }

    public Rectangle CropRect { get; private set; }

    public IReadOnlyList<IDrawable> Drawables => _drawables;

    public ChromaKeySettings ChromaKeySettings { get; private set; } = ChromaKeySettings.Default;

    public void SetCropRect(Rectangle cropRect)
    {
        CropRect = cropRect;
    }

    public void ResizeImage(Size imageSize)
    {
        if (ImageSize == imageSize)
        {
            return;
        }

        if (ImageSize.Width <= 0 || ImageSize.Height <= 0)
        {
            ImageSize = imageSize;
            CropRect = new Rectangle(Point.Empty, imageSize);
            return;
        }

        double scaleX = (double)imageSize.Width / ImageSize.Width;
        double scaleY = (double)imageSize.Height / ImageSize.Height;

        CropRect = ScaleRectangle(CropRect, scaleX, scaleY);
        foreach (IDrawable drawable in _drawables)
        {
            ScaleDrawable(drawable, scaleX, scaleY);
        }

        ImageSize = imageSize;
    }

    public void SetOrientation(ImageOrientation orientation)
    {
        if (orientation == Orientation)
        {
            return;
        }

        CropRect = ImageOrientationGeometry.GetOrientedCropRect(CropRect, ImageSize, Orientation, orientation);
        Orientation = orientation;
    }

    public void Rotate(RotationDirection rotationDirection)
    {
        SetOrientation(ImageOrientationGeometry.GetRotatedOrientation(Orientation, rotationDirection));
    }

    public void Flip(FlipDirection flipDirection)
    {
        Size orientedImageSize = ImageOrientationGeometry.GetOrientedImageSize(ImageSize, Orientation);
        CropRect = ImageOrientationGeometry.GetFlippedCropRect(CropRect, orientedImageSize, flipDirection);
        Orientation = ImageOrientationGeometry.GetFlippedOrientation(Orientation, flipDirection);
    }

    public ImageEditRenderSnapshot CreateRenderSnapshot()
    {
        return new(Orientation, ImageSize, CropRect);
    }

    public void AddDrawable(IDrawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);

        _drawables.Add(drawable);
    }

    public void InsertDrawable(int index, IDrawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);

        if (index < 0 || index > _drawables.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _drawables.Insert(index, drawable);
    }

    public IDrawable RemoveDrawableAt(int index)
    {
        if (index < 0 || index >= _drawables.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        IDrawable drawable = _drawables[index];
        _drawables.RemoveAt(index);
        return drawable;
    }

    public bool RemoveDrawable(IDrawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);

        return _drawables.Remove(drawable);
    }

    public IDrawable GetDrawableAt(int index)
    {
        if (index < 0 || index >= _drawables.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _drawables[index];
    }

    public void ApplyShapeState(int index, ModifyShapeOperation.ShapeState state)
    {
        ApplyShapeState(GetDrawableAt(index), state);
    }

    public void SetChromaKeySettings(ChromaKeySettings settings)
    {
        ChromaKeySettings = settings;

        ImageDrawable? imageDrawable = _drawables.OfType<ImageDrawable>().FirstOrDefault();
        if (imageDrawable == null)
        {
            return;
        }

        if (imageDrawable.ImageEffect is not ImageChromaKeyEffect chromaKeyEffect)
        {
            chromaKeyEffect = new ImageChromaKeyEffect(settings.Color, settings.Tolerance / 100f, settings.Desaturation / 100f);
            imageDrawable.ImageEffect = chromaKeyEffect;
        }

        chromaKeyEffect.Color = settings.Color;
        chromaKeyEffect.Tolerance = settings.Tolerance / 100f;
        chromaKeyEffect.Desaturation = settings.Desaturation / 100f;
        chromaKeyEffect.IsEnabled = !settings.Color.IsEmpty;
    }

    private static void ApplyShapeState(IDrawable drawable, ModifyShapeOperation.ShapeState state)
    {
        switch (drawable)
        {
            case RectangleDrawable rect:
                rect.Offset = state.Offset;
                rect.Size = state.Size;
                rect.StrokeColor = state.StrokeColor;
                rect.FillColor = state.FillColor;
                rect.StrokeWidth = state.StrokeWidth;
                break;

            case EllipseDrawable ellipse:
                ellipse.Offset = state.Offset;
                ellipse.Size = state.Size;
                ellipse.StrokeColor = state.StrokeColor;
                ellipse.FillColor = state.FillColor;
                ellipse.StrokeWidth = state.StrokeWidth;
                break;

            case LineDrawable line:
                line.Offset = state.Offset;
                line.EndPoint = state.EndPoint;
                line.StrokeColor = state.StrokeColor;
                line.StrokeWidth = state.StrokeWidth;
                break;

            case ArrowDrawable arrow:
                arrow.Offset = state.Offset;
                arrow.EndPoint = state.EndPoint;
                arrow.StrokeColor = state.StrokeColor;
                arrow.StrokeWidth = state.StrokeWidth;
                break;

            case TextDrawable text:
                text.Offset = state.Offset;
                text.Size = state.Size;
                text.Text = state.Text;
                text.Color = state.TextColor;
                text.BackgroundColor = state.TextBackgroundColor;
                text.FontFamily = state.FontFamily;
                text.FontSize = state.FontSize;
                break;
        }
    }

    private static void ScaleDrawable(IDrawable drawable, double scaleX, double scaleY)
    {
        double averageScale = (scaleX + scaleY) / 2;
        switch (drawable)
        {
            case ImageDrawable image:
                image.Offset = ScaleVector(image.Offset, scaleX, scaleY);
                image.ImageSize = ScaleSize(image.ImageSize, scaleX, scaleY);
                break;

            case RectangleDrawable rect:
                rect.Offset = ScaleVector(rect.Offset, scaleX, scaleY);
                rect.Size = ScaleSize(rect.Size, scaleX, scaleY);
                rect.StrokeWidth = ScaleInt(rect.StrokeWidth, averageScale);
                break;

            case EllipseDrawable ellipse:
                ellipse.Offset = ScaleVector(ellipse.Offset, scaleX, scaleY);
                ellipse.Size = ScaleSize(ellipse.Size, scaleX, scaleY);
                ellipse.StrokeWidth = ScaleInt(ellipse.StrokeWidth, averageScale);
                break;

            case LineDrawable line:
                line.Offset = ScaleVector(line.Offset, scaleX, scaleY);
                line.EndPoint = ScaleVector(line.EndPoint, scaleX, scaleY);
                line.StrokeWidth = ScaleInt(line.StrokeWidth, averageScale);
                break;

            case ArrowDrawable arrow:
                arrow.Offset = ScaleVector(arrow.Offset, scaleX, scaleY);
                arrow.EndPoint = ScaleVector(arrow.EndPoint, scaleX, scaleY);
                arrow.StrokeWidth = ScaleInt(arrow.StrokeWidth, averageScale);
                break;

            case TextDrawable text:
                text.Offset = ScaleVector(text.Offset, scaleX, scaleY);
                text.Size = ScaleSize(text.Size, scaleX, scaleY);
                text.FontSize = (float)Math.Max(1, text.FontSize * averageScale);
                break;
        }
    }

    private static Rectangle ScaleRectangle(Rectangle rectangle, double scaleX, double scaleY)
    {
        return new(
            ScaleInt(rectangle.X, scaleX),
            ScaleInt(rectangle.Y, scaleY),
            ScaleInt(rectangle.Width, scaleX),
            ScaleInt(rectangle.Height, scaleY));
    }

    private static Size ScaleSize(Size size, double scaleX, double scaleY)
    {
        return new(ScaleInt(size.Width, scaleX), ScaleInt(size.Height, scaleY));
    }

    private static Vector2 ScaleVector(Vector2 vector, double scaleX, double scaleY)
    {
        return new((float)(vector.X * scaleX), (float)(vector.Y * scaleY));
    }

    private static int ScaleInt(int value, double scale)
    {
        return Math.Max(0, (int)Math.Round(value * scale));
    }
}
