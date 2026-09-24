using CaptureTool.Presentation.Features.AnalyzedContent;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;

public sealed partial class AnalyzedContentPane : UserControlBase
{
    private readonly AnalyzedContentViewModel _fallbackViewModel = new();
    private AnalyzedContentViewModel? _subscribedViewModel;
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(AnalyzedContentViewModel),
        typeof(AnalyzedContentPane),
        new PropertyMetadata(null, OnViewModelChanged));

    public AnalyzedContentPane()
    {
        InitializeComponent();
        Loaded += (_, _) => Subscribe();
        Unloaded += (_, _) => Unsubscribe();
        PassageList.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(PassageList_ManualScroll), true);
        PassageList.AddHandler(PointerPressedEvent, new PointerEventHandler(PassageList_PointerPressed), true);
    }

    public AnalyzedContentViewModel ViewModel
    {
        get => Get<AnalyzedContentViewModel?>(ViewModelProperty) ?? _fallbackViewModel;
        set => Set(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is AnalyzedContentPane pane)
        {
            pane.DataContext = args.NewValue;
            pane.Bindings.Update();
            if (pane.IsLoaded) { pane.Subscribe(); }
        }
    }

    private void Subscribe()
    {
        Unsubscribe();
        _subscribedViewModel = ViewModel;
        _subscribedViewModel.ItemFocusRequested += ViewModel_ItemFocusRequested;
        ScrollToSelectedPassage();
    }

    private void Unsubscribe()
    {
        if (_subscribedViewModel != null) { _subscribedViewModel.ItemFocusRequested -= ViewModel_ItemFocusRequested; }
        _subscribedViewModel = null;
    }

    private void ViewModel_ItemFocusRequested(object? sender, AnalyzedContentItemViewModel item) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (IsLoaded && ViewModel.FilteredItems.Contains(item)) { PassageList.ScrollIntoView(item, ScrollIntoViewAlignment.Leading); }
        });

    private void ScrollToSelectedPassage()
    {
        if (ViewModel.SelectedItem is { } item) { ViewModel_ItemFocusRequested(ViewModel, item); }
    }

    private void PassageList_Loaded(object sender, RoutedEventArgs e) => ScrollToSelectedPassage();
    private void PassageList_ManualScroll(object sender, PointerRoutedEventArgs e) => ViewModel.FollowPlayback = false;
    private void PassageList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.PageUp or VirtualKey.PageDown or VirtualKey.Home or VirtualKey.End or VirtualKey.Up or VirtualKey.Down)
        {
            ViewModel.FollowPlayback = false;
        }
    }

    private void PassageList_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Touch) { ViewModel.FollowPlayback = false; return; }
        for (DependencyObject? element = e.OriginalSource as DependencyObject; element != null && element != PassageList;
            element = VisualTreeHelper.GetParent(element))
        {
            if (element is ScrollBar) { ViewModel.FollowPlayback = false; return; }
        }
    }
}
