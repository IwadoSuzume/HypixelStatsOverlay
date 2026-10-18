using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using HypixelStatsOverlay.Models;

namespace HypixelStatsOverlay.Services;

public class HypixelApiService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(3);

    private readonly HttpClient _http;
    private readonly MojangApiService _mojang;
    private readonly SemaphoreSlim _throttle = new(4, 4);
    private readonly ConcurrentDictionary<string, (DateTime FetchedAt, PlayerStats Stats)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public HypixelApiService(HttpClient http, MojangApiService mojang)
    {
        _http = http;
        _mojang = mojang;
    }

    public async Task<PlayerStats> GetStatsAsync(string username, string apiKey, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(username, out var cached) && DateTime.UtcNow - cached.FetchedAt < CacheTtl)
            return cached.Stats;

        await _throttle.WaitAsync(ct);
        try
        {
            // Another concurrent request may have populated the cache while we waited.
            if (_cache.TryGetValue(username, out cached) && DateTime.UtcNow - cached.FetchedAt < CacheTtl)
                return cached.Stats;

            var stats = await FetchAsync(username, apiKey, ct);
            _cache[username] = (DateTime.UtcNow, stats);
            return stats;
        }
        finally
        {
            _throttle.Release();
        }
    }

    private async Task<PlayerStats> FetchAsync(string username, string apiKey, CancellationToken ct)
    {
        string? uuid;
        try
        {
            uuid = await _mojang.GetUuidAsync(username, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return PlayerStats.Error(username, Loc.T("Api_MojangTimeout"));
        }
        catch (Exception ex)
        {
            return PlayerStats.Error(username, Loc.T("Api_UuidLookupFailed", HttpRetry.Describe(ex)));
        }

        if (uuid == null)
            return PlayerStats.Nicked(username);

        var url = $"https://api.hypixel.net/v2/player?key={Uri.EscapeDataString(apiKey)}&uuid={Uri.EscapeDataString(uuid)}";

        HttpResponseMessage resp;
        try
        {
            resp = await HttpRetry.ExecuteAsync(() => _http.GetAsync(url, ct), ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return PlayerStats.Error(username, Loc.T("Api_HypixelTimeout"));
        }
        catch (HttpRequestException ex)
        {
            return PlayerStats.Error(username, Loc.T("Api_ConnectionFailed", HttpRetry.Describe(ex)));
        }

        using (resp)
        {
            if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                return PlayerStats.Error(username, Loc.T("Api_TooManyRequests"));

            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden || resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // A real "bad key" response from Hypixel is JSON ({"success":false,"cause":"..."}).
                // Proxy/VPN exit IPs that Cloudflare's WAF has flagged instead get an HTML challenge
                // page back with the same status code, so the body shape tells them apart.
                var body = await resp.Content.ReadAsStringAsync(ct);
                var snippet = body.Length > 200 ? body[..200] : body;
                return PlayerStats.Error(username, Loc.T("Api_InvalidKey", (int)resp.StatusCode, snippet));
            }

            if (!resp.IsSuccessStatusCode)
                return PlayerStats.Error(username, Loc.T("Api_HttpError", (int)resp.StatusCode));

            try
            {
                var stats = await ParsePlayerAsync(resp, username, uuid, ct);
                await AttachGuildTagAsync(stats, uuid, apiKey, ct);
                return stats;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return PlayerStats.Error(username, Loc.T("Api_HypixelTimeout"));
            }
        }
    }

    /// <summary>Guild tag is a "nice to have" on the stat card - any failure here (network, no guild,
    /// rate limit) just leaves the tag blank rather than failing the whole stats lookup.</summary>
    private async Task AttachGuildTagAsync(PlayerStats stats, string uuid, string apiKey, CancellationToken ct)
    {
        if (stats.IsSpecialStatus)
            return;

        try
        {
            var url = $"https://api.hypixel.net/v2/guild?key={Uri.EscapeDataString(apiKey)}&player={Uri.EscapeDataString(uuid)}";
            using var resp = await HttpRetry.ExecuteAsync(() => _http.GetAsync(url, ct), ct);
            if (!resp.IsSuccessStatusCode)
                return;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
                return;
            if (!root.TryGetProperty("guild", out var guild) || guild.ValueKind == JsonValueKind.Null)
                return; // Player isn't in a guild.

            var tag = GetString(guild, "tag");
            if (string.IsNullOrEmpty(tag))
                return;

            stats.GuildTagDisplay = $"[{tag}]";
            stats.GuildTagColorHex = McColor.FromName(GetString(guild, "tagColor")) ?? McColor.White;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Cosmetic only - swallow and leave GuildTagDisplay empty.
        }
    }

    private async Task<PlayerStats> ParsePlayerAsync(HttpResponseMessage resp, string username, string uuid, CancellationToken ct)
    {
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        if (!root.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
            return PlayerStats.Error(username, Loc.T("Api_QueryFailed"));

        if (!root.TryGetProperty("player", out var player) || player.ValueKind == JsonValueKind.Null)
            return PlayerStats.Error(username, Loc.T("Api_NeverLoggedIn"));

        double wins = 0, losses = 0, finalKills = 0, finalDeaths = 0, bedsBroken = 0, bedsLost = 0, kills = 0, deaths = 0;
        double experience = 0, coins = 0;
        int? winStreak = null;
        JsonElement bw = default;
        var hasBedwars = false;
        if (player.TryGetProperty("stats", out var statsEl) &&
            statsEl.TryGetProperty("Bedwars", out bw))
        {
            hasBedwars = true;
            wins = GetDouble(bw, "wins_bedwars");
            losses = GetDouble(bw, "losses_bedwars");
            finalKills = GetDouble(bw, "final_kills_bedwars");
            finalDeaths = GetDouble(bw, "final_deaths_bedwars");
            bedsBroken = GetDouble(bw, "beds_broken_bedwars");
            bedsLost = GetDouble(bw, "beds_lost_bedwars");
            kills = GetDouble(bw, "kills_bedwars");
            deaths = GetDouble(bw, "deaths_bedwars");
            experience = GetDouble(bw, "Experience");
            coins = GetDouble(bw, "coins");
            // Missing (not zero) when the player has winstreak hidden in their API settings.
            winStreak = GetNullableInt(bw, "winstreak");
        }

        var fkdr = finalDeaths <= 0 ? finalKills : finalKills / finalDeaths;
        var wlr = losses <= 0 ? wins : wins / losses;
        var bblr = bedsLost <= 0 ? bedsBroken : bedsBroken / bedsLost;
        var kdr = deaths <= 0 ? kills : kills / deaths;
        var (level, levelExp, levelExpNeeded) = GetLevelProgress(experience);
        var mostPlayedMode = hasBedwars ? GetMostPlayedMode(bw) : "-";

        var displayName = player.TryGetProperty("displayname", out var dn) ? dn.GetString() ?? username : username;
        var rank = GetRank(player);

        return new PlayerStats
        {
            Name = displayName,
            BedwarsLevel = level,
            CurrentLevelExp = levelExp,
            CurrentLevelExpNeeded = levelExpNeeded,
            Fkdr = Math.Round(fkdr, 2),
            Wins = (int)wins,
            Losses = (int)losses,
            Wlr = Math.Round(wlr, 2),
            BedsBroken = (int)bedsBroken,
            BedsLost = (int)bedsLost,
            Bblr = Math.Round(bblr, 2),
            FinalKills = (int)finalKills,
            FinalDeaths = (int)finalDeaths,
            Kills = (int)kills,
            Deaths = (int)deaths,
            Kdr = Math.Round(kdr, 2),
            Coins = (long)coins,
            MostPlayedMode = mostPlayedMode,
            WinStreak = winStreak,
            RankPrefix = rank.Prefix,
            RankPlus = rank.Plus,
            RankSuffix = rank.Suffix,
            RankColorHex = rank.ColorHex,
            RankPlusColorHex = rank.PlusColorHex
        };
    }

    /// <summary>
    /// Converts raw Bedwars XP into (star level, xp earned toward next level, xp that level costs),
    /// reproducing Hypixel's own leveling curve: each 100-level "prestige" costs 500/1000/2000/3500 xp
    /// for levels 1-4, then a flat 5000 xp for every level after that (487,000 xp per prestige total).
    /// Deriving the level from raw XP (rather than trusting achievements.bedwars_level) keeps the star
    /// number and the progress-bar fraction guaranteed consistent with each other.
    /// </summary>
    private static (int Level, double CurrentExp, double ExpNeeded) GetLevelProgress(double totalExp)
    {
        const int levelsPerPrestige = 100;
        const int xpPerNormalLevel = 5000;
        int[] easyLevelCosts = { 500, 1000, 2000, 3500 };
        var xpPerPrestige = easyLevelCosts.Sum() + (levelsPerPrestige - easyLevelCosts.Length) * xpPerNormalLevel;

        var prestige = (int)(totalExp / xpPerPrestige);
        var expInPrestige = totalExp - prestige * (double)xpPerPrestige;
        var level = prestige * levelsPerPrestige;

        var easyLevelTotal = easyLevelCosts.Sum();
        if (expInPrestige < easyLevelTotal)
        {
            var i = 0;
            while (i < easyLevelCosts.Length && expInPrestige >= easyLevelCosts[i])
            {
                expInPrestige -= easyLevelCosts[i];
                level++;
                i++;
            }

            var needed = i < easyLevelCosts.Length ? easyLevelCosts[i] : xpPerNormalLevel;
            return (level, expInPrestige, needed);
        }

        expInPrestige -= easyLevelTotal;
        level += easyLevelCosts.Length + (int)(expInPrestige / xpPerNormalLevel);
        var remExp = expInPrestige % xpPerNormalLevel;
        return (level, remExp, xpPerNormalLevel);
    }

    private static readonly (string Prefix, string Label)[] BedwarsModes =
    {
        ("eight_one_", "Solo"),
        ("eight_two_", "Doubles"),
        ("four_three_", "Threes"),
        ("four_four_", "Fours")
    };

    /// <summary>Whichever mode (solo/doubles/threes/fours) has the most wins+losses combined. Always
    /// English, regardless of UI language - these are Hypixel's own mode names, not translated UI text.</summary>
    private static string GetMostPlayedMode(JsonElement bw)
    {
        string? best = null;
        double bestGames = 0;
        foreach (var (prefix, label) in BedwarsModes)
        {
            var games = GetDouble(bw, $"{prefix}wins_bedwars") + GetDouble(bw, $"{prefix}losses_bedwars");
            if (games > bestGames)
            {
                bestGames = games;
                best = label;
            }
        }

        return best ?? "-";
    }

    /// <summary>
    /// Reproduces Hypixel's own rank precedence (staff > MVP++ > MVP+ > MVP > VIP+ > VIP > default)
    /// and chat colors, so the overlay's rank tag matches what shows in-game. The "+"/"++" is split
    /// out separately because Hypixel lets MVP+ players recolor just the plus via /rankcolor (and
    /// MVP++ players recolor the whole tag via /monthlycolor) - it isn't always the same color as
    /// the surrounding brackets.
    /// </summary>
    private static (string Prefix, string Plus, string Suffix, string ColorHex, string PlusColorHex) GetRank(JsonElement player)
    {
        var staffRank = GetString(player, "rank");
        if (!string.IsNullOrEmpty(staffRank) && staffRank != "NORMAL" && staffRank != "NONE")
        {
            var (tag, color) = staffRank switch
            {
                "OWNER" => ("[OWNER]", McColor.DarkRed),
                "ADMIN" => ("[ADMIN]", McColor.Red),
                "GAME_MASTER" => ("[GM]", McColor.DarkGreen),
                "MODERATOR" => ("[MOD]", McColor.DarkGreen),
                "HELPER" => ("[HELPER]", McColor.Blue),
                "YOUTUBER" => ("[YOUTUBE]", McColor.Red),
                _ => ($"[{staffRank}]", McColor.Red)
            };
            return (tag, string.Empty, string.Empty, color, color);
        }

        var monthlyPackageRank = GetString(player, "monthlyPackageRank");
        if (monthlyPackageRank == "SUPERSTAR")
        {
            // MVP++ is styled as one uniform color via /monthlycolor; unlike MVP+ it doesn't get a
            // separately-colored plus.
            var color = McColor.FromName(GetString(player, "monthlyRankColor")) ?? McColor.Gold;
            return ("[MVP++]", string.Empty, string.Empty, color, color);
        }

        var packageRank = GetString(player, "newPackageRank") ?? GetString(player, "packageRank");
        return packageRank switch
        {
            "MVP_PLUS" => ("[MVP", "+", "]", McColor.Aqua, McColor.FromName(GetString(player, "rankPlusColor")) ?? McColor.Gold),
            "MVP" => ("[MVP]", string.Empty, string.Empty, McColor.Aqua, McColor.Aqua),
            // VIP+'s plus isn't player-customizable on Hypixel; it's always gold.
            "VIP_PLUS" => ("[VIP", "+", "]", McColor.Green, McColor.Gold),
            "VIP" => ("[VIP]", string.Empty, string.Empty, McColor.Green, McColor.Green),
            _ => (string.Empty, string.Empty, string.Empty, McColor.Gray, McColor.Gray)
        };
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static double GetDouble(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
                return 0;
        }

        return current.ValueKind switch
        {
            JsonValueKind.Number => current.GetDouble(),
            _ => 0
        };
    }

    /// <summary>Unlike GetDouble, distinguishes "field absent" (null) from "field present and 0".</summary>
    private static int? GetNullableInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? (int)prop.GetDouble()
            : null;
}
