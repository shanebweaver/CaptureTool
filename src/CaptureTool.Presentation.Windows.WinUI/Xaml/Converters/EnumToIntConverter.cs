using Microsoft.UI.Xaml.Data;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Converters;

public sealed partial class EnumToIntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        try
        {
            if (value is Enum enumValue)
            {
                return System.Convert.ToInt32(enumValue);
            }
        }
        catch (Exception)
        {
            // Failed to convert the enum.
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        try
        {
            if (value is int intValue && targetType.IsEnum)
            {
                return Enum.ToObject(targetType, intValue);
            }
        }
        catch (Exception)
        {
            // Failed to convert the int.
        }
        return Enum.GetValues(targetType).GetValue(0) ?? 0;
    }
}
