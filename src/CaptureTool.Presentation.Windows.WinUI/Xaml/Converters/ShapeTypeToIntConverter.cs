using CaptureTool.Domain.Edit.Interfaces;
using Microsoft.UI.Xaml.Data;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Converters;

public sealed partial class ShapeTypeToIntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ShapeType shapeType)
        {
            return (int)shapeType;
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is int intValue && intValue >= 0 && intValue <= 3)
        {
            return (ShapeType)intValue;
        }
        return ShapeType.Rectangle;
    }
}
