using CaptureTool.Application.Abstractions.Analysis;
using Windows.Networking.Connectivity;

namespace CaptureTool.Infrastructure.Analysis.Windows;

/// <summary>Checks download connectivity without making a network request. Local inference never uses this gate.</summary>
internal sealed class ModelDownloadGuard(Func<bool>? isOffline = null)
{
    internal static ModelDownloadGuard Current { get; } = new();
    public bool IsOffline => (isOffline ?? ReadOffline)();

    public void ThrowIfOffline()
    {
        if (IsOffline)
        {
            throw new ModelDownloadRequiredException();
        }
    }

    public async Task RunAsync(Func<CancellationToken, Task> download, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ThrowIfOffline();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task operation = download(cancellation.Token);
        bool disconnected = false;
        try
        {
            while (!operation.IsCompleted)
            {
                await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(1))).ConfigureAwait(false);
                if (!operation.IsCompleted && IsOffline)
                {
                    disconnected = true;
                    await cancellation.CancelAsync().ConfigureAwait(false);
                    break;
                }
            }
            // Keep ownership until the SDK stops, even if cancellation is delayed.
            await operation.ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (disconnected)
            {
                throw new ModelDownloadRequiredException();
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested && (disconnected || IsOffline))
        {
            throw new ModelDownloadRequiredException();
        }
    }

    private static bool ReadOffline()
    {
        try
        {
            return NetworkInformation.GetInternetConnectionProfile()?.GetNetworkConnectivityLevel()
                is not NetworkConnectivityLevel.InternetAccess;
        }
        catch (Exception)
        {
            // An unavailable connectivity probe is not proof that downloads cannot work.
            return false;
        }
    }
}
