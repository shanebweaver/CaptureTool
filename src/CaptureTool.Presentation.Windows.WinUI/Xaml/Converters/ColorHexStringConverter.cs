using Microsoft.UI.Xaml.Data;
using System;
using System.Globalization;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Converters;

public sealed partial class ColorHexStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        try
        {
            if (value is System.Drawing.Color color)
            {
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
        }
        catch(Exception)
        {
            // Failed to convert the color.
        }

        return "#000000";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        try
        {
            if (value is string hex)
            {
                return FromHex(hex);
            }
        }
        catch (Exception)
        {
            // Failed to convert the color.
        }

        return System.Drawing.KnownColor.White;
    }

    public static System.Drawing.Color FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            throw new ArgumentException("Invalid hex color", nameof(hex));

        hex = hex.TrimStart('#');

        if (hex.Length == 6)
        {
            // RRGGBB
            byte r = byte.Parse(hex[..2], NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            return System.Drawing.Color.FromArgb(r, g, b);
        }
        else if (hex.Length == 8)
        {
            // AARRGGBB
            byte a = byte.Parse(hex[..2], NumberStyles.HexNumber);
            byte r = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
            return System.Drawing.Color.FromArgb(a, r, g, b);
        }
        else
        {
            throw new FormatException("Hex color must be 6 (RRGGBB) or 8 (AARRGGBB) characters long.");
        }
    }
}