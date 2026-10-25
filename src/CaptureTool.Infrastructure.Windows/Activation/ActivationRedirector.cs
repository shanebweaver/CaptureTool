using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Windows.Activation;

/// <summary>Waits for activation redirection while allowing COM to service the calling apartment.</summary>
public static partial class ActivationRedirector
{
    private const int RpcCallPending = unchecked((int)0x80010115);

    public static void Redirect(Func<Task> redirect, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(redirect);
        if (timeout.TotalMilliseconds < 1 || timeout.TotalMilliseconds >= uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var completed = new EventWaitHandle(false, EventResetMode.ManualReset);
        Task operation = Task.Run(async () =>
        {
            try
            {
                await redirect().ConfigureAwait(false);
            }
            finally
            {
                completed.Set();
            }
        });

        try
        {
            int result;
            unsafe
            {
                nint handle = completed.SafeWaitHandle.DangerousGetHandle();
                result = CoWaitForMultipleObjects(0, (uint)timeout.TotalMilliseconds, 1, &handle, out _);
            }

            if (result == RpcCallPending)
            {
                throw new TimeoutException("Activation redirection did not complete before the deadline.");
            }

            Marshal.ThrowExceptionForHR(result);
            operation.GetAwaiter().GetResult();
        }
        finally
        {
            // A timed-out WinRT call may still finish. Keep its event alive until
            // Set completes, and observe any late exception before releasing it.
            _ = operation.ContinueWith(task =>
            {
                _ = task.Exception;
                completed.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    [LibraryImport("ole32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial int CoWaitForMultipleObjects(
        uint flags, uint timeoutMilliseconds, uint handleCount, nint* handles, out uint index);
}
