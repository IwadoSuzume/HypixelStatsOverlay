using System.Net.Http;

namespace HypixelStatsOverlay.Services;

/// <summary>Retries a transient HTTP failure (timeout/connection reset) a couple of times with a
/// short backoff before giving up - covers intermittent packet loss on a degraded route without every
/// caller needing its own retry loop. Only retries timeouts/connection failures, never a cancellation
/// the caller actually asked for.</summary>
internal static class HttpRetry
{
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct, int maxAttempts = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < maxAttempts && !ct.IsCancellationRequested &&
                                        ex is OperationCanceledException or HttpRequestException)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
            }
        }
    }

    /// <summary>.NET's HttpRequestException/AuthenticationException messages (e.g. "The SSL connection
    /// could not be established, see inner exception") are useless on their own - the actual cause is
    /// always one level deeper. Walks the whole InnerException chain so error text shown to the user is
    /// actually diagnosable.</summary>
    public static string Describe(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e != null; e = e.InnerException)
            parts.Add(e.Message);
        return string.Join(" -> ", parts);
    }
}
