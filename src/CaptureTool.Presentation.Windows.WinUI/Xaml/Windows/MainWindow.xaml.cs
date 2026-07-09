using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Presentation.Shell;
using CaptureTool.Presentation.Windows.WinUI.AudioCapture;
using CaptureTool.Presentation.Windows.WinUI.Capture;
using CaptureTool.Presentation.Windows.WinUI.Edit;
using CaptureTool.Presentation.Windows.WinUI.EditSessions;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Pages;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.WindowManagement;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Windows;

public sealed partial class MainWindow : Window
{
    private static readonly SizeInt32 DefaultWindowSize = new(720, 540);
    private static readonly SizeInt32 MinWindowSize = new(500, 374);

    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;
    private readonly IEditSessionGuard _editSessionGuard;
    private readonly IShutdownHandler _shutdownHandler;
    private readonly WinUIAudioCaptureNavigationConfirmationService _audioCaptureNavigationConfirmationService;
    private readonly WinUICaptureDiscardConfirmationService _captureDiscardConfirmationService;
    private readonly WinUIEditSessionConfirmationService _editSessionConfirmationService;
    private readonly ImageSuperResolutionPreparationConsentService _imageSuperResolutionPreparationConsentService;
    private readonly DispatcherQueueTimer _notificationTimer;

    public MainWindowViewModel ViewModel { get; } = ViewModelLocator.GetViewModel<MainWindowViewModel>();
    private bool _closeConfirmed;

    public MainWindow()
    {
        _audioCaptureNavigationGuard = App.Current.ServiceProvider.GetService<IAudioCaptureNavigationGuard>();
        _editSessionGuard = App.Current.ServiceProvider.GetService<IEditSessionGuard>();
        _shutdownHandler = App.Current.ServiceProvider.GetService<IShutdownHandler>();
        _audioCaptureNavigationConfirmationService = App.Current.ServiceProvider.GetService<WinUIAudioCaptureNavigationConfirmationService>();
        _captureDiscardConfirmationService = App.Current.ServiceProvider.GetService<WinUICaptureDiscardConfirmationService>();
        _editSessionConfirmationService = App.Current.ServiceProvider.GetService<WinUIEditSessionConfirmationService>();
        _imageSuperResolutionPreparationConsentService = App.Current.ServiceProvider.GetService<ImageSuperResolutionPreparationConsentService>();

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = MinWindowSize.Width;
            presenter.PreferredMinimumHeight = MinWindowSize.Height;
        }

        InitializeComponent();
        _notificationTimer = DispatcherQueue.CreateTimer();
        _notificationTimer.Interval = TimeSpan.FromSeconds(6);
        _notificationTimer.Tick += NotificationTimer_Tick;
        RootGrid.Loaded += RootGrid_Loaded;

        AppTitleBar.Loaded += AppTitleBar_Loaded;
        AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
        ExtendsContentIntoTitleBar = true;
        UpdateAppTitle();

        Activated += OnActivated;
        Closed += OnClosed;
        ViewModel.NavigationRequested += OnViewModelNavigationRequested;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdateRequestedAppTheme();
        UpdateTitleBarColors();
        RestartNotificationTimer();
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        _editSessionConfirmationService.XamlRoot = RootGrid.XamlRoot;
        _audioCaptureNavigationConfirmationService.XamlRoot = RootGrid.XamlRoot;
        _captureDiscardConfirmationService.XamlRoot = RootGrid.XamlRoot;
        _imageSuperResolutionPreparationConsentService.XamlRoot = RootGrid.XamlRoot;
    }

    private void UpdateAppTitle()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            string appTitle = "Capture Tool";

            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                appTitle = AppInfo.Current.DisplayInfo.DisplayName;
            }

            Title = appTitle;
            TitleBarTextBlock.Text = appTitle;
        });
    }

    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        if (ExtendsContentIntoTitleBar == true)
        {
            // Set the initial interactive regions.
            SetRegionsForCustomTitleBar();
        }
    }

    private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ExtendsContentIntoTitleBar == true)
        {
            // Update interactive regions if the size of the window changes.
            SetRegionsForCustomTitleBar();
        }
    }

    private void SetRegionsForCustomTitleBar()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            double scaleAdjustment = AppTitleBar.XamlRoot.RasterizationScale;

            var rightInset = AppWindow.TitleBar.RightInset;
            var leftInset = AppWindow.TitleBar.LeftInset;

            RightPaddingColumn.Width = new GridLength(rightInset);
            LeftPaddingColumn.Width = new GridLength(leftInset);

            int offsetX = (int)GetElementOffsetFromWindowLeftInPixels(DraggablePanel);
            int offsetY = 0; // At the top

            var width = (int)(AppWindow.ClientSize.Width - (rightInset + leftInset + AppMenuColumn.ActualWidth));
            var height = AppWindow.TitleBar.Height;

            RectInt32 draggableRect = new(offsetX, offsetY, width, height);
            AppWindow.TitleBar.SetDragRectangles([draggableRect]);
        });
    }

    // Returns the X offset in physical pixels from the element to the left edge of the window
    private static double GetElementOffsetFromWindowLeftInPixels(FrameworkElement element)
    {
        // Transform (0,0) of the element to the window's coordinate space
        GeneralTransform transform = element.TransformToVisual(null); // 'null' means root visual (window)
        Point offset = transform.TransformPoint(new Point(0, 0));
        return offset.X;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentAppTheme))
        {
            UpdateRequestedAppTheme();
            UpdateTitleBarColors();
        }

        if (e.PropertyName == nameof(MainWindowViewModel.CurrentNotification))
        {
            RestartNotificationTimer();
        }
    }

    private void RestartNotificationTimer()
    {
        _notificationTimer.Stop();

        if (ViewModel.HasNotification)
        {
            _notificationTimer.Start();
        }
    }

    private void NotificationTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        _notificationTimer.Stop();
        ViewModel.DismissNotificationCommand.Execute(null);
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated)
        {
            Activated -= OnActivated;
            RestoreAppWindowSizeAndPosition();
        }
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        if (!_closeConfirmed)
        {
            args.Handled = true;
            bool canClose = await _editSessionGuard.CanLeaveCurrentSessionAsync();
            if (canClose)
            {
                canClose = await _audioCaptureNavigationGuard.CanNavigateAwayFromActiveCaptureAsync();
            }

            if (canClose)
            {
                _closeConfirmed = true;
                DispatcherQueue.TryEnqueue(Close);
            }

            return;
        }

        Activated -= OnActivated;
        Closed -= OnClosed;
        RootGrid.Loaded -= RootGrid_Loaded;
        AppTitleBar.Loaded -= AppTitleBar_Loaded;
        AppTitleBar.SizeChanged -= AppTitleBar_SizeChanged;
        _notificationTimer.Stop();
        _notificationTimer.Tick -= NotificationTimer_Tick;

        ViewModel.NavigationRequested -= OnViewModelNavigationRequested;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        ViewModel.Dispose();

        _shutdownHandler.Shutdown();
    }

    public void SuspendMediaPlayback()
    {
        switch (NavigationFrame.Content)
        {
            case AudioEditPage audioEditPage:
                audioEditPage.SuspendMediaPlayback();
                break;

            case VideoEditPage videoEditPage:
                videoEditPage.SuspendMediaPlayback();
                break;
        }
    }

    public void ResumeMediaPlayback()
    {
        switch (NavigationFrame.Content)
        {
            case AudioEditPage audioEditPage:
                audioEditPage.ResumeMediaPlayback();
                break;

            case VideoEditPage videoEditPage:
                videoEditPage.ResumeMediaPlayback();
                break;
        }
    }

    public void HandleNavigationRequest(INavigationRequest request)
    {
        bool navigationRequested = ViewModel.HandleNavigationRequest(request);
        if (!navigationRequested)
        {
            DispatcherQueue.TryEnqueue(ResumeMediaPlayback);
        }
    }

    private void OnViewModelNavigationRequested(object? sender, INavigationRequest navigationRequest)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Type pageType = PageLocator.GetPageType(navigationRequest.Route);
            if (navigationRequest.IsBackNavigation && NavigationFrame.CanGoBack)
            {
                NavigationFrame.GoBack();
                NavigationFrame.ForwardStack.Clear();
                GC.Collect();
            }
            else
            {
                NavigationFrame.Navigate(pageType, navigationRequest.Parameter);
            }

            if (navigationRequest.ClearHistory)
            {
                NavigationFrame.ForwardStack.Clear();
                NavigationFrame.BackStack.Clear();
                GC.Collect();
            }

            ResumeMediaPlayback();
        });
    }

    private void RestoreAppWindowSizeAndPosition()
    {
        AppWindow.Move(new PointInt32(1, 1));
        AppWindow.Resize(DefaultWindowSize);
        this.CenterOnScreen();
    }

    private void UpdateRequestedAppTheme()
    {
        object theme = ViewModel.CurrentAppTheme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            AppTheme.SystemDefault => ConvertToElementTheme(ViewModel.DefaultAppTheme),
            _ => DependencyProperty.UnsetValue
        };

        RootGrid.SetValue(FrameworkElement.RequestedThemeProperty, theme);
    }

    private static ElementTheme ConvertToElementTheme(AppTheme appTheme)
    {
        return appTheme switch
        {
            AppTheme.SystemDefault => ElementTheme.Default,
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private void UpdateTitleBarColors()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var titleBar = AppWindow.TitleBar;

            if (titleBar is null)
                return;

            AppTheme appTheme = ViewModel.CurrentAppTheme == AppTheme.SystemDefault
                ? ViewModel.DefaultAppTheme
                : ViewModel.CurrentAppTheme;

            Color foregroundColor = appTheme switch
            {
                AppTheme.Dark => Colors.White,
                _ => Colors.Black,
            };

            titleBar.ForegroundColor = foregroundColor;
            titleBar.ButtonForegroundColor = foregroundColor;

            titleBar.InactiveForegroundColor = foregroundColor;
            titleBar.ButtonInactiveForegroundColor = foregroundColor;
        });
    }
}
