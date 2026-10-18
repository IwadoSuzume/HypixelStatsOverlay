namespace HypixelStatsOverlay.Services;

/// <summary>Minecraft chat color names mapped to their exact hex values, for matching in-game rank colors.</summary>
public static class McColor
{
    public const string Black = "#000000";
    public const string DarkBlue = "#0000AA";
    public const string DarkGreen = "#00AA00";
    public const string DarkAqua = "#00AAAA";
    public const string DarkRed = "#AA0000";
    public const string DarkPurple = "#AA00AA";
    public const string Gold = "#FFAA00";
    public const string Gray = "#AAAAAA";
    public const string DarkGray = "#555555";
    public const string Blue = "#5555FF";
    public const string Green = "#55FF55";
    public const string Aqua = "#55FFFF";
    public const string Red = "#FF5555";
    public const string LightPurple = "#FF55FF";
    public const string Yellow = "#FFFF55";
    public const string White = "#FFFFFF";

    /// <summary>Resolves a Hypixel API color name (e.g. "GOLD", "LIGHT_PURPLE") to its hex value, or null if unknown.</summary>
    public static string? FromName(string? name) => name switch
    {
        "BLACK" => Black,
        "DARK_BLUE" => DarkBlue,
        "DARK_GREEN" => DarkGreen,
        "DARK_AQUA" => DarkAqua,
        "DARK_RED" => DarkRed,
        "DARK_PURPLE" => DarkPurple,
        "GOLD" => Gold,
        "GRAY" => Gray,
        "DARK_GRAY" => DarkGray,
        "BLUE" => Blue,
        "GREEN" => Green,
        "AQUA" => Aqua,
        "RED" => Red,
        "LIGHT_PURPLE" => LightPurple,
        "YELLOW" => Yellow,
        "WHITE" => White,
        _ => null
    };
}
