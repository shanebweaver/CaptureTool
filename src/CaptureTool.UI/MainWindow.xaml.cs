using System;
using System.Threading;
using CaptureTool.Services.Navigation;
using CaptureTool.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using WinRT.Interop;

namespace CaptureTool.UI;

public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; } = ViewModelLocator.MainWindow;

    private readonly CancellationTokenSource _activationCts = new();

    private readonly AppWindow _appWindow;

    public MainWindow()
    {
        InitializeComponent();

        _appWindow = GetAppWindowForCurrentWindow();
        var titleBar = _appWindow.TitleBar;
        titleBar.ExtendsContentIntoTitleBar = true;

        Activated += OnActivated;
        Closed += OnClosed;
        SizeChanged += OnSizeChanged;
        VisibilityChanged += OnVisibilityChanged;
        ViewModel.NavigationRequested += OnViewModelNavigationRequested;
    }

    private AppWindow GetAppWindowForCurrentWindow()
    {
        IntPtr hWnd = WindowNative.GetWindowHandle(this);
        WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        return AppWindow.GetFromWindowId(wndId);
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        AppWindow.MoveAndResize(new(48, 48, 540, 320), DisplayArea.Primary);

        if (args.WindowActivationState == WindowActivationState.CodeActivated && ViewModel.IsUnloaded)
        {
            await ViewModel.LoadAsync(null, _activationCts.Token);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Activated -= OnActivated;
        Closed -= OnClosed;

        ViewModel.NavigationRequested -= OnViewModelNavigationRequested;

        _activationCts.Cancel();
        _activationCts.Dispose();
    }

    private void OnSizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        Size newSize = args.Size;
        // TODO: Save size to settings
    }

    private void OnVisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        // TODO: Figure out how this affects the size. We don't want to restore a minimized window to the previous size (0,0).
    }

    private void OnViewModelNavigationRequested(NavigationRequest navigationRequest)
    {
        Type viewType = ViewLocator.GetViewType(navigationRequest.Key);
        NavigationFrame.Navigate(viewType, navigationRequest.Parameter);
    }

    private bool SetTitleBarColors()
    {
        // Check to see if customization is supported.
        // The method returns true on Windows 10 since Windows App SDK 1.2,
        // and on all versions of Windows App SDK on Windows 11.
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindowTitleBar m_TitleBar = _appWindow.TitleBar;

            // Set active window colors.
            // Note: No effect when app is running on Windows 10
            // because color customization is not supported.
            m_TitleBar.ForegroundColor = Colors.White;
            m_TitleBar.BackgroundColor = Colors.Green;
            m_TitleBar.ButtonForegroundColor = Colors.White;
            m_TitleBar.ButtonBackgroundColor = Colors.SeaGreen;
            m_TitleBar.ButtonHoverForegroundColor = Colors.Gainsboro;
            m_TitleBar.ButtonHoverBackgroundColor = Colors.DarkSeaGreen;
            m_TitleBar.ButtonPressedForegroundColor = Colors.Gray;
            m_TitleBar.ButtonPressedBackgroundColor = Colors.LightGreen;

            // Set inactive window colors.
            // Note: No effect when app is running on Windows 10
            // because color customization is not supported.
            m_TitleBar.InactiveForegroundColor = Colors.Gainsboro;
            m_TitleBar.InactiveBackgroundColor = Colors.SeaGreen;
            m_TitleBar.ButtonInactiveForegroundColor = Colors.Gainsboro;
            m_TitleBar.ButtonInactiveBackgroundColor = Colors.SeaGreen;
            return true;
        }
        return false;
    }
}
