using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using System;

namespace CaptureTool.UI.Windows.Xaml.Converters;

public sealed partial class ColorTypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        try
        {
            if (value is System.Drawing.Color c)
            {
                return global::Windows.UI.Color.FromArgb(c.A, c.R, c.G, c.B);
            }
        }
        catch (Exception)
        {
            // Failed to convert the color.
        }
        return Colors.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        try
        {
            if (value is global::Windows.UI.Color c)
            {
                return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
            }
        }
        catch (Exception)
        {
            // Failed to convert the color.
        }
        return System.Drawing.KnownColor.White;
    }
}
