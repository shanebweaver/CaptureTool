using CaptureTool.Edit.Drawable;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Color = Windows.UI.Color;

namespace CaptureTool.Edit.Windows;

public static partial class Win2DImageCanvasRenderer
{
    private static readonly Color ClearColor = Colors.Transparent;

    public static void Render(IDrawable[] drawables, ImageCanvasRenderOptions options, CanvasDrawingSession drawingSession, float scale = 1f)
    {
        drawingSession.Clear(ClearColor);

        var device = drawingSession.Device;
        var renderTarget = new CanvasRenderTarget(device, options.CanvasSize.Width, options.CanvasSize.Height, options.Dpi);

        using (var tempSession = renderTarget.CreateDrawingSession())
        {
            tempSession.Clear(ClearColor);

            foreach (IDrawable drawable in drawables)
            {
                Draw(drawable, tempSession);
            }
        }

        //  Rotate, flip/mirror, scale
        var rotateEffect = new Transform2DEffect
        {
            Source = renderTarget,
            TransformMatrix = ImageOrientationHelper.CalculateRenderTransform(options.CanvasSize, options.Orientation, scale)
        };

        // Crop
        var cropEffect = new CropEffect
        {
            Source = rotateEffect,
            SourceRectangle = new(
                new Point(options.CropRect.Location.X, options.CropRect.Location.Y),
                new Size(options.CropRect.Width, options.CropRect.Height))
        };
        var cropAlignmentEffect = new Transform2DEffect
        {
            Source = cropEffect,
            TransformMatrix = Matrix3x2.CreateTranslation(-options.CropRect.X, -options.CropRect.Y)
        };

        drawingSession.DrawImage(cropAlignmentEffect);
    }

    public static void Draw(IDrawable drawable, CanvasDrawingSession drawingSession)
    {
        if (drawable is TextDrawable textDrawable)
            DrawText(textDrawable, drawingSession);
        else if (drawable is RectangleDrawable rectangleDrawable)
            DrawRectangle(rectangleDrawable, drawingSession);
        else if (drawable is ImageDrawable imageDrawable)
            DrawImage(imageDrawable, drawingSession);
    }

    private static void DrawText(TextDrawable drawable, CanvasDrawingSession drawingSession)
    {
        Vector2 textPosition = new(drawable.Offset.X, drawable.Offset.Y);
        Color color = Color.FromArgb(drawable.Color.A, drawable.Color.R, drawable.Color.G, drawable.Color.B);
        drawingSession.DrawText(drawable.Text, textPosition, color);
    }

    private static void DrawRectangle(RectangleDrawable drawable, CanvasDrawingSession drawingSession)
    {
        Rect rectangleRect = new(drawable.Offset.X, drawable.Offset.Y, drawable.Size.Width, drawable.Size.Height);
        Color color = Color.FromArgb(drawable.Color.A, drawable.Color.R, drawable.Color.G, drawable.Color.B);
        drawingSession.DrawRectangle(rectangleRect, color, drawable.StrokeWidth);
    }

    private static void DrawImage(ImageDrawable drawable, CanvasDrawingSession drawingSession)
    {
        ICanvasImage? preparedImage = drawable.GetPreparedImage();
        if (preparedImage != null)
        {
            if (drawable.ImageEffect is ImageChromaKeyEffect imageChromaKeyEffect && imageChromaKeyEffect.IsEnabled)
            {
                var keyColor = Color.FromArgb(
                    imageChromaKeyEffect.Color.A,
                    imageChromaKeyEffect.Color.R,
                    imageChromaKeyEffect.Color.G,
                    imageChromaKeyEffect.Color.B);
                var tolerance = imageChromaKeyEffect.Tolerance;

                // 1. Apply chroma key to image: this gives you a transparent background
                var chromaKeyed = new ChromaKeyEffect
                {
                    Source = preparedImage,
                    Color = keyColor,
                    Tolerance = tolerance,
                    Feather = true,
                    InvertAlpha = false
                };

                // 2. Extract just the alpha mask from the chroma keyed image
                //    (which contains smooth feathered edges of the keyed color)
                var chromaAlphaMask = new ColorMatrixEffect
                {
                    Source = chromaKeyed,
                    ColorMatrix = new Matrix5x4
                    {
                        // Pass-through RGB (won't be used)
                        M11 = 1,
                        M22 = 1,
                        M33 = 1,
                        M44 = 1,     // preserve alpha
                    }
                };

                // 3. Desaturate the original image
                var desaturated = new ColorMatrixEffect
                {
                    Source = preparedImage,
                    ColorMatrix = new Matrix5x4
                    {
                        M11 = 0.299f,
                        M12 = 0.299f,
                        M13 = 0.299f,
                        M21 = 0.587f,
                        M22 = 0.587f,
                        M23 = 0.587f,
                        M31 = 0.114f,
                        M32 = 0.114f,
                        M33 = 0.114f,
                        M44 = 1f
                    }
                };

                // 4. Lerp between original and grayscale using the chromaAlphaMask
                var grayedEdges = LerpByMaskEffectHelper.Create(preparedImage, desaturated, chromaAlphaMask);

                // 5. Apply alpha from chromaKeyed onto the blended result
                //    (so transparent pixels from chromaKeyed stay fully transparent)
                var finalResult = new AlphaMaskEffect
                {
                    Source = grayedEdges,
                    AlphaMask = chromaKeyed // use original chroma keyed image for its alpha
                };

                // 6. Draw it!
                drawingSession.Clear(Colors.White);
                drawingSession.DrawImage(finalResult, drawable.Offset);

            }
            else
            {
                drawingSession.DrawImage(preparedImage, drawable.Offset);
            }
        }
    }

    public static async Task PrepareAsync(ImageDrawable imageDrawable, ICanvasResourceCreator resourceCreator)
    {
        ICanvasImage prepared = await CanvasBitmap.LoadAsync(resourceCreator, imageDrawable.FileName.Path);
        imageDrawable.SetPreparedImage(prepared);
    }
}

public static class LerpByMaskEffectHelper
{
    public static ICanvasImage Create(ICanvasImage original, ICanvasImage altered, ICanvasImage mask)
    {
        // Create inverse mask: (1 - alpha)
        var inverseMask = new ColorMatrixEffect
        {
            Source = mask,
            ColorMatrix = new Matrix5x4
            {
                M11 = 1f,
                M22 = 1f,
                M33 = 1f,
                M44 = -1f,  // Invert alpha
                M54 = 1f    // Offset to complete inversion
            }
        };

        // Multiply original * (1 - alpha)
        var originalPart = new BlendEffect
        {
            Mode = BlendEffectMode.Multiply,
            Background = original,
            Foreground = inverseMask
        };

        // Multiply altered * alpha
        var alteredPart = new BlendEffect
        {
            Mode = BlendEffectMode.Multiply,
            Background = altered,
            Foreground = mask
        };

        // Add them together (original*(1-a) + altered*a)
        return new CompositeEffect
        {
            Mode = CanvasComposite.Add,
            Sources = { originalPart, alteredPart }
        };
    }
}
