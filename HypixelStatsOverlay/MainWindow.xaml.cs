using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HypixelStatsOverlay.Models;
using HypixelStatsOverlay.Services;
using Application = System.Windows.Application;

namespace HypixelStatsOverlay;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService = new();
    private readonly AppConfig _config;

    private static readonly HttpClient Http = CreateHttpClient();
    private readonly MojangApiService _mojang;
    private readonly HypixelApiService _hypixelApi;

    private readonly LogWatcherService _logWatcher;
    private readonly KeyboardHookService _keyboardHook = new();
    private readonly OverlayWindow _overlay = new();
    private readonly WindowToolsService _windowTools = new();
    private readonly AutoWhoService _autoWho = new();
    private readonly DispatcherTimer _statCardRefreshTimer;

    private readonly Dictionary<string, PlayerStats> _rosterStats = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _currentRoster = new();
    private List<WindowEntry> _windowEntries = new();
    private WindowEntry? _selectedWindow;

    private NotifyIcon? _trayIcon;
    private bool _reallyExit;
    private bool _showingSampleData;

    // Hypixel API keys are UUIDs (e.g. generated at developer.hypixel.net); used to spot one
    // sitting in the clipboard so it can be offered up instead of requiring manual paste.
    private static readonly Regex ApiKeyShapeRegex = new(
        @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        RegexOptions.Compiled);
    private string? _lastPromptedClipboardKey;

    /// <summary>The "Status" card was removed from the Player Lookup page; these stay as detached
    /// (never-rendered) elements purely so the existing status-tracking writes below don't need to
    /// be ripped out call-by-call.</summary>
    private readonly TextBlock StatusText = new();
    private readonly Run StatusLabelRun = new();
    private readonly TextBlock RosterText = new();

    /// <summary>Tracks the localization key/args behind each dynamically-set status TextBlock, so
    /// ApplyLanguage() can re-render whatever's currently shown in the newly selected language
    /// instead of only refreshing the static labels.</summary>
    private readonly Dictionary<TextBlock, (string Key, object[] Args)> _dynamicText = new();

    public MainWindow()
    {
        InitializeComponent();

        _config = _configService.Load();
        Loc.SetLanguage(_config.Language);

        ApiKeyValue = _config.HypixelApiKey;
        OwnUsernameBox.Text = _config.OwnUsername;
        LogPathBox.Text = _config.MinecraftLogPath;
        OpacitySlider.Value = _config.OverlayOpacity;
        ApplyOpacity(_config.OverlayOpacity);
        ScaleSlider.Value = _config.OverlayScale;
        ApplyScale(_config.OverlayScale);
        AutoWhoToggle.IsChecked = _config.AutoWhoEnabled;

        SetText(RosterText, "Roster_EmptyCapture");
        SetText(SelectedWindowText, "NoWindowSelected");
        SetText(WindowToolStatusText, "WindowTool_Hint");
        ApplyLanguage();

        _mojang = new MojangApiService(Http);
        _hypixelApi = new HypixelApiService(Http, _mojang);

        // The "Stat Card" tab has no query button - it always reflects whatever ID is currently in
        // OwnUsernameBox, generated once at startup and regenerated whenever that field changes.
        OwnUsernameBox.LostFocus += (_, _) => _ = LoadStatCardAsync();
        _ = LoadStatCardAsync();

        // Keeps the stat card's numbers current while the window sits open between matches.
        _statCardRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _statCardRefreshTimer.Tick += (_, _) => _ = LoadStatCardAsync(force: true);
        _statCardRefreshTimer.Start();

        _logWatcher = new LogWatcherService(_config.MinecraftLogPath);
        _logWatcher.RosterUpdated += names => Dispatcher.Invoke(() => OnRosterUpdated(names));
        _logWatcher.StatusChanged += msg => Dispatcher.Invoke(() =>
        {
            // Baked in whatever language was active when the watcher fired, not re-translatable via
            // a Loc key - drop any stale dynamic-text tracking so ApplyLanguage() doesn't stomp it.
            _dynamicText.Remove(StatusText);
            StatusText.Text = msg;
        });
        _logWatcher.PlayerEliminated += name => Dispatcher.Invoke(() => OnPlayerEliminated(name));
        _logWatcher.AutoWhoTriggerDetected += () => Dispatcher.Invoke(OnAutoWhoTriggerDetected);
        _logWatcher.DataClearRequested += () => Dispatcher.Invoke(OnDataClearRequested);

        _keyboardHook.TabPressed += () => Dispatcher.Invoke(OnTabPressed);
        _keyboardHook.TabReleased += () => Dispatcher.Invoke(() => _overlay.HideOverlay());

        SetupTrayIcon();

        Activated += (_, _) => CheckClipboardForApiKey();

        // Not called directly here: at this point in the constructor the window hasn't been shown
        // yet (no HWND), and ApiKeyDetectedDialog.Show sets Owner = this - WPF throws if the owner
        // hasn't been shown previously. Even deferring to Loaded isn't safe: App.OnStartup shows
        // this window mid-way through the splash-to-main handoff (SplashWindow.FlyLogoIntoAsync),
        // and Loaded fires synchronously inside that Show() call - opening a modal dialog re-entrantly
        // from there deadlocks the whole UI thread (the splash animation freezes solid). Instead App
        // calls CheckClipboardForApiKey() itself once the entire handoff has actually finished.
        _logWatcher.Start();
        _keyboardHook.Start();
        SetText(StatusText, string.IsNullOrWhiteSpace(_config.HypixelApiKey) ? "Status_WatchingNoKey" : "Status_WatchingWaiting");
    }

    /// <summary>Sets a TextBlock's text from a localization key and remembers the key/args so
    /// ApplyLanguage() can re-render it if the user switches language afterward.</summary>
    private void SetText(TextBlock tb, string key, params object[] args)
    {
        _dynamicText[tb] = (key, args);
        tb.Text = Loc.T(key, args);
    }

    private void LanguageToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var newLanguage = Loc.Language == "zh" ? "en" : "zh";
        Loc.SetLanguage(newLanguage);
        _config.Language = newLanguage;
        _configService.Save(_config);
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        LanguageToggleButton.Content = Loc.T("LangToggle");
        TrayButton.ToolTip = Loc.T("Tray_Tooltip");
        CloseButtonTitleBar.ToolTip = Loc.T("Exit");

        HeaderTagText.Text = Loc.T("HeaderTag");

        TabStatCard.Header = Loc.T("Tab_StatCard");
        TabPlayerLookup.Header = Loc.T("Tab_PlayerLookup");
        TabWindowTools.Header = Loc.T("Tab_WindowTools");

        StatCardOverviewText.Text = Loc.T("StatCard_Overview");
        StatCardStreakLabelRun.Text = Loc.T("StatCard_Streak");
        StatCardCoinsLabel.Text = Loc.T("StatCard_Coins");
        StatCardGamesLabel.Text = Loc.T("StatCard_Games");
        StatCardMostPlayedLabel.Text = Loc.T("StatCard_MostPlayed");
        StatCardWinsLabel.Text = Loc.T("StatCard_Wins");
        StatCardLossesLabel.Text = Loc.T("StatCard_Losses");
        StatCardWLRatioLabel.Text = Loc.T("StatCard_WLRatio");
        StatCardFinalKillsLabel.Text = Loc.T("StatCard_FinalKills");
        StatCardFinalDeathsLabel.Text = Loc.T("StatCard_FinalDeaths");
        StatCardFDRatioLabel.Text = Loc.T("StatCard_FDRatio");
        StatCardKillsLabel.Text = Loc.T("StatCard_Kills");
        StatCardDeathsLabel.Text = Loc.T("StatCard_Deaths");
        StatCardKDRatioLabel.Text = Loc.T("StatCard_KDRatio");
        StatCardBedsBrokenLabel.Text = Loc.T("StatCard_BedsBroken");
        StatCardBedsLostLabel.Text = Loc.T("StatCard_BedsLost");
        StatCardBBBLLabel.Text = Loc.T("StatCard_BBBL");

        ApiKeyLabelRun.Text = Loc.T("ApiKey_Label");
        ApiKeyHintText.Text = Loc.T("ApiKey_Hint");
        PasteApiKeyButton.ToolTip = Loc.T("ApiKey_PasteTooltip");
        ApiKeyVisibilityToggle.ToolTip = Loc.T("ApiKey_ShowHideTooltip");
        OwnUsernameLabelRun.Text = Loc.T("OwnUsername_Label");
        OwnUsernameHintText.Text = Loc.T("OwnUsername_Hint");
        LogPathLabelRun.Text = Loc.T("LogPath_Label");
        BrowseButton.Content = Loc.T("Browse");

        OpacityLabelRun.Text = Loc.T("Opacity_Label");
        PreviewToggle.Content = Loc.T("Preview");
        OpacityHintText.Text = Loc.T("Opacity_Hint");
        ScaleLabelRun.Text = Loc.T("Scale_Label");
        ScaleHintText.Text = Loc.T("Scale_Hint");
        AutoWhoLabelRun.Text = Loc.T("AutoWho_Label");
        AutoWhoHintText.Text = Loc.T("AutoWho_Hint");
        AutoWhoTestButton.Content = Loc.T("Test");
        SaveSettingsButton.Content = Loc.T("SaveButton");
        StatusLabelRun.Text = Loc.T("Status_Label");

        TargetWindowLabelRun.Text = Loc.T("TargetWindow_Label");
        SelectProcessButton.Content = Loc.T("SelectProcess");
        SelectProcessHintText.Text = Loc.T("SelectProcess_Hint");
        WindowBorderLabelRun.Text = Loc.T("WindowBorder_Label");
        MakeBorderlessButton.Content = Loc.T("MakeBorderless");
        BorderlessFullscreenButton.Content = Loc.T("BorderlessFullscreen");
        RestoreWindowButton.Content = Loc.T("RestoreWindow");

        foreach (var (tb, (key, args)) in _dynamicText)
            tb.Text = Loc.T(key, args);

        RefreshTrayMenu();
    }

    private void RefreshTrayMenu()
    {
        if (_trayIcon == null)
            return;

        var menu = new ContextMenuStrip();
        menu.Items.Add(Loc.T("TrayMenu_ShowSettings"), null, (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); });
        menu.Items.Add(Loc.T("TrayMenu_Exit"), null, (_, _) => { _reallyExit = true; Close(); });
        var oldMenu = _trayIcon.ContextMenuStrip;
        _trayIcon.ContextMenuStrip = menu;
        oldMenu?.Dispose();
    }

    /// <summary>
    /// Hypixel API keys can only be generated by hand at developer.hypixel.net (there's no API to
    /// automate that login), so the next best thing is watching for the user copying a freshly
    /// generated key: whenever the window regains focus, check whether the clipboard holds a
    /// UUID-shaped string that differs from the saved key, and offer to fill it in.
    /// </summary>
    internal void CheckClipboardForApiKey()
    {
        string candidate;
        try
        {
            if (!System.Windows.Clipboard.ContainsText())
                return;
            candidate = System.Windows.Clipboard.GetText().Trim();
        }
        catch (Exception)
        {
            // Clipboard can throw if another process holds it briefly; just skip this check.
            return;
        }

        if (!ApiKeyShapeRegex.IsMatch(candidate))
            return;

        if (candidate.Equals(_config.HypixelApiKey, StringComparison.OrdinalIgnoreCase))
            return;

        if (candidate.Equals(_lastPromptedClipboardKey, StringComparison.OrdinalIgnoreCase))
            return;

        _lastPromptedClipboardKey = candidate;

        if (!ApiKeyDetectedDialog.Show(this, candidate))
            return;

        ApiKeyValue = candidate;
        _config.HypixelApiKey = candidate;
        _configService.Save(_config);

        SetText(StatusText, "Status_ApiKeyAutoFilled");
    }

    /// <summary>The API key lives in whichever of the two overlapping boxes is currently visible -
    /// PasswordBox while masked, plain TextBox while revealed via the eye toggle.</summary>
    private string ApiKeyValue
    {
        get => ApiKeyVisibilityToggle.IsChecked == true ? ApiKeyTextBox.Text : ApiKeyPasswordBox.Password;
        set
        {
            ApiKeyPasswordBox.Password = value;
            ApiKeyTextBox.Text = value;
        }
    }

    private void PasteApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!System.Windows.Clipboard.ContainsText())
            return;

        ApiKeyValue = System.Windows.Clipboard.GetText().Trim();
    }

    private void ApiKeyVisibilityToggle_Checked(object sender, RoutedEventArgs e)
    {
        ApiKeyTextBox.Text = ApiKeyPasswordBox.Password;
        ApiKeyPasswordBox.Visibility = Visibility.Collapsed;
        ApiKeyTextBox.Visibility = Visibility.Visible;
    }

    private void ApiKeyVisibilityToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        ApiKeyPasswordBox.Password = ApiKeyTextBox.Text;
        ApiKeyTextBox.Visibility = Visibility.Collapsed;
        ApiKeyPasswordBox.Visibility = Visibility.Visible;
    }

    /// <summary>Guards against redundant re-fetching - OwnUsernameBox.LostFocus fires every time focus
    /// leaves the field, not just when its text actually changed (e.g. tabbing through without editing).</summary>
    private string? _lastCardUsername;

    private bool _cardLoadInFlight;

    /// <summary>Looks up the Minecraft ID from the "Player Lookup" page (not a separate field here - the
    /// user only has to type their name once) and binds the result directly onto StatCardBorder, which
    /// displays it live - no image export/generation step. Runs automatically at startup, again
    /// whenever OwnUsernameBox changes, and every 30 seconds thereafter, so there's no query button.</summary>
    /// <param name="force">Bypasses the "already loaded this username" guard - used by the 30-second
    /// refresh timer, which needs to re-fetch the same username instead of being deduped away.</param>
    private async Task LoadStatCardAsync(bool force = false)
    {
        var username = OwnUsernameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            SetText(CardStatusText, "CardStatus_FillUsername");
            return;
        }

        var apiKey = ApiKeyValue.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            SetText(CardStatusText, "CardStatus_FillApiKey");
            return;
        }

        // Same ID as last time and a lookup is already running/done for it - nothing to do, unless
        // this is a forced periodic refresh, which wants fresh numbers for that same ID.
        if (_cardLoadInFlight || (!force && username.Equals(_lastCardUsername, StringComparison.OrdinalIgnoreCase)))
            return;

        _cardLoadInFlight = true;
        SetText(CardStatusText, "CardStatus_Loading");
        try
        {
            var stats = await _hypixelApi.GetStatsAsync(username, apiKey);
            // Only latch the "already handled this ID" guard once a real response came back - a
            // network hiccup should still be retryable by refocusing the field with the same text.
            _lastCardUsername = username;

            if (stats.IsSpecialStatus)
            {
                if (stats.IsNicked)
                    SetText(CardStatusText, "CardStatus_NickedNotFound");
                else
                {
                    // Baked in whatever language was active when the API call returned, not
                    // re-translatable via a Loc key - drop any stale dynamic-text tracking for it.
                    _dynamicText.Remove(CardStatusText);
                    CardStatusText.Text = stats.StatusText;
                }
                return;
            }

            StatCardBorder.DataContext = stats;
            SetText(CardStatusText, "CardStatus_Empty");
        }
        catch (Exception ex)
        {
            SetText(CardStatusText, "CardStatus_LoadFailed", HttpRetry.Describe(ex));
        }
        finally
        {
            _cardLoadInFlight = false;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void TrayButton_Click(object sender, RoutedEventArgs e) => Hide();

    // The titlebar's close button now really exits (see OnClosing below); use the dedicated tray
    // button, or the tray icon's own "Show Settings", to keep the process running in the background instead.
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _reallyExit = true;
        Close();
    }

    private static HttpClient CreateHttpClient()
    {
        // A pooled connection that a proxy silently kills while idle (common with VPN/proxy clients
        // that drop idle tunnels without a proper FIN) surfaces on the next reuse as exactly this kind
        // of "SSL connection could not be established ... unexpected EOF" - not a fresh handshake
        // failure at all, just HttpClient trying to reuse a connection that's already dead underneath
        // it. Recycling pooled connections periodically means a dead one gets replaced instead of reused.
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        // A bare "WitheredRose/1.0" UA reads as an obvious script to Cloudflare's bot management sitting in
        // front of api.hypixel.net, which can 403 it with a challenge page before the request ever
        // reaches Hypixel's own key check - a realistic browser UA (plus the Accept/Accept-Language a
        // browser would send) gets treated as ordinary traffic instead.
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        return client;
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Visible = true,
            Text = "WitheredRose"
        };

        _trayIcon.DoubleClick += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); };
        RefreshTrayMenu();
    }

    /// <summary>WinForms' NotifyIcon needs a System.Drawing.Icon, not a WPF ImageSource, so the
    /// embedded logo.ico is pulled out via the pack URI resource stream rather than Window.Icon.</summary>
    private static System.Drawing.Icon LoadAppIcon()
    {
        var resourceInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/logo.ico"));
        return new System.Drawing.Icon(resourceInfo!.Stream);
    }

    private void OnRosterUpdated(IReadOnlyList<string> names)
    {
        _currentRoster = names.ToList();
        _rosterStats.Clear();
        foreach (var name in _currentRoster)
            _rosterStats[name] = PlayerStats.Loading(name);

        SetText(RosterText, "Roster_Current", string.Join(", ", _currentRoster));

        var apiKey = _config.HypixelApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            foreach (var name in _currentRoster)
                _rosterStats[name] = PlayerStats.Error(name, Loc.T("ApiKeyNotSet"));
            return;
        }

        foreach (var name in _currentRoster)
            _ = FetchOneAsync(name, apiKey);
    }

    private async Task FetchOneAsync(string name, string apiKey)
    {
        PlayerStats stats;
        try
        {
            stats = await _hypixelApi.GetStatsAsync(name, apiKey);
        }
        catch (Exception ex)
        {
            stats = PlayerStats.Error(name, Loc.T("LookupFailed", HttpRetry.Describe(ex)));
        }

        Dispatcher.Invoke(() =>
        {
            _rosterStats[name] = stats;
            if (_overlay.IsVisible)
                UpdateOverlayPlayer(name, stats);
        });
    }

    /// <summary>Updates a single row's data in place instead of going through RefreshOverlayContent's
    /// Clear()+re-add-everything, which the ItemsControl sees as a full reset (every container torn
    /// down and rebuilt) and which made the SizeToContent overlay window visibly shrink-then-regrow
    /// once per player as each async stat fetch completed at its own pace. A same-index replace only
    /// touches that one container, so the row count - and the window size - never changes.</summary>
    private void UpdateOverlayPlayer(string name, PlayerStats stats)
    {
        stats.IsOwnPlayer = string.Equals(name, _config.OwnUsername, StringComparison.OrdinalIgnoreCase);
        if (stats.IsOwnPlayer && !stats.IsSpecialStatus)
            ApplyOwnPlayerRankOverride(stats);

        for (var i = 0; i < _overlay.Players.Count; i++)
        {
            if (string.Equals(_overlay.Players[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                _overlay.Players[i] = stats;
                return;
            }
        }

        // Row not painted yet (e.g. the very first result to land before the overlay's initial
        // population) - fall back to a full rebuild just this once.
        RefreshOverlayContent();
    }

    /// <summary>The pre-game countdown just hit "1 seconds!" - the match is starting right now. The
    /// previous match's roster is left on screen (the user wants the overlay to keep showing it)
    /// until the next "/who" replaces it via OnRosterUpdated.</summary>
    private void OnAutoWhoTriggerDetected()
    {
        if (AutoWhoToggle.IsChecked == true)
            _ = TriggerAutoWhoAsync();
    }

    /// <summary>AutoWho: holds "S" for 4 seconds (via AutoWhoService, giving players time to actually
    /// spawn in) then types "/who" into Minecraft's chat automatically.</summary>
    private async Task TriggerAutoWhoAsync()
    {
        SetText(StatusText, DescribeAutoWhoResultKey(await _autoWho.SendWhoCommandAsync()));
    }

    /// <summary>Clears the current match's roster/stats - fired when the chat shows Hypixel's "Sending
    /// you to &lt;server&gt;!" transfer message, or when the player runs /l, /L, or /lobby. Both signal
    /// the player has left the match, so the stale roster shouldn't linger on screen or in the overlay.</summary>
    private void OnDataClearRequested()
    {
        _currentRoster.Clear();
        _rosterStats.Clear();
        SetText(RosterText, "Roster_Empty");

        if (_overlay.IsVisible)
            _overlay.Players.Clear();
    }

    /// <summary>Manually fires AutoWho right away (no 5s wait, no need for a real match to be
    /// running) so it can be tested/debugged from the settings window directly.</summary>
    private async void AutoWhoTestButton_Click(object sender, RoutedEventArgs e)
    {
        SetText(StatusText, "Status_AutoWhoTesting");
        SetText(StatusText, DescribeAutoWhoResultKey(await _autoWho.SendWhoCommandAsync()));
    }

    private static string DescribeAutoWhoResultKey(AutoWhoResult result) => result switch
    {
        AutoWhoResult.Sent => "AutoWho_Sent",
        AutoWhoResult.WindowNotFound => "AutoWho_WindowNotFound",
        _ => "AutoWho_Unknown"
    };

    /// <summary>Removes a finally-killed player from the roster so the overlay reflects who's still alive.</summary>
    private void OnPlayerEliminated(string name)
    {
        var match = _currentRoster.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        if (match == null) return;

        _currentRoster.Remove(match);
        _rosterStats.Remove(match);

        if (_currentRoster.Count > 0)
            SetText(RosterText, "Roster_Current", string.Join(", ", _currentRoster));
        else
            SetText(RosterText, "Roster_Empty");

        if (_overlay.IsVisible)
            RemoveOverlayPlayer(match);
    }

    /// <summary>Removes a single row from the overlay without going through RefreshOverlayContent's
    /// Clear()+re-add-everything - same reasoning as UpdateOverlayPlayer: a Clear() is a full reset for
    /// the ItemsControl (every remaining row's container torn down and rebuilt too), which made the
    /// overlay flicker on every single final kill during a match, not just once.</summary>
    private void RemoveOverlayPlayer(string name)
    {
        for (var i = 0; i < _overlay.Players.Count; i++)
        {
            if (string.Equals(_overlay.Players[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                _overlay.Players.RemoveAt(i);
                return;
            }
        }
    }

    private void OnTabPressed()
    {
        RefreshOverlayContent();
        _overlay.ShowAtTopLeft();
    }

    private void RefreshOverlayContent()
    {
        // Real roster data is about to replace whatever's shown, so any sample-preview rows no longer apply.
        _showingSampleData = false;
        _overlay.Players.Clear();

        // Stable sort: the local player's row always floats to the top, everyone else keeps roster order.
        var ordered = _currentRoster.OrderBy(name =>
            !string.IsNullOrWhiteSpace(_config.OwnUsername) &&
            string.Equals(name, _config.OwnUsername, StringComparison.OrdinalIgnoreCase) ? 0 : 1);

        foreach (var name in ordered)
        {
            var stats = _rosterStats.TryGetValue(name, out var s) ? s : PlayerStats.Loading(name);
            stats.IsOwnPlayer = string.Equals(name, _config.OwnUsername, StringComparison.OrdinalIgnoreCase);
            if (stats.IsOwnPlayer && !stats.IsSpecialStatus)
                ApplyOwnPlayerRankOverride(stats);

            _overlay.Players.Add(stats);
        }
    }

    /// <summary>Cosmetic-only override for the local player's own row: shows an MVP++ badge with a
    /// dark-blue "++" regardless of their real Hypixel rank. Only the rank tag/colors are touched -
    /// FKDR, wins, etc. stay the real fetched values.</summary>
    private static void ApplyOwnPlayerRankOverride(PlayerStats stats)
    {
        stats.RankPrefix = "[MVP";
        stats.RankPlus = "++";
        stats.RankSuffix = "]";
        stats.RankColorHex = McColor.Gold;
        stats.RankPlusColorHex = McColor.DarkBlue;
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        ApplyOpacity(e.NewValue);

    private void ApplyOpacity(double value)
    {
        OpacityValueText.Text = value.ToString("P0");
        _overlay.SetBackgroundOpacity(value);
    }

    private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        ApplyScale(e.NewValue);

    private void ApplyScale(double value)
    {
        // Display-only remap: the slider still drives the real 0.6x-2.0x overlay scale below, but
        // the label reads as a plain 0-100 range (min->0%, max->100%) instead of the raw multiplier.
        var displayPercent = (value - ScaleSlider.Minimum) / (ScaleSlider.Maximum - ScaleSlider.Minimum);
        ScaleValueText.Text = displayPercent.ToString("P0");
        _overlay.Scale = value;
    }

    /// <summary>Shows the overlay with placeholder rows (if no real game roster is loaded yet) so the
    /// user can see the transparency/rank/FKDR styling without having to be in a live match.</summary>
    private void PreviewToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_overlay.Players.Count == 0)
        {
            _showingSampleData = true;
            var ownSample = new PlayerStats
            {
                Name = "YourName", IsOwnPlayer = true,
                BedwarsLevel = 320, Fkdr = 4.21, Wins = 950, BedsBroken = 700, FinalKills = 2600, WinStreak = 12
            };
            ApplyOwnPlayerRankOverride(ownSample);
            _overlay.Players.Add(ownSample);
            _overlay.Players.Add(new PlayerStats
            {
                Name = "EnemyPlayer",
                RankPrefix = "[VIP", RankPlus = "+", RankSuffix = "]",
                RankColorHex = McColor.Green, RankPlusColorHex = McColor.Gold,
                BedwarsLevel = 150, Fkdr = 1.85, Wins = 210, BedsBroken = 190, FinalKills = 430, WinStreak = null
            });
            _overlay.Players.Add(PlayerStats.Nicked("xX_Nicked_Xx"));
        }

        _overlay.ShowAtTopLeft();
    }

    private void PreviewToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _overlay.HideOverlay();

        if (_showingSampleData)
        {
            _overlay.Players.Clear();
            _showingSampleData = false;
        }
    }

    private void SelectProcessButton_Click(object sender, RoutedEventArgs e)
    {
        _windowEntries = _windowTools.EnumerateWindows()
            .OrderBy(w => w.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ProcessListBox.ItemsSource = _windowEntries;
        ProcessPickerPopup.IsOpen = true;
    }

    private void ProcessListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProcessListBox.SelectedItem is WindowEntry entry)
        {
            _selectedWindow = entry;
            SetText(SelectedWindowText, "Plain", entry.DisplayName);
        }

        ProcessPickerPopup.IsOpen = false;
    }

    private void BorderlessButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow == null)
        {
            SetText(WindowToolStatusText, "WindowTool_SelectFirst");
            return;
        }

        if (_windowTools.MakeBorderless(_selectedWindow.Handle))
            SetText(WindowToolStatusText, "WindowTool_AppliedBorderless", _selectedWindow.DisplayName);
        else
            SetText(WindowToolStatusText, "WindowTool_OpFailed");
    }

    private void FullscreenBorderlessButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow == null)
        {
            SetText(WindowToolStatusText, "WindowTool_SelectFirst");
            return;
        }

        if (_windowTools.MakeBorderlessFullscreen(_selectedWindow.Handle))
            SetText(WindowToolStatusText, "WindowTool_SetFullscreen", _selectedWindow.DisplayName);
        else
            SetText(WindowToolStatusText, "WindowTool_OpFailed");
    }

    private void RestoreWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow == null)
        {
            SetText(WindowToolStatusText, "WindowTool_SelectFirst");
            return;
        }

        if (_windowTools.RestoreWindow(_selectedWindow.Handle))
            SetText(WindowToolStatusText, "WindowTool_Restored", _selectedWindow.DisplayName);
        else
            SetText(WindowToolStatusText, "WindowTool_NoRecord");
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = Loc.T("Dialog_SelectLogFileTitle"),
            Filter = "Log files (*.log)|*.log|All files (*.*)|*.*",
            FileName = "latest.log"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            LogPathBox.Text = dialog.FileName;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _config.HypixelApiKey = ApiKeyValue.Trim();
        _config.OwnUsername = OwnUsernameBox.Text.Trim();
        _config.MinecraftLogPath = LogPathBox.Text.Trim();
        _config.OverlayOpacity = OpacitySlider.Value;
        _config.OverlayScale = _overlay.Scale;
        _config.AutoWhoEnabled = AutoWhoToggle.IsChecked == true;
        _configService.Save(_config);

        _logWatcher.UpdateLogPath(_config.MinecraftLogPath);

        SetText(StatusText, string.IsNullOrWhiteSpace(_config.HypixelApiKey) ? "Status_SavedNoKey" : "Status_SavedWaiting");
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_reallyExit)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _logWatcher.Dispose();
        _statCardRefreshTimer.Stop();
        _keyboardHook.Dispose();
        _overlay.Close();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        base.OnClosing(e);
        Application.Current.Shutdown();
    }
}
