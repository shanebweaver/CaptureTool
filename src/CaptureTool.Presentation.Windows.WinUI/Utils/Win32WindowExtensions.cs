using Microsoft.UI.Xaml;
using System.Drawing;

namespace CaptureTool.Presentation.Windows.WinUI.Utils;

public static partial class Win32WindowExtensions
{
    public static nint GetWindowHandle(this Window window)
        => WinRT.Interop.WindowNative.GetWindowHandle(window);

    public static void SetForegroundWindow(this Window window)
        => Win32WindowHelpers.SetForegroundWindow(window.GetWindowHandle());

    public static void ExcludeFromScreenCapture(this Window window)
        => Win32WindowHelpers.ExcludeFromScreenCapture(window.GetWindowHandle());

    public static void IncludeInScreenCapture(this Window window)
        => Win32WindowHelpers.IncludeInScreenCapture(window.GetWindowHandle());

    public static void MakeBorderlessOverlay(this Window window)
        => Win32WindowHelpers.MakeBorderlessOverlay(window.GetWindowHandle());

    public static void MoveAndResize(this Window window, Rectangle bounds)
        => Win32WindowHelpers.MoveAndResize(window.GetWindowHandle(), bounds);

    public static void CenterOnScreen(this Window window, double? width = null, double? height = null)
        => Win32WindowHelpers.CenterOnScreen(window.GetWindowHandle(), width, height);

    public static void Restore(this Window window)
        => Win32WindowHelpers.Restore(window.GetWindowHandle());

    public static void SetActiveWindow(this Window window)
        => Win32WindowHelpers.SetActiveWindow(window.GetWindowHandle());

    public static bool IsMinimized(this Window window)
        => Win32WindowHelpers.IsMinimized(window.GetWindowHandle());

    public static void Hide(this Window window)
        => Win32WindowHelpers.HideWindow(window.GetWindowHandle());

    public static void Show(this Window window)
        => Win32WindowHelpers.ShowWindow(window.GetWindowHandle());
}
