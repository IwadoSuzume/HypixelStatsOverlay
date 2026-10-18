using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace HypixelStatsOverlay.Services;

/// <summary>
/// Tails the Minecraft client log and extracts the player roster whenever the
/// user runs "/who" in-game (Hypixel prints an "ONLINE: a, b, c" chat line).
/// </summary>
public class LogWatcherService : IDisposable
{
    private static readonly Regex OnlineLineRegex = new(@"ONLINE:\s*(?<names>.+)\s*$", RegexOptions.Compiled);

    // Hypixel Bedwars final-kill chat lines are center-padded with spaces and always
    // start with the victim's name, e.g. "     Steve was killed by Alex. FINAL KILL!"
    private static readonly Regex FinalKillVictimRegex = new(@"^(?<victim>[A-Za-z0-9_]{1,16})\b", RegexOptions.Compiled);
    private static readonly Regex FormattingCodeRegex = new(@"§.", RegexOptions.Compiled);

    // The vanilla client logs every server switch on the Bungee network (leaving a game back to a
    // lobby, queueing into the next mini-lobby, etc.) as a non-chat "Connecting to host, port" line -
    // a reliable "no longer in that match" signal independent of any chat wording.
    private static readonly Regex ServerConnectRegex = new(@"^Connecting to ", RegexOptions.Compiled);

    // Hypixel prints this in the pre-game lobby countdown, exactly 1 second before the match
    // actually starts (their countdown announcer only fires at 10/5/4/3/2/1, and doesn't bother
    // pluralizing "second" - the literal text really is "1 seconds!") - used as the AutoWho trigger.
    private const string AutoWhoTriggerText = "The game starts in 1 seconds!";

    // Hypixel's server-transfer message, e.g. "Sending you to mini183DH!" - the mini-lobby/server name
    // in the middle is randomly generated each time and deliberately left out of the match here.
    private static readonly Regex SendingToRegex = new(@"^Sending you to .+!$", RegexOptions.Compiled);

    // Vanilla logs the player's own outgoing commands as "Issued server command: /whatever" - used to
    // detect the player manually returning to a lobby via /l, /L, or /lobby.
    private static readonly Regex LobbyCommandRegex = new(
        @"^Issued server command:\s*/(l|lobby)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Vanilla logs received chat messages with a literal "[CHAT] " tag baked into the message text
    // itself (e.g. "[22:15:01] [Client thread/INFO]: [CHAT] Bed Wars") - on top of the usual
    // "[HH:mm:ss] [Thread/LEVEL]: " prefix already stripped below. Anchored regexes like
    // SendingToRegex/LobbyCommandRegex/FinalKillVictimRegex never matched with this tag left in front
    // of the content.
    private static readonly Regex ChatTagRegex = new(@"^\[CHAT\]\s*", RegexOptions.Compiled);

    private readonly DispatcherTimer _timer;
    private string _logPath;
    private long _lastPosition;
    private bool _running;

    public event Action<IReadOnlyList<string>>? RosterUpdated;
    public event Action<string>? StatusChanged;

    /// <summary>Fired with the victim's username whenever a Bedwars "FINAL KILL!" chat line is seen.</summary>
    public event Action<string>? PlayerEliminated;

    /// <summary>Fired when the pre-game lobby countdown hits "1 seconds!", so AutoWho can run its
    /// /who sequence right as the match actually begins.</summary>
    public event Action? AutoWhoTriggerDetected;

    /// <summary>Fired as soon as the client leaves the current server (back to a lobby, or queueing
    /// into the next one), so the roster clears immediately instead of lingering until the next
    /// match's "Bed Wars" banner.</summary>
    public event Action? LeftGame;

    /// <summary>Fired when the chat shows Hypixel's "Sending you to &lt;server&gt;!" transfer message,
    /// or the player runs /l, /L, or /lobby - both mean the match's roster is now stale.</summary>
    public event Action? DataClearRequested;

    public LogWatcherService(string logPath)
    {
        _logPath = logPath;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Poll();
    }

    public void UpdateLogPath(string logPath)
    {
        _logPath = logPath;
        _lastPosition = 0;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;

        // Skip pre-existing log content so we don't replay a stale roster from
        // a previous session; only react to "/who" runs from this point on.
        if (_lastPosition == 0 && File.Exists(_logPath))
        {
            try
            {
                using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                _lastPosition = fs.Length;
            }
            catch (IOException)
            {
                // Ignore; Poll() will retry.
            }
        }

        _timer.Start();
    }

    public void Stop()
    {
        _running = false;
        _timer.Stop();
    }

    private void Poll()
    {
        try
        {
            if (!File.Exists(_logPath))
            {
                StatusChanged?.Invoke(Loc.T("LogWatch_FileNotFound"));
                return;
            }

            using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            // Minecraft truncates/replaces latest.log on each launch.
            if (fs.Length < _lastPosition)
                _lastPosition = 0;

            if (fs.Length == _lastPosition)
                return;

            fs.Seek(_lastPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var newText = reader.ReadToEnd();
            _lastPosition = fs.Position;

            foreach (var line in newText.Split('\n'))
            {
                var onlineMatch = OnlineLineRegex.Match(line);
                if (onlineMatch.Success)
                {
                    var names = onlineMatch.Groups["names"].Value
                        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .ToList();

                    if (names.Count > 0)
                    {
                        StatusChanged?.Invoke(Loc.T("LogWatch_CapturedRoster", names.Count));
                        RosterUpdated?.Invoke(names);
                    }

                    continue;
                }

                if (line.Contains("FINAL KILL", StringComparison.Ordinal))
                {
                    var killMatch = FinalKillVictimRegex.Match(ExtractChatContent(line));
                    if (killMatch.Success)
                        PlayerEliminated?.Invoke(killMatch.Groups["victim"].Value);
                    continue;
                }

                if (line.Contains(AutoWhoTriggerText, StringComparison.Ordinal))
                {
                    StatusChanged?.Invoke(Loc.T("LogWatch_AutoWhoTrigger"));
                    AutoWhoTriggerDetected?.Invoke();
                    continue;
                }

                if (line.Contains("Connecting to", StringComparison.Ordinal) && ServerConnectRegex.IsMatch(ExtractChatContent(line)))
                {
                    StatusChanged?.Invoke(Loc.T("LogWatch_LeftGame"));
                    LeftGame?.Invoke();
                    continue;
                }

                if (line.Contains("Sending you to", StringComparison.Ordinal) && SendingToRegex.IsMatch(ExtractChatContent(line)))
                {
                    StatusChanged?.Invoke(Loc.T("LogWatch_DataCleared"));
                    DataClearRequested?.Invoke();
                    continue;
                }

                if (line.Contains("Issued server command:", StringComparison.Ordinal) && LobbyCommandRegex.IsMatch(ExtractChatContent(line)))
                {
                    StatusChanged?.Invoke(Loc.T("LogWatch_DataCleared"));
                    DataClearRequested?.Invoke();
                }
            }
        }
        catch (IOException)
        {
            // File briefly locked/being rotated by Minecraft; retry next tick.
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(Loc.T("LogWatch_ReadError", ex.Message));
        }
    }

    /// <summary>
    /// Strips a raw log line down to the chat message it carries: drops the "[HH:mm:ss] [Thread/LEVEL]: "
    /// prefix, removes §-formatting codes, and trims the space-padding Hypixel uses to center messages.
    /// </summary>
    private static string ExtractChatContent(string line)
    {
        var content = FormattingCodeRegex.Replace(line, string.Empty);
        var msgStart = content.LastIndexOf("]: ", StringComparison.Ordinal);
        if (msgStart >= 0)
            content = content[(msgStart + 3)..];
        content = ChatTagRegex.Replace(content, string.Empty);
        return content.Trim();
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
