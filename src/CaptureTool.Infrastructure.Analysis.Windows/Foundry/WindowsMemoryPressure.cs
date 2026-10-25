using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Analysis.Windows.Foundry;

/// <summary>Checks Windows' system-wide low physical memory notification without blocking.</summary>
internal sealed partial class WindowsMemoryPressure : IDisposable
{
    private readonly SafeWaitHandle _notification = CreateMemoryResourceNotification(0);

    // If Windows cannot report memory availability, avoid retaining an unused model.
    public bool IsLow => _notification.IsInvalid ||
        QueryMemoryResourceNotification(_notification, out int low) == 0 || low != 0;

    public void Dispose() => _notification.Dispose();

    [LibraryImport("kernel32.dll")]
    private static partial SafeWaitHandle CreateMemoryResourceNotification(int notificationType);

    [LibraryImport("kernel32.dll")]
    private static partial int QueryMemoryResourceNotification(SafeWaitHandle notification, out int resourceState);
}
