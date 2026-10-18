namespace HypixelStatsOverlay.Models;

/// <summary>One entry in the "select a window" picker for the borderless/fullscreen tool.</summary>
public class WindowEntry
{
    public nint Handle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? ProcessName : $"{Title} ({ProcessName})";
}
