using System.Diagnostics;
using System.Text;
using HypixelStatsOverlay.Native;

namespace HypixelStatsOverlay.Services;

public enum AutoWhoResult
{
    Sent,
    WindowNotFound
}

/// <summary>
/// Posts "/who" straight into Minecraft's chat by targeting its window handle directly with
/// PostMessage, rather than simulating hardware-level input via SendInput. Because the messages
/// go straight to that window's own queue, Minecraft never needs OS keyboard focus or to be the
/// foreground window - no focus-stealing, no stolen mouse/keyboard from whatever the player is
/// doing elsewhere. Used by the AutoWho feature to fetch the roster right as a match starts
/// without the player needing to do it manually every game.
/// </summary>
public class AutoWhoService
{
    private const ushort VkS = 0x53;
    private const ushort VkT = 0x54;
    private const ushort VkReturn = 0x0D;

    // Matches vanilla, Badlion, Lunar, Feather, and modded (Forge/Fabric) client window titles -
    // not every client's title actually contains the word "Minecraft".
    private static readonly string[] TitleHints = { "Minecraft", "Badlion", "Lunar Client", "Feather Client" };
    private static readonly string[] ProcessNameHints = { "javaw", "java" };

    /// <summary>Holds "S" for 4 seconds, then types "/who" + Enter into Minecraft's chat - all
    /// posted directly to the game window without touching OS focus.</summary>
    public async Task<AutoWhoResult> SendWhoCommandAsync()
    {
        var hwnd = FindMinecraftWindow();
        if (hwnd == 0)
            return AutoWhoResult.WindowNotFound;

        NativeMethods.PostVirtualKeyDown(hwnd, VkS);
        await Task.Delay(TimeSpan.FromSeconds(4));
        NativeMethods.PostVirtualKeyUp(hwnd, VkS);

        NativeMethods.PostVirtualKey(hwnd, VkT); // opens Minecraft's chat box (default "T" keybind)
        await Task.Delay(150);

        foreach (var c in "/who")
            NativeMethods.PostUnicodeChar(hwnd, c);
        await Task.Delay(150);

        NativeMethods.PostVirtualKey(hwnd, VkReturn); // submits the chat message
        return AutoWhoResult.Sent;
    }

    private static nint FindMinecraftWindow()
    {
        // Most of the time the player is already tabbed into the game when a match starts,
        // so check the foreground window first before doing a full enumeration.
        var foreground = NativeMethods.GetForegroundWindow();
        if (IsMinecraftWindow(foreground))
            return foreground;

        var found = nint.Zero;
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!IsMinecraftWindow(hwnd))
                return true;

            found = hwnd;
            return false; // stop enumerating, we found one
        }, nint.Zero);

        return found;
    }

    private static bool IsMinecraftWindow(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.IsWindowVisible(hwnd))
            return false;

        // The actual game always runs as a javaw/java process, even under Badlion/Lunar/Feather -
        // only the game window itself, never a launcher/hub UI, runs on the JVM. Badlion's own
        // launcher ("Badlion Client.exe") is a separate Electron process whose window title also
        // contains "Badlion", so title alone would wrongly match it; requiring the JVM process
        // check first rules it out.
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            if (!ProcessNameHints.Any(hint => proc.ProcessName.Equals(hint, StringComparison.OrdinalIgnoreCase)))
                return false;
        }
        catch
        {
            return false;
        }

        var length = NativeMethods.GetWindowTextLength(hwnd);
        if (length == 0)
            return false;

        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        var title = sb.ToString();
        return TitleHints.Any(hint => title.Contains(hint, StringComparison.OrdinalIgnoreCase));
    }
}
