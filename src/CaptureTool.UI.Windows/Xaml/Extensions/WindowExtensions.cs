using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;

namespace CaptureTool.UI.Windows.Xaml.Extensions;

internal static partial class WindowExtensions
{
    public static void Maximize(this Window window)
    {
        window.UpdateOverlappedPresenter(p => p.Maximize());
    }

    public static void Minimize(this Window window)
    {
        window.UpdateOverlappedPresenter(p => p.Minimize());
    }

    public static void Restore(this Window window)
    {
        window.UpdateOverlappedPresenter(p => p.Restore());
    }

    private static void UpdateOverlappedPresenter(this Window window, Action<OverlappedPresenter> action)
    {
        if (window is null)
        {
            throw new ArgumentNullException(nameof(window));
        }

        var appwindow = window.AppWindow;
        if (appwindow.Presenter is OverlappedPresenter overlapped)
        {
            action(overlapped);
        }
        else
        {
            throw new NotSupportedException($"Not supported with a {appwindow.Presenter.Kind} presenter");
        }
    }
}
