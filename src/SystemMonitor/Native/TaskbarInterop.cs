using System.Runtime.InteropServices;

namespace SystemMonitor.Native;

/// <summary>
/// P/Invoke surface for locating the taskbar and controlling overlay window styles.
/// All functions are documented public Win32 APIs (user32.dll/shell32.dll) — no Explorer
/// hooking, no injection: we only read taskbar geometry and set styles on our own window.
/// </summary>
internal static partial class TaskbarInterop
{
    internal const int GWL_EXSTYLE = -20;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_NOACTIVATE = 0x08000000;
    internal const int WS_EX_TRANSPARENT = 0x00000020;
    internal const int WS_EX_LAYERED = 0x00080000;

    internal static readonly nint HWND_TOPMOST = new(-1);
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_SHOWWINDOW = 0x0040;

    internal const int ABM_GETTASKBARPOS = 0x00000005;
    internal const int ABM_GETSTATE = 0x00000004;
    internal const int ABM_GETAUTOHIDEBAREX = 0x0000000b;
    internal const int ABS_AUTOHIDE = 0x0000001;

    internal const int ABE_LEFT = 0;
    internal const int ABE_TOP = 1;
    internal const int ABE_RIGHT = 2;
    internal const int ABE_BOTTOM = 3;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct AppBarData
    {
        public uint CbSize;
        public nint HWnd;
        public uint UCallbackMessage;
        public uint UEdge;
        public Rect Rc;
        public nint LParam;
    }

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint FindWindow(string lpClassName, string? lpWindowName);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint hWnd, out Rect lpRect);

    [LibraryImport("shell32.dll")]
    internal static partial nint SHAppBarMessage(uint dwMessage, ref AppBarData pData);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint RegisterWindowMessage(string lpString);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    internal static partial int GetWindowLong32(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    internal static partial int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial nint GetWindowLongPtr64(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    /// <summary>Wraps the 32/64-bit GetWindowLong split so callers don't need to care about pointer size.</summary>
    internal static nint GetWindowLongPtr(nint hWnd, int nIndex) =>
        nint.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);

    internal static nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong) =>
        nint.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, (int)dwNewLong);
}
