using Microsoft.UI.Dispatching;
using System;

namespace CaptureTool.UI.Windows;

/// <summary>
/// Keeps the application alive even when all UI windows are closed,
/// by maintaining a dedicated DispatcherQueue thread.
/// </summary>
public sealed partial class KeepAlive : IDisposable
{
    private readonly DispatcherQueueController _controller;
    private bool _disposed;

    public KeepAlive()
    {
        _controller = DispatcherQueueController.CreateOnDedicatedThread();
        _controller.DispatcherQueue.TryEnqueue(() => { });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _controller.ShutdownQueueAsync().AsTask().Wait();
        }
        catch
        {
        }

        _disposed = true;
    }
}
