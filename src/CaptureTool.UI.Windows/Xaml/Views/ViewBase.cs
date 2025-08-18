﻿using CaptureTool.Common.Loading;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;

namespace CaptureTool.UI.Windows.Xaml.Views;

public abstract partial class ViewBase<VM> : UserControl where VM : ViewModelBase
{
    private CancellationTokenSource? _loadCts;
    public VM ViewModel { get; } = App.Current.ServiceProvider.GetService<VM>();

    public ViewBase()
    {
        DataContext = ViewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    ~ViewBase()
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loadCts ??= new();

        try
        {
            if (ViewModel is IAsyncLoadable asyncLoadable && asyncLoadable.IsUnloaded)
            {
                await asyncLoadable.LoadAsync(null, _loadCts.Token);
            }
            else if (ViewModel is ILoadable loadable && loadable.IsUnloaded)
            {
                loadable.Load(null);
            }
        }
        catch (OperationCanceledException ex)
        {
            ServiceLocator.Logging.LogException(ex, "View load canceled.");
        }
        catch (Exception ex)
        {
            ServiceLocator.Logging.LogException(ex, "Failed to load view.");
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_loadCts != null)
        {
            _loadCts.Cancel();
            _loadCts.Dispose();
            _loadCts = null;
        }

        if (ViewModel is IUnloadable unloadable)
        {
            unloadable.Unload();
        }
    }
}
