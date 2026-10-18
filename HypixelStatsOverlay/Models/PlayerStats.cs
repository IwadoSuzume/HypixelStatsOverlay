using HypixelStatsOverlay.Services;

namespace HypixelStatsOverlay.Models;

public class PlayerStats
{
    public string Name { get; set; } = string.Empty;

    /// <summary>"[TAG]" if the player is in a guild, empty otherwise - shown in its own overlay
    /// column (last column, after all stats), pre-formatted so the XAML can bind it directly with
    /// no converter needed.</summary>
    public string GuildTagDisplay { get; set; } = string.Empty;

    /// <summary>Matches the guild's actual tag color in-game - Hypixel colors the whole "[TAG]" unit,
    /// brackets included, as one color (unlike the rank prefix/plus, which can differ).</summary>
    public string GuildTagColorHex { get; set; } = "#AAAAAA";

    public double BedwarsLevel { get; set; }
    public double NextLevel => BedwarsLevel + 1;

    /// <summary>XP earned so far toward NextLevel, and the total XP that level costs - drives the
    /// progress bar between the two star badges, mirroring the reference stat-card design.</summary>
    public double CurrentLevelExp { get; set; }
    public double CurrentLevelExpNeeded { get; set; } = 1;
    public double LevelProgress => CurrentLevelExpNeeded <= 0 ? 0 : Math.Clamp(CurrentLevelExp / CurrentLevelExpNeeded, 0, 1);
    public string LevelProgressText =>
        $"{CurrentLevelExp:N0} / {CurrentLevelExpNeeded:N0} xp ({LevelProgress:0%})";

    public double Fkdr { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public double Wlr { get; set; }
    public int BedsBroken { get; set; }
    public int BedsLost { get; set; }
    public double Bblr { get; set; }
    public int FinalKills { get; set; }
    public int FinalDeaths { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public double Kdr { get; set; }

    public int Games => Wins + Losses;
    public long Coins { get; set; }

    /// <summary>Which mode (solo/doubles/threes/fours) has the most games played, by wins+losses. "-"
    /// when the player has no games in any of the tracked modes.</summary>
    public string MostPlayedMode { get; set; } = "-";

    /// <summary>Null when the player has winstreak hidden in their Hypixel API settings, rather than an actual 0 streak.</summary>
    public int? WinStreak { get; set; }

    public string WinStreakText => WinStreak?.ToString() ?? "-";

    // Hypixel renders the "+"/"++" in a rank tag with a separately customizable color
    // (e.g. "[MVP" aqua + "+" gold + "]" aqua), so the tag is split into three segments
    // rather than one solid-colored string.
    public string RankPrefix { get; set; } = string.Empty;
    public string RankPlus { get; set; } = string.Empty;
    public string RankSuffix { get; set; } = string.Empty;

    /// <summary>Hex color for RankPrefix/RankSuffix, matching the rank's in-game chat color, e.g. "#55FFFF" for MVP.</summary>
    public string RankColorHex { get; set; } = "#AAAAAA";

    /// <summary>Hex color for RankPlus specifically, e.g. the player's chosen /rankcolor.</summary>
    public string RankPlusColorHex { get; set; } = "#AAAAAA";

    /// <summary>True when this row is the local user, so it can be pinned to the top and highlighted.</summary>
    public bool IsOwnPlayer { get; set; }

    /// <summary>True when no Mojang account matches this name - almost always means a Bedwars nick plugin
    /// is masking the player's real IGN, rather than an actual lookup failure.</summary>
    public bool IsNicked { get; set; }

    public string? ErrorMessage { get; set; }
    public bool IsLoading { get; set; }

    /// <summary>True when the row should show a single status message instead of stat columns.</summary>
    public bool IsSpecialStatus => IsLoading || ErrorMessage != null;

    public string StatusText => IsLoading ? Loc.T("CardStatus_Loading") : IsNicked ? Loc.T("Overlay_NickedStatus") : (ErrorMessage ?? string.Empty);

    /// <summary>Same as <see cref="StatusText"/> but hardcoded to English, for the in-game overlay
    /// specifically - unlike the Stat Card tab, the overlay must never show Chinese, and ErrorMessage
    /// is already baked in whatever language was active when the lookup failed so it can't be
    /// re-translated after the fact.</summary>
    public string StatusTextEnglish =>
        IsLoading ? "Loading..." : IsNicked ? "Nicked (hidden name)" : ErrorMessage != null ? "Lookup failed" : string.Empty;

    public static PlayerStats Loading(string name) => new() { Name = name, IsLoading = true };

    public static PlayerStats Error(string name, string message) =>
        new() { Name = name, ErrorMessage = message };

    public static PlayerStats Nicked(string name) =>
        new() { Name = name, IsNicked = true, ErrorMessage = "NICK" };
}
