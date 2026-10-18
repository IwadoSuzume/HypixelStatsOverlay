using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HypixelStatsOverlay.Native;

namespace HypixelStatsOverlay.Services;

/// <summary>
/// Global low-level keyboard hook that reports Tab press/release, but only
/// while a Minecraft window is in the foreground.
/// </summary>
public class KeyboardHookService : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private nint _hookHandle;
    private bool _tabDown;

    public event Action? TabPressed;
    public event Action? TabReleased;

    public KeyboardHookService()
    {
        // Field, not local: must outlive the hook or the GC can collect the
        // delegate while Windows still holds a native pointer to it.
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookHandle != 0) return;

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _proc,
            NativeMethods.GetModuleHandle(curModule.ModuleName!),
            0);
    }

    public void Stop()
    {
        if (_hookHandle == 0) return;
        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = 0;
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var vkCode = Marshal.ReadInt32(lParam);
            if (vkCode == NativeMethods.VK_TAB)
            {
                if (wParam == NativeMethods.WM_KEYDOWN && !_tabDown)
                {
                    if (IsMinecraftForeground())
                    {
                        _tabDown = true;
                        TabPressed?.Invoke();
                    }
                }
                else if (wParam == NativeMethods.WM_KEYUP && _tabDown)
                {
                    _tabDown = false;
                    TabReleased?.Invoke();
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static bool IsMinecraftForeground()
    {
        var hWnd = NativeMethods.GetForegroundWindow();
        if (hWnd == 0) return false;

        var title = new StringBuilder(256);
        NativeMethods.GetWindowText(hWnd, title, title.Capacity);
        if (title.ToString().Contains("Minecraft", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName.Equals("javaw", StringComparison.OrdinalIgnoreCase) ||
                   proc.ProcessName.Equals("java", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose() => Stop();
}
