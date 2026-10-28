using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CaptureTool.UiTests;

internal static class UiTestAppLauncher
{
    public static Process Launch(ProcessStartInfo startInfo)
    {
        if (Environment.GetEnvironmentVariable("CAPTURETOOL_UI_TEST_APP_ID") is { Length: > 0 } appId)
        {
            var activation = (IApplicationActivationManager)new ApplicationActivationManager();
            try
            {
                string arguments = string.Join(" ", startInfo.ArgumentList.Select(argument => $"\"{argument}\""));
                activation.ActivateApplication(appId, arguments, 0, out uint processId);
                return Process.GetProcessById((int)processId);
            }
            finally
            {
                Marshal.ReleaseComObject(activation);
            }
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to launch CaptureTool.");
    }

    [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        void ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appId,
            [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint processId);
    }

    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private class ApplicationActivationManager { }
}
