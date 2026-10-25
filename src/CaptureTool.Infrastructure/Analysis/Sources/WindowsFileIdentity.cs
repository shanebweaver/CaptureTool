using CaptureTool.Application.Abstractions.Analysis;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace CaptureTool.Infrastructure.Analysis.Sources;

internal static class WindowsFileIdentity
{
    public static AnalysisSourceIdentity Read(SafeFileHandle handle)
    {
        if (GetFileInformationByHandleEx(handle, 18 /* FileIdInfo */, out FileIdInfo info, 24))
        {
            if ((info.FileIdLow != 0 || info.FileIdHigh != 0) &&
                (info.FileIdLow != ulong.MaxValue || info.FileIdHigh != ulong.MaxValue))
            {
                return new(info.VolumeSerialNumber, info.FileIdLow, info.FileIdHigh);
            }

            throw new IOException("The filesystem did not provide a usable file identity.");
        }

        int error = Marshal.GetLastPInvokeError();
        // Some filesystems expose only 64-bit identifiers. ReFS needs the full 128-bit
        // identifier above; never truncate it to the older API's file index.
        if (error is not (1 or 50 or 87))
        {
            throw Unavailable(error);
        }

        if (!GetFileInformationByHandle(handle, out HandleFileInfo basic))
        {
            throw Unavailable(Marshal.GetLastPInvokeError());
        }

        ulong id = ((ulong)basic.FileIndexHigh << 32) | basic.FileIndexLow;
        if (id is 0 or ulong.MaxValue)
        {
            throw new IOException("The filesystem did not provide a usable file identity.");
        }

        return new(basic.VolumeSerialNumber, id, 0);
    }

    private static IOException Unavailable(int error) => new("The selected file identity is unavailable.", new Win32Exception(error));

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInfo
    {
        public ulong VolumeSerialNumber;
        public ulong FileIdLow;
        public ulong FileIdHigh;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HandleFileInfo
    {
        public uint FileAttributes;
        public FILETIME CreationTime;
        public FILETIME LastAccessTime;
        public FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int informationClass, out FileIdInfo info, uint size);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out HandleFileInfo info);
}
