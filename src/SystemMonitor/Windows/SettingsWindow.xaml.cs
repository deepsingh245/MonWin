using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SystemMonitor.Native;
using SystemMonitor.ViewModels;

namespace SystemMonitor.Windows;

/// <summary>
/// Shown non-modally (see App.OpenSettings) and dismisses like a Windows 11 flyout —
/// clicking anywhere outside its own bounds closes it, matching the small-utility feel
/// of the rest of the app instead of a traditional modal dialog that only a button can
/// close. Click-outside is detected by comparing the click's screen position against
/// this window's own rect (via a scoped low-level mouse hook) rather than relying on
/// window activation — activation-based detection missed plain desktop/other-app
/// clicks and mis-fired when this app's own overlay window activated itself.
/// </summary>
public partial class SettingsWindow : Window
{
    private bool _suppressClickOutsideClose;
    private MouseHookInterop.LowLevelMouseProc? _mouseProc;
    private nint _hookHandle;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => Close();

        // The native color picker (opened via "Custom...") steals focus/clicks the same
        // way an outside click would, so without this we'd never get to use it.
        viewModel.ExternalDialogOpening += (_, _) => _suppressClickOutsideClose = true;
        viewModel.ExternalDialogClosed += (_, _) => _suppressClickOutsideClose = false;

        Loaded += (_, _) => InstallClickOutsideHook();
        Closed += (_, _) => RemoveClickOutsideHook();
    }

    private void InstallClickOutsideHook()
    {
        _mouseProc = HookCallback;
        _hookHandle = MouseHookInterop.SetWindowsHookEx(MouseHookInterop.WH_MOUSE_LL, _mouseProc, 0, 0);
    }

    private void RemoveClickOutsideHook()
    {
        if (_hookHandle != 0)
        {
            MouseHookInterop.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;
        }

        _mouseProc = null;
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && !_suppressClickOutsideClose &&
            (wParam == MouseHookInterop.WM_LBUTTONDOWN || wParam == MouseHookInterop.WM_RBUTTONDOWN))
        {
            var data = Marshal.PtrToStructure<MouseHookInterop.MsLlHookStruct>(lParam);
            var hwnd = new WindowInteropHelper(this).Handle;

            if (hwnd != 0 && TaskbarInterop.GetWindowRect(hwnd, out var rect))
            {
                var outside = data.Pt.X < rect.Left || data.Pt.X > rect.Right
                    || data.Pt.Y < rect.Top || data.Pt.Y > rect.Bottom;
                if (outside)
                {
                    Dispatcher.BeginInvoke(Close);
                }
            }
        }

        return MouseHookInterop.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}
