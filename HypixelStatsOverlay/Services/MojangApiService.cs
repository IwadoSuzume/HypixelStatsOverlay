using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace HypixelStatsOverlay.Services;

public class MojangApiService
{
    private readonly HttpClient _http;

    // Username -> UUID essentially never changes, so unlike HypixelApiService's stats cache (which
    // needs a short TTL - stats change every game) this one just lives for the process lifetime. Every
    // repeat opponent across a play session then resolves for free with no extra API round trip.
    private readonly ConcurrentDictionary<string, string> _uuidCache = new(StringComparer.OrdinalIgnoreCase);

    public MojangApiService(HttpClient http)
    {
        _http = http;
    }

    /// <summary>Resolves a Minecraft username to its (dashless) UUID, or null if no such account exists.
    /// Hits Mojang's own API directly first - the same approach the PCL launcher uses - falling back to
    /// playerdb.co's proxy only if that fails outright (timeout, TLS handshake failure, etc.), since on
    /// a rough network route one of the two tends to work when the other doesn't.</summary>
    public async Task<string?> GetUuidAsync(string username, CancellationToken ct = default)
    {
        if (_uuidCache.TryGetValue(username, out var cachedUuid))
            return cachedUuid;

        string? uuid;
        try
        {
            uuid = await FetchFromMojangAsync(username, ct);
        }
        catch (Exception mojangEx) when (!ct.IsCancellationRequested)
        {
            try
            {
                uuid = await FetchFromPlayerDbAsync(username, ct);
            }
            catch (Exception playerDbEx) when (!ct.IsCancellationRequested)
            {
                throw new Exception(
                    $"api.mojang.com: {HttpRetry.Describe(mojangEx)}; playerdb.co fallback: {HttpRetry.Describe(playerDbEx)}");
            }
        }

        if (uuid != null)
            _uuidCache[username] = uuid;

        return uuid;
    }

    private async Task<string?> FetchFromMojangAsync(string username, CancellationToken ct)
    {
        var url = $"https://api.mojang.com/users/profiles/minecraft/{Uri.EscapeDataString(username)}";
        using var resp = await HttpRetry.ExecuteAsync(() => _http.GetAsync(url, ct), ct);

        // Mojang answers "no such player" with 404, and a malformed/too-long name with 400 - both
        // mean "treat this as an unresolvable (possibly nicked) name" rather than a hard error.
        if (resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            return null;

        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        return doc.RootElement.GetProperty("id").GetString();
    }

    private async Task<string?> FetchFromPlayerDbAsync(string username, CancellationToken ct)
    {
        var url = $"https://playerdb.co/api/player/minecraft/{Uri.EscapeDataString(username)}";
        using var resp = await HttpRetry.ExecuteAsync(() => _http.GetAsync(url, ct), ct);

        // playerdb answers "no such player" with 400 (not 404), alongside a success:false body.
        if (resp.StatusCode == HttpStatusCode.BadRequest)
            return null;

        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        if (!root.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
            return null;

        return root.GetProperty("data").GetProperty("player").GetProperty("raw_id").GetString();
    }
}
