using System.Globalization;

namespace CaptureTool.Application.Capture;

internal static class CaptureFileNameFormatter
{
    public static string Create(DateTime timestamp, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Capture_{timestamp:yyyy-MM-dd_HHmmss_fffffff}_{Guid.NewGuid():N}.{extension}");
    }
}
