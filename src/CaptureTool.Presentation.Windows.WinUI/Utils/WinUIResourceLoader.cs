using Microsoft.Windows.ApplicationModel.Resources;
using System.Runtime.InteropServices;

namespace CaptureTool.Presentation.Windows.WinUI.Utils;

internal static class WinUIResourceLoader
{
    private const int NoPackageIdentityHResult = unchecked((int)0x80073D54);

    public static string GetString(
        ref ResourceLoader? resourceLoader,
        string resourceKey,
        string fallback)
    {
        try
        {
            resourceLoader ??= new ResourceLoader();
            string value = resourceLoader.GetString(resourceKey);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
        catch (InvalidOperationException ex) when (ex.HResult == NoPackageIdentityHResult)
        {
            return fallback;
        }
        catch (COMException ex) when (ex.HResult == NoPackageIdentityHResult)
        {
            return fallback;
        }
    }
}
