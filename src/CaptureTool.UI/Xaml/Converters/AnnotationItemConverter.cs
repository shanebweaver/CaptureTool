using System;
using System.Collections.Generic;
using System.Linq;
using CaptureTool.Capture.Desktop.Annotation;
using CaptureTool.UI.Xaml.Controls.ImageCanvas.Drawable;
using Microsoft.UI.Xaml.Data;
using Windows.Foundation;
using Windows.UI;

namespace CaptureTool.UI.Xaml.Converters;

internal sealed partial class AnnotationItemConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is IEnumerable<AnnotationItem> annotationItems)
        {
            return annotationItems.Select<AnnotationItem, IDrawable>((annotationItem) =>
            {
                return annotationItem switch
                {
                    RectangleShapeAnnotationItem rectangleItem => CreateRectangleDrawable(rectangleItem),
                    TextAnnotationItem textItem => CreateTextDrawable(textItem),
                    _ => throw new NotImplementedException()
                };
            });
        }

        throw new ArgumentException("Invalid value type. Expected AnnotationItem.", nameof(value));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }

    private RectangleDrawable CreateRectangleDrawable(RectangleShapeAnnotationItem rectangleItem)
    {
        Point position = new(rectangleItem.Left, rectangleItem.Top);
        Size size = new(rectangleItem.Width, rectangleItem.Height);
        Color color = ConvertColor(rectangleItem.Color);
        return new RectangleDrawable(position, size, color, rectangleItem.StrokeWidth);
    }

    private TextDrawable CreateTextDrawable(TextAnnotationItem textItem)
    {
        Point position = new(textItem.Left, textItem.Top);
        Color color = ConvertColor(textItem.Color);
        return new TextDrawable(position, textItem.Text, color);
    }

    private static Color ConvertColor(System.Drawing.Color color)
    {
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}
