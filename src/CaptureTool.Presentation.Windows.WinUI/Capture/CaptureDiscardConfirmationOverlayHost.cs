using CaptureTool.Presentation.Windows.WinUI.Utils;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using Rectangle = System.Drawing.Rectangle;

namespace CaptureTool.Presentation.Windows.WinUI.Capture;

internal sealed class CaptureDiscardConfirmationOverlayHost : IDisposable
{
    private const string WindowClassName = "CaptureDiscardConfirmationOverlayWindow";
    private const double CardWidth = 520;

    private readonly Rectangle? _windowBounds;
    private readonly TaskCompletionSource<bool> _resultCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private HWND? _hwnd;
    private DesktopWindowXamlSource? _xamlSource;
    private Grid? _root;
    private bool _disposed;

    public CaptureDiscardConfirmationOverlayHost(Rectangle? windowBounds = null)
    {
        _windowBounds = windowBounds;
    }

    public async Task<bool> ShowConfirmationAsync(
        string title,
        string content,
        string discardButtonText,
        string cancelButtonText)
    {
        Initialize(title, content, discardButtonText, cancelButtonText);
        Activate();

        try
        {
            return await _resultCompletion.Task;
        }
        finally
        {
            Dispose();
        }
    }

    private void Initialize(
        string title,
        string content,
        string discardButtonText,
        string cancelButtonText)
    {
        if (_hwnd is not null)
        {
            return;
        }

        Rectangle bounds = _windowBounds ?? GetFallbackWindowBounds();
        _hwnd = CreateOverlayWindow(bounds);

        _xamlSource = new DesktopWindowXamlSource();
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hwnd.Value);
        _xamlSource.Initialize(windowId);

        _root = CreateRoot(title, content, discardButtonText, cancelButtonText);
        _xamlSource.Content = _root;
    }

    private static unsafe HWND CreateOverlayWindow(Rectangle bounds)
    {
        WNDCLASSEXW wndClass = new()
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            style = 0,
            lpfnWndProc = &WindowProc,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = HINSTANCE.Null,
            hIcon = HICON.Null,
            hCursor = HCURSOR.Null,
            hbrBackground = HBRUSH.Null,
            lpszMenuName = null,
            hIconSm = HICON.Null
        };
        fixed (char* name = WindowClassName)
        {
            wndClass.lpszClassName = name;
            PInvoke.RegisterClassEx(in wndClass);
        }

        HWND hwnd = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
            WindowClassName,
            null,
            WINDOW_STYLE.WS_VISIBLE | WINDOW_STYLE.WS_POPUP,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            new(IntPtr.Zero),
            null,
            null,
            null);

        PInvoke.SetWindowDisplayAffinity(hwnd, WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE);

        return hwnd;
    }

    private Grid CreateRoot(
        string title,
        string content,
        string discardButtonText,
        string cancelButtonText)
    {
        var root = new Grid
        {
            Background = new SolidColorBrush(Colors.Transparent)
        };
        root.PointerPressed += Root_PointerPressed;

        UIElement card = CreateConfirmationCard(title, content, discardButtonText, cancelButtonText);
        root.Children.Add(card);

        return root;
    }

    private UIElement CreateConfirmationCard(
        string title,
        string content,
        string discardButtonText,
        string cancelButtonText)
    {
        var card = new Border
        {
            Width = CardWidth,
            MaxWidth = CardWidth,
            Padding = new Thickness(22),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = GetBrush("AcrylicBackgroundFillColorDefaultBrush", Colors.White),
            BorderBrush = GetBrush("CardStrokeColorDefaultBrush", Colors.Transparent),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Shadow = new ThemeShadow(),
            Translation = new Vector3(0, 0, 32)
        };

        var layout = new Grid
        {
            RowSpacing = 16,
            ColumnSpacing = 16
        };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var iconHost = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            Background = GetBrush("SystemFillColorCriticalBackgroundBrush", ColorHelper.FromArgb(0x33, 0xFF, 0x33, 0x40)),
            Child = new SymbolIcon(Symbol.Important)
            {
                Foreground = GetBrush("SystemFillColorCriticalBrush", ColorHelper.FromArgb(0xFF, 0xC4, 0x2B, 0x1C))
            }
        };
        Grid.SetRowSpan(iconHost, 2);
        layout.Children.Add(iconHost);

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = GetBrush("TextFillColorPrimaryBrush", Colors.Black),
            TextWrapping = TextWrapping.WrapWholeWords
        };
        Grid.SetColumn(titleText, 1);
        layout.Children.Add(titleText);

        var contentText = new TextBlock
        {
            Text = content,
            FontSize = 14,
            Foreground = GetBrush("TextFillColorSecondaryBrush", Colors.DimGray),
            TextWrapping = TextWrapping.WrapWholeWords,
            LineHeight = 20
        };
        Grid.SetRow(contentText, 1);
        Grid.SetColumn(contentText, 1);
        layout.Children.Add(contentText);

        var buttons = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var cancelButton = new Button
        {
            Content = cancelButtonText,
            MinWidth = 118,
            CornerRadius = new CornerRadius(4)
        };
        cancelButton.Click += (_, _) => Complete(false);

        var discardButton = new Button
        {
            Content = discardButtonText,
            MinWidth = 132,
            Background = GetBrush("SystemFillColorCriticalBrush", ColorHelper.FromArgb(0xFF, 0xC4, 0x2B, 0x1C)),
            BorderBrush = GetBrush("SystemFillColorCriticalBrush", ColorHelper.FromArgb(0xFF, 0xC4, 0x2B, 0x1C)),
            Foreground = GetBrush("TextOnAccentFillColorPrimaryBrush", Colors.White),
            CornerRadius = new CornerRadius(4)
        };
        discardButton.Click += (_, _) => Complete(true);

        buttons.Children.Add(cancelButton);
        buttons.Children.Add(discardButton);

        Grid.SetRow(buttons, 2);
        Grid.SetColumnSpan(buttons, 2);
        layout.Children.Add(buttons);

        card.Child = layout;
        return card;
    }

    private void Activate()
    {
        if (_hwnd is null)
        {
            return;
        }

        Win32WindowHelpers.SetActiveWindow(_hwnd.Value);
        Win32WindowHelpers.SetForegroundWindow(_hwnd.Value);
    }

    private void Root_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, _root))
        {
            Complete(false);
        }
    }

    private void Complete(bool shouldDiscard)
    {
        _resultCompletion.TrySetResult(shouldDiscard);
    }

    private static Rectangle GetFallbackWindowBounds()
    {
        HWND foregroundWindow = PInvoke.GetForegroundWindow();
        HMONITOR monitor = PInvoke.MonitorFromWindow(foregroundWindow, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        MONITORINFO info = new()
        {
            cbSize = (uint)Marshal.SizeOf<MONITORINFO>()
        };
        PInvoke.GetMonitorInfo(monitor, ref info);

        return new Rectangle(
            info.rcMonitor.left,
            info.rcMonitor.top,
            info.rcMonitor.right - info.rcMonitor.left,
            info.rcMonitor.bottom - info.rcMonitor.top);
    }

    private static Microsoft.UI.Xaml.Media.Brush GetBrush(string key, global::Windows.UI.Color fallback)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out object value) &&
            value is Microsoft.UI.Xaml.Media.Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }

    [UnmanagedCallersOnly(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    private static LRESULT WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _resultCompletion.TrySetResult(false);

        if (_root is not null)
        {
            _root.PointerPressed -= Root_PointerPressed;
            _root = null;
        }

        if (_xamlSource is not null)
        {
            try
            {
                _xamlSource.Content = null;
                (_xamlSource as IDisposable)?.Dispose();
            }
            catch
            {
            }
            _xamlSource = null;
        }

        if (_hwnd is not null)
        {
            try
            {
                PInvoke.DestroyWindow(_hwnd.Value);
            }
            catch
            {
            }
            _hwnd = null;
        }
    }
}
