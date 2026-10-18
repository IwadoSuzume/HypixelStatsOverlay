using System.IO;

namespace HypixelStatsOverlay.Models;

public class AppConfig
{
    public string HypixelApiKey { get; set; } = string.Empty;

    /// <summary>Local player's Minecraft IGN; their row is pinned to the top of the overlay.</summary>
    public string OwnUsername { get; set; } = string.Empty;

    /// <summary>Background opacity of the floating stats overlay: 0.2 (very see-through) to 1.0 (solid).</summary>
    public double OverlayOpacity { get; set; } = 0.8;

    /// <summary>Zoom factor applied to the floating stats overlay, adjustable by dragging its corner handle in preview mode.</summary>
    public double OverlayScale { get; set; } = 1.0;

    /// <summary>AutoWho: when true, automatically types "/who" into Minecraft's chat 4 seconds after
    /// each Bedwars match starts, instead of requiring the player to do it manually.</summary>
    public bool AutoWhoEnabled { get; set; }

    public string MinecraftLogPath { get; set; } = DetectDefaultLogPath();

    /// <summary>UI language: "en" or "zh".</summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Badlion Client nests the actual game log under logs/blclient/minecraft
    /// instead of writing straight to .minecraft/logs like vanilla, so prefer
    /// that path when it exists.
    /// </summary>
    private static string DetectDefaultLogPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var minecraftLogs = Path.Combine(appData, ".minecraft", "logs");

        var badlionPath = Path.Combine(minecraftLogs, "blclient", "minecraft", "latest.log");
        if (File.Exists(badlionPath))
            return badlionPath;

        return Path.Combine(minecraftLogs, "latest.log");
    }
}
