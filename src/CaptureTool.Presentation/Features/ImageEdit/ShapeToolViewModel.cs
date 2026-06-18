using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Presentation.Features.ImageEdit;

public sealed partial class ShapeToolViewModel : ViewModelBase
{
    private const int MinimumShapeStrokeWidth = 1;
    private const int MaximumShapeStrokeWidth = 100;

    public ShapeType SelectedShapeType
    {
        get;
        private set => Set(ref field, value);
    }

    public Color ShapeStrokeColor
    {
        get;
        private set => Set(ref field, value);
    }

    public Color ShapeFillColor
    {
        get;
        private set => Set(ref field, value);
    }

    public IReadOnlyList<Color> ShapeStrokeColorOptions { get; }

    public IReadOnlyList<Color> ShapeFillColorOptions { get; }

    public int ShapeStrokeWidth
    {
        get;
        private set => Set(ref field, value);
    }

    public int ShapeStrokeOpacity
    {
        get;
        private set => Set(ref field, value);
    }

    public int ShapeFillOpacity
    {
        get;
        private set => Set(ref field, value);
    }

    public IRelayCommand<ShapeType> UpdateSelectedShapeTypeCommand { get; }
    public IRelayCommand<Color> UpdateShapeStrokeColorCommand { get; }
    public IRelayCommand<Color> UpdateShapeFillColorCommand { get; }
    public IRelayCommand<int> UpdateShapeStrokeWidthCommand { get; }
    public IRelayCommand<int> UpdateShapeStrokeOpacityCommand { get; }
    public IRelayCommand<int> UpdateShapeFillOpacityCommand { get; }

    public ShapeToolViewModel()
    {
        UpdateSelectedShapeTypeCommand = new RelayCommand<ShapeType>(UpdateSelectedShapeType);
        UpdateShapeStrokeColorCommand = new RelayCommand<Color>(UpdateShapeStrokeColor);
        UpdateShapeFillColorCommand = new RelayCommand<Color>(UpdateShapeFillColor);
        UpdateShapeStrokeWidthCommand = new RelayCommand<int>(UpdateShapeStrokeWidth);
        UpdateShapeStrokeOpacityCommand = new RelayCommand<int>(UpdateShapeStrokeOpacity);
        UpdateShapeFillOpacityCommand = new RelayCommand<int>(UpdateShapeFillOpacity);

        SelectedShapeType = ShapeType.Rectangle;
        ShapeStrokeColor = ImageEditColorPalette.Drawables[3]; // Red
        ShapeFillColor = ImageEditColorPalette.Drawables[0]; // Transparent
        ShapeStrokeColorOptions = ImageEditColorPalette.Drawables;
        ShapeFillColorOptions = ImageEditColorPalette.Drawables;
        ShapeStrokeWidth = 3;
        ShapeStrokeOpacity = 100;
        ShapeFillOpacity = 100;
    }

    public void ApplyImageSizeDefaults(Size imageSize)
    {
        int largestEdge = Math.Max(imageSize.Width, imageSize.Height);

        if (largestEdge > 0)
        {
            ShapeStrokeWidth = Math.Clamp(
                (int)Math.Round(largestEdge / 900d),
                3,
                MaximumShapeStrokeWidth);
        }
    }

    public ShapeStyle CreateStyle()
    {
        return new(ShapeStrokeColor, ShapeFillColor, ShapeStrokeWidth);
    }

    public IDrawable? CreateDrawable(Vector2 startPoint, Vector2 endPoint)
    {
        return DrawableFactory.CreateShape(SelectedShapeType, startPoint, endPoint, CreateStyle());
    }

    public void UpdateSelectedShapeType(ShapeType value)
    {
        SelectedShapeType = value;
    }

    public void UpdateShapeStrokeColor(Color value)
    {
        ShapeStrokeColor = ImageEditColorPalette.ApplyOpacity(value, ShapeStrokeOpacity);
    }

    public void UpdateShapeFillColor(Color value)
    {
        ShapeFillColor = ImageEditColorPalette.ApplyOpacity(value, ShapeFillOpacity);
    }

    public void UpdateShapeStrokeWidth(int value)
    {
        ShapeStrokeWidth = Math.Clamp(value, MinimumShapeStrokeWidth, MaximumShapeStrokeWidth);
    }

    public void UpdateShapeStrokeOpacity(int value)
    {
        ShapeStrokeOpacity = Math.Clamp(value, 0, 100);
        ShapeStrokeColor = ImageEditColorPalette.ApplyOpacity(ShapeStrokeColor, ShapeStrokeOpacity);
    }

    public void UpdateShapeFillOpacity(int value)
    {
        ShapeFillOpacity = Math.Clamp(value, 0, 100);
        ShapeFillColor = ImageEditColorPalette.ApplyOpacity(ShapeFillColor, ShapeFillOpacity);
    }

    public void ApplyDrawable(IDrawable drawable)
    {
        switch (drawable)
        {
            case RectangleDrawable rectangle:
                ApplyShape(ShapeType.Rectangle, rectangle.StrokeColor, rectangle.FillColor, rectangle.StrokeWidth);
                break;
            case EllipseDrawable ellipse:
                ApplyShape(ShapeType.Ellipse, ellipse.StrokeColor, ellipse.FillColor, ellipse.StrokeWidth);
                break;
            case LineDrawable line:
                ApplyLineShape(ShapeType.Line, line.StrokeColor, line.StrokeWidth);
                break;
            case ArrowDrawable arrow:
                ApplyLineShape(ShapeType.Arrow, arrow.StrokeColor, arrow.StrokeWidth);
                break;
        }
    }

    private void ApplyShape(ShapeType shapeType, Color strokeColor, Color fillColor, int strokeWidth)
    {
        SelectedShapeType = shapeType;
        ShapeStrokeColor = strokeColor;
        ShapeFillColor = fillColor;
        ShapeStrokeWidth = Math.Clamp(strokeWidth, MinimumShapeStrokeWidth, MaximumShapeStrokeWidth);
        ShapeStrokeOpacity = ImageEditColorPalette.AlphaToOpacityPercentage(strokeColor);
        ShapeFillOpacity = ImageEditColorPalette.AlphaToOpacityPercentage(fillColor);
    }

    private void ApplyLineShape(ShapeType shapeType, Color strokeColor, int strokeWidth)
    {
        SelectedShapeType = shapeType;
        ShapeStrokeColor = strokeColor;
        ShapeStrokeWidth = Math.Clamp(strokeWidth, MinimumShapeStrokeWidth, MaximumShapeStrokeWidth);
        ShapeStrokeOpacity = ImageEditColorPalette.AlphaToOpacityPercentage(strokeColor);
    }
}
