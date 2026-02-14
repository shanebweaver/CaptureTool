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
        catch (InvalidCastException)
        {
            // Failed to convert the enum.
        }
        catch (OverflowException)
        {
            // Enum value is outside the range of Int32.
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
        catch (ArgumentException)
        {
            // Failed to convert the int to enum.
        }
        
        // Return the first enum value as fallback
        var enumValues = Enum.GetValues(targetType);
        return enumValues.GetValue(0) ?? 0;
    }
}
