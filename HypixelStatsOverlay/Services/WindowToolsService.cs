using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HypixelStatsOverlay.Models;
using HypixelStatsOverlay.Native;

namespace HypixelStatsOverlay.Services;

/// <summary>
/// Generic window utility: lists other apps' top-level windows and can strip their title
/// bar/border (optionally stretching them to fill the monitor) - the same trick tools like
/// "Borderless Gaming" use to make a windowed app behave like fullscreen-borderless.
/// </summary>
public class WindowToolsService
{
    // Style + bounds captured right before the FIRST borderless/fullscreen change to a given
    // window, so "restore" can undo back to how it originally looked even after several tweaks
    // (e.g. borderless, then fullscreen) rather than just the most recent one.
    private readonly Dictionary<nint, (long Style, NativeMethods.Rect Bounds)> _originalStates = new();

    /// <summary>Lists visible, titled top-level windows belonging to other processes.</summary>
    public List<WindowEntry> EnumerateWindows()
    {
        var results = new List<WindowEntry>();
        var selfPid = Environment.ProcessId;

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd))
                return true;

            var length = NativeMethods.GetWindowTextLength(hwnd);
            if (length == 0)
                return true;

            var sb = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
            var title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title))
                return true;

            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == selfPid)
                return true; // don't offer this app's own windows as a target

            try
            {
                using var proc = Process.GetProcessById((int)pid);
                results.Add(new WindowEntry { Handle = hwnd, Title = title, ProcessName = proc.ProcessName });
            }
            catch
            {
                // Process exited between enumeration and lookup, or access denied; skip it.
            }

            return true;
        }, nint.Zero);

        return results;
    }

    /// <summary>Strips the title bar/border but leaves the window's current size and position alone.</summary>
    public bool MakeBorderless(nint hwnd)
    {
        try
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            CaptureOriginalState(hwnd);
            StripChrome(hwnd);
            return NativeMethods.SetWindowPos(hwnd, nint.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Strips the title bar/border and stretches the window to exactly cover the monitor it's on.</summary>
    public bool MakeBorderlessFullscreen(nint hwnd)
    {
        try
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            CaptureOriginalState(hwnd);
            StripChrome(hwnd);

            var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MonitorInfo { cbSize = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
            if (!NativeMethods.GetMonitorInfo(monitor, ref info))
                return false;

            var bounds = info.rcMonitor;
            return NativeMethods.SetWindowPos(hwnd, nint.Zero, bounds.Left, bounds.Top,
                bounds.Right - bounds.Left, bounds.Bottom - bounds.Top,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Puts a window back to its original title bar/border, size, and position - undoing whatever
    /// combination of MakeBorderless/MakeBorderlessFullscreen was applied to it. Returns false if
    /// this window was never modified (nothing to restore) or the handle is no longer valid.
    /// </summary>
    public bool RestoreWindow(nint hwnd)
    {
        if (!_originalStates.TryGetValue(hwnd, out var original))
            return false;

        try
        {
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, (nint)original.Style);
            var r = original.Bounds;
            var ok = NativeMethods.SetWindowPos(hwnd, nint.Zero, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
            _originalStates.Remove(hwnd);
            return ok;
        }
        catch
        {
            _originalStates.Remove(hwnd);
            return false;
        }
    }

    /// <summary>Removes the caption bar, resize border, and system menu/min/max box styles.</summary>
    private static void StripChrome(nint hwnd)
    {
        var style = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE);
        style &= ~(NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME | NativeMethods.WS_MINIMIZEBOX |
                   NativeMethods.WS_MAXIMIZEBOX | NativeMethods.WS_SYSMENU);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, (nint)style);
    }

    /// <summary>Records a window's pre-modification style/bounds, but only the first time it's
    /// touched - a second borderless/fullscreen call on an already-modified window must not
    /// overwrite this with the (already altered) current state.</summary>
    private void CaptureOriginalState(nint hwnd)
    {
        if (_originalStates.ContainsKey(hwnd))
            return;

        var style = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE);
        NativeMethods.GetWindowRect(hwnd, out var bounds);
        _originalStates[hwnd] = (style, bounds);
    }
}
