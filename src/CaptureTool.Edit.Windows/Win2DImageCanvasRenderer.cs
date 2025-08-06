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
                var desaturation = imageChromaKeyEffect.Desaturation;
                var finalResult = ApplyChromaCleanup(preparedImage, keyColor, tolerance, desaturation);

                drawingSession.Clear(Colors.White);
                drawingSession.DrawImage(finalResult, drawable.Offset);
            }
            else
            {
                drawingSession.Clear(Colors.White);
                drawingSession.DrawImage(preparedImage, drawable.Offset);
            }
        }
    }

    public static async Task PrepareAsync(ImageDrawable imageDrawable, ICanvasResourceCreator resourceCreator)
    {
        ICanvasImage prepared = await CanvasBitmap.LoadAsync(resourceCreator, imageDrawable.FileName.Path);
        imageDrawable.SetPreparedImage(prepared);
    }

    public static ICanvasImage ApplyChromaCleanup(
        ICanvasImage originalImage,
        Color chromaColor,
        float keyTolerance = 0.1f,
        float desaturation = 0f,
        float blur = 0.0f
    )
    {
        // Step 1: Remove the background
        var chromaKeyRemoved = new ChromaKeyEffect
        {
            Source = originalImage,
            Color = chromaColor,
            Tolerance = keyTolerance,
            InvertAlpha = false,
            Feather = true,
        };

        // Step 2: Create mask of pixels matching chroma color (for desaturation)
        var chromaColorMask = new ChromaKeyEffect
        {
            Source = originalImage,
            Color = chromaColor,
            Tolerance = desaturation,
            InvertAlpha = true,
            Feather = false,
        };

        // Step 3: Extract alpha from chromaKeyRemoved as second mask
        var chromaKeyAlphaMask = new OpacityEffect
        {
            Source = chromaKeyRemoved
        };

        // Step 4: Combine both masks with ArithmeticCompositeEffect
        var combinedMask = new ArithmeticCompositeEffect
        {
            Source1 = chromaColorMask,
            Source2 = chromaKeyAlphaMask,
            MultiplyAmount = 1,
            Source1Amount = 0,
            Source2Amount = 0,
            Offset = 0
        };

        ICanvasImage finalMask = combinedMask;

        if (blur > 0f)
        {
            finalMask = new GaussianBlurEffect
            {
                Source = combinedMask,
                BlurAmount = blur,
                Optimization = EffectOptimization.Balanced
            };
        }

        // Step 5: Create desaturated version of the original image
        var desaturated = new ColorMatrixEffect
        {
            Source = originalImage,
            ColorMatrix = new Matrix5x4
            {
                M11 = 0.3f,
                M12 = 0.3f,
                M13 = 0.3f,
                M21 = 0.59f,
                M22 = 0.59f,
                M23 = 0.59f,
                M31 = 0.11f,
                M32 = 0.11f,
                M33 = 0.11f,
                M44 = 1f,
            }
        };

        // Step 6: Mask the desaturated version with the combined mask
        var maskedDesaturated = new AlphaMaskEffect
        {
            Source = desaturated,
            AlphaMask = finalMask
        };

        // Step 7: Composite the masked grayscale image over the background-removed image
        var final = new CompositeEffect
        {
            Mode = CanvasComposite.SourceOver,
            Sources =
            {
                chromaKeyRemoved,
                maskedDesaturated
            }
        };

        return final;
    }
}
