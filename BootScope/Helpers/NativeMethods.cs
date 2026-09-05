using System.Runtime.InteropServices;

namespace BootScope.Helpers;

/// <summary>
/// Thin wrapper around the Win32 GetProcessIoCounters API, used to read per-process disk I/O
/// byte counters. This is a standard Windows API (kernel32.dll) rather than a third-party
/// dependency, and it is the only reliable way to get per-process disk throughput without
/// pulling in a heavier WMI/ETW dependency.
/// </summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProcessIoCounters(IntPtr processHandle, out IO_COUNTERS ioCounters);

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    /// <summary>Returns total and available physical memory in megabytes, or (0, 0) if unavailable.</summary>
    public static (double totalMb, double availableMb) GetPhysicalMemoryMb()
    {
        try
        {
            var status = new MEMORYSTATUSEX();
            status.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
            if (GlobalMemoryStatusEx(ref status))
            {
                return (status.ullTotalPhys / 1024.0 / 1024.0, status.ullAvailPhys / 1024.0 / 1024.0);
            }
        }
        catch (Exception)
        {
            // Fall through to zeroed defaults below.
        }

        return (0, 0);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPTStr)] public string lpVerb;
        [MarshalAs(UnmanagedType.LPTStr)] public string lpFile;
        [MarshalAs(UnmanagedType.LPTStr)] public string? lpParameters;
        [MarshalAs(UnmanagedType.LPTStr)] public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [MarshalAs(UnmanagedType.LPTStr)] public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    /// <summary>Shows the standard Windows shell "Properties" dialog for the given file path.</summary>
    public static void ShowFileProperties(string filePath)
    {
        var info = new SHELLEXECUTEINFO();
        info.cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>();
        info.lpVerb = "properties";
        info.lpFile = filePath;
        info.nShow = 1; // SW_SHOWNORMAL
        info.fMask = SEE_MASK_INVOKEIDLIST;
        ShellExecuteEx(ref info);
    }
}
