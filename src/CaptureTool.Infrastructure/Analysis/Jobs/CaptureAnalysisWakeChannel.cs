using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Capture.Assets;
using System.Threading.Channels;

namespace CaptureTool.Infrastructure.Analysis.Jobs;

internal sealed class CaptureAnalysisWakeChannel :
    ICaptureAssetChangeSignal,
    ICaptureAnalysisWakeSignal,
    ICaptureAnalysisWakeWaiter
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });

    public bool TrySignal()
    {
        return _channel.Writer.TryWrite(true);
    }

    public async ValueTask WaitAsync(
        TimeSpan maximumDelay,
        CancellationToken cancellationToken = default)
    {
        if (maximumDelay < TimeSpan.Zero && maximumDelay != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDelay));
        }

        using var timeout = new CancellationTokenSource();
        if (maximumDelay != Timeout.InfiniteTimeSpan)
        {
            timeout.CancelAfter(maximumDelay);
        }

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        try
        {
            _ = await _channel.Reader.ReadAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The due-time delay elapsed without a wake signal.
        }
    }
}
