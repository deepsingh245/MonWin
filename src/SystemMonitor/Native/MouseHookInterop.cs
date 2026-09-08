using System.Runtime.InteropServices;

namespace SystemMonitor.Native;

/// <summary>
/// Minimal WH_MOUSE_LL (low-level mouse hook) wrapper used only to implement
/// click-outside-to-dismiss for the Settings flyout. Scoped to a single window's
/// lifetime (installed on open, removed on close) — this is a standard, documented
/// user-mode mechanism (no driver, no injection into other processes; the hook runs on
/// this process's own UI thread and only observes coordinates, never other apps' data).
/// </summary>
internal static class MouseHookInterop
{
    internal const int WH_MOUSE_LL = 14;
    internal const int WM_LBUTTONDOWN = 0x0201;
    internal const int WM_RBUTTONDOWN = 0x0204;

    internal delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct PointL
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MsLlHookStruct
    {
        public PointL Pt;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint DwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    internal static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);
}
