namespace HypixelStatsOverlay.Services;

/// <summary>Minimal English/Chinese string table for the UI. Not a full resx-based localization
/// system - just a lookup keyed by short id, since the app only ever needs these two languages.</summary>
public static class Loc
{
    public static string Language { get; private set; } = "en";

    public static event Action? LanguageChanged;

    /// <summary>Sets the language without raising LanguageChanged - use once at startup before any
    /// window has subscribed, so the initial config value doesn't trigger a redundant refresh.</summary>
    public static void Initialize(string language)
    {
        Language = language == "zh" ? "zh" : "en";
    }

    public static void SetLanguage(string language)
    {
        var normalized = language == "zh" ? "zh" : "en";
        if (Language == normalized)
            return;

        Language = normalized;
        LanguageChanged?.Invoke();
    }

    public static string T(string key, params object[] args)
    {
        if (!Strings.TryGetValue(key, out var pair))
            return key;

        var template = Language == "zh" ? pair.Zh : pair.En;
        return args.Length == 0 ? template : string.Format(template, args);
    }

    private static readonly Dictionary<string, (string En, string Zh)> Strings = new()
    {
        // Title bar
        ["Tray"] = ("Tray", "托盘"),
        ["Tray_Tooltip"] = ("Minimize to tray (runs in background)", "最小化到托盘（后台运行）"),
        ["Exit"] = ("Exit", "退出"),
        ["LangToggle"] = ("CN", "EN"),

        // Header - kept in English regardless of app language (see the Tab_* labels below for the
        // same rule applied elsewhere).
        ["HeaderTag"] = ("MINECRAFT · HYPIXEL BEDWARS", "MINECRAFT · HYPIXEL BEDWARS"),

        // Tabs - short chrome labels, kept in English regardless of app language (see the *_Label
        // entries below for the same rule applied to field/section captions).
        ["Tab_StatCard"] = ("Stat Card", "Stat Card"),
        ["Tab_PlayerLookup"] = ("Player Lookup", "Player Lookup"),
        ["Tab_WindowTools"] = ("Window Tools", "Window Tools"),

        // Stat Card tab
        ["StatCard_Overview"] = ("BEDWARS STATS OVERVIEW", "起床战争数据总览"),
        ["StatCard_Streak"] = ("Streak ", "连胜 "),
        ["StatCard_Coins"] = ("Coins", "金币"),
        ["StatCard_Games"] = ("Games", "对局数"),
        ["StatCard_MostPlayed"] = ("Most Played", "最常玩模式"),
        ["StatCard_Wins"] = ("Wins", "胜场"),
        ["StatCard_Losses"] = ("Losses", "败场"),
        ["StatCard_WLRatio"] = ("W/L Ratio", "胜负比"),
        ["StatCard_FinalKills"] = ("Final Kills", "终杀"),
        ["StatCard_FinalDeaths"] = ("Final Deaths", "终亡"),
        ["StatCard_FDRatio"] = ("F/D Ratio", "终杀死亡比"),
        ["StatCard_Kills"] = ("Kills", "击杀"),
        ["StatCard_Deaths"] = ("Deaths", "死亡"),
        ["StatCard_KDRatio"] = ("K/D Ratio", "击杀死亡比"),
        ["StatCard_BedsBroken"] = ("Beds Broken", "破床数"),
        ["StatCard_BedsLost"] = ("Beds Lost", "失床数"),
        ["StatCard_BBBL"] = ("BB/BL", "破床/失床比"),
        ["StatCard_StatusHint"] = ("Auto-shows stats for the username set in \"Player Lookup\".",
            "自动显示“玩家查询”页设置的用户名数据。"),
        ["CardStatus_FillUsername"] = ("Please fill in your Minecraft username on the \"Player Lookup\" page first.", "请先在“玩家查询”页填写你的我的世界用户名。"),
        ["CardStatus_FillApiKey"] = ("Please fill in your Hypixel API Key on the \"Player Lookup\" page first.", "请先在“玩家查询”页填写你的 Hypixel API 密钥。"),
        ["CardStatus_Loading"] = ("Loading...", "加载中……"),
        ["CardStatus_NickedNotFound"] = ("Player not found (may be hidden by a nick plugin).", "未找到该玩家（可能被隐藏插件伪装了昵称）。"),
        ["CardStatus_LoadFailed"] = ("Load failed: {0}", "加载失败：{0}"),
        ["CardStatus_Empty"] = ("", ""),

        // Player Lookup tab - left column
        // *_Label entries are short field/section captions - kept in English in both languages by
        // request, unlike the full-sentence *_Hint text underneath them which still translates.
        ["ApiKey_Label"] = ("Hypixel API Key", "Hypixel API Key"),
        ["ApiKey_Hint"] = ("No key? Get one at https://developer.hypixel.net", "没有密钥？前往 https://developer.hypixel.net 获取。"),
        ["ApiKey_PasteTooltip"] = ("Paste from clipboard", "从剪贴板粘贴"),
        ["ApiKey_ShowHideTooltip"] = ("Show/Hide", "显示/隐藏"),
        ["OwnUsername_Label"] = ("Minecraft Username", "Minecraft Username"),
        ["OwnUsername_Hint"] = ("Pins your stats at the top of the overlay.", "会将你的数据固定显示在悬浮窗顶部。"),
        ["LogPath_Label"] = ("Minecraft Log Path (latest.log)", "Minecraft Log Path (latest.log)"),
        ["Browse"] = ("Browse...", "浏览……"),

        // Player Lookup tab - right column
        ["Opacity_Label"] = ("Overlay Opacity", "Overlay Opacity"),
        ["Preview"] = ("Preview", "预览"),
        ["Opacity_Hint"] = ("Drag to adjust opacity; click \"Preview\" to test.",
            "拖动调整不透明度；点击“预览”试看效果。"),
        ["Scale_Label"] = ("Overlay Size", "Overlay Size"),
        ["Scale_Hint"] = ("Drag to adjust size; click \"Preview\" to test.",
            "拖动调整大小；点击“预览”试看效果。"),
        ["AutoWho_Label"] = ("AutoWho", "AutoWho"),
        ["AutoWho_Hint"] = ("Auto-sends /who 4s after each game starts to capture the roster.",
            "每局开始 4 秒后自动发送 /who 获取名单。"),
        ["Test"] = ("Test", "测试"),
        ["SaveButton"] = ("Save Settings & Start Watching", "保存设置并开始监听"),
        ["Status_Label"] = ("Status", "Status"),

        ["Status_NotStarted"] = ("Not started yet", "尚未开始"),
        ["Status_WatchingNoKey"] = ("Watching started, but no API Key is set yet - please fill it in and save", "监听已开始，但尚未设置 API 密钥 - 请填写后保存"),
        ["Status_WatchingWaiting"] = ("Watching started, waiting for /who ...", "监听已开始，等待 /who 中……"),
        ["Status_SavedNoKey"] = ("Saved, but no API Key is set yet", "已保存，但尚未设置 API 密钥"),
        ["Status_SavedWaiting"] = ("Settings saved, waiting for /who ...", "设置已保存，等待 /who 中……"),
        ["Status_ApiKeyAutoFilled"] = ("API Key auto-filled, waiting for /who ...", "已自动填入 API 密钥，等待 /who 中……"),
        ["Status_AutoWhoTesting"] = ("AutoWho: testing...", "自动 /who：测试中……"),
        ["AutoWho_Sent"] = ("AutoWho: /who sent automatically", "自动 /who：已自动发送 /who"),
        ["AutoWho_WindowNotFound"] = ("AutoWho: Minecraft window not found (game isn't running, or uses a client this tool doesn't recognize)",
            "自动 /who：未找到我的世界窗口（游戏未运行，或使用了本工具无法识别的启动器）"),
        ["AutoWho_Unknown"] = ("AutoWho: unknown error", "自动 /who：未知错误"),

        ["Roster_Current"] = ("Current roster: {0}", "当前名单：{0}"),
        ["Roster_EmptyCapture"] = ("Current roster: (empty - type /who in-game to capture)", "当前名单：（空 - 请在游戏内输入 /who 获取）"),
        ["Roster_Empty"] = ("Current roster: (empty)", "当前名单：（空）"),

        // Window Tools tab
        ["TargetWindow_Label"] = ("Target Window", "Target Window"),
        ["SelectProcess"] = ("Select Process", "选择进程"),
        ["NoWindowSelected"] = ("No window selected", "未选择窗口"),
        ["SelectProcess_Hint"] = ("Pick a window; the buttons below act on it.",
            "选择一个窗口，下方按钮将作用于它。"),
        ["WindowBorder_Label"] = ("Window Border", "Window Border"),
        ["MakeBorderless"] = ("Make Borderless", "去除边框"),
        ["BorderlessFullscreen"] = ("Borderless Fullscreen", "无边框全屏"),
        ["RestoreWindow"] = ("Restore Window", "恢复窗口"),
        ["WindowTool_Hint"] = ("Borderless: removes title bar. Fullscreen: also fills the screen. Restore: undoes both.",
            "去边框：移除标题栏。全屏：额外铺满屏幕。恢复：撤销以上操作。"),
        ["WindowTool_SelectFirst"] = ("Please click \"Select Process\" and pick a window first.", "请先点击“选择进程”并选择一个窗口。"),
        ["WindowTool_AppliedBorderless"] = ("Applied borderless to {0}.", "已为 {0} 去除边框。"),
        ["WindowTool_SetFullscreen"] = ("Set {0} to borderless fullscreen.", "已将 {0} 设置为无边框全屏。"),
        ["WindowTool_Restored"] = ("Restored {0}'s original border and size.", "已恢复 {0} 的原始边框和大小。"),
        ["WindowTool_OpFailed"] = ("Operation failed - the target window may have closed, or is running with higher privileges (try running this program as administrator).",
            "操作失败 - 目标窗口可能已关闭，或以更高权限运行（请尝试以管理员身份运行本程序）。"),
        ["WindowTool_NoRecord"] = ("No record to restore (this window was never made borderless/fullscreen, or has since closed).",
            "没有可恢复的记录（该窗口从未被去除边框/设为全屏，或已关闭）。"),

        // Dialogs
        ["Dialog_ApiKeyDetectedTitle"] = ("API Key detected", "检测到 API 密钥"),
        ["Dialog_ApiKeyDetectedBody"] = ("Found a string in the clipboard that looks like a Hypixel API Key. Use it to update the current settings?",
            "在剪贴板中检测到一个疑似 Hypixel API 密钥的字符串，是否使用它更新当前设置？"),
        ["Dialog_UseKey"] = ("Use This Key", "使用此密钥"),
        ["Dialog_Ignore"] = ("Ignore", "忽略"),
        ["Dialog_SelectLogFileTitle"] = ("Select Minecraft Log File (latest.log)", "选择我的世界日志文件 (latest.log)"),
        ["TrayMenu_ShowSettings"] = ("Show Settings", "显示设置"),
        ["TrayMenu_Exit"] = ("Exit", "退出"),

        // Player lookup errors (shown in the overlay row / stat card when a fetch fails)
        ["Api_MojangTimeout"] = ("Mojang API request timed out, please try again later", "Mojang API 请求超时，请稍后重试"),
        ["Api_UuidLookupFailed"] = ("UUID lookup failed: {0}", "UUID 查询失败：{0}"),
        ["Api_HypixelTimeout"] = ("Hypixel API request timed out, please try again later", "Hypixel API 请求超时，请稍后重试"),
        ["Api_ConnectionFailed"] = ("Hypixel API connection failed: {0}", "Hypixel API 连接失败：{0}"),
        ["Api_TooManyRequests"] = ("Too many API requests, please try again later", "API 请求次数过多，请稍后重试"),
        ["Api_InvalidKey"] = ("Invalid API Key ({0}): {1}", "API 密钥无效 ({0})：{1}"),
        ["Api_HttpError"] = ("Hypixel API error: {0}", "Hypixel API 错误：{0}"),
        ["Api_QueryFailed"] = ("Hypixel API query failed", "Hypixel API 查询失败"),
        ["Api_NeverLoggedIn"] = ("This player has never logged into Hypixel", "该玩家从未登录过 Hypixel"),
        ["ApiKeyNotSet"] = ("API Key not set", "未设置 API 密钥"),
        ["LookupFailed"] = ("Lookup failed: {0}", "查询失败：{0}"),
        ["Overlay_NickedStatus"] = ("Nicked (hidden name)", "使用了隐藏昵称"),

        // Log watcher status line
        ["LogWatch_FileNotFound"] = ("Waiting: log file not found", "等待中：未找到日志文件"),
        ["LogWatch_CapturedRoster"] = ("Captured roster: {0} players", "已获取名单：{0} 名玩家"),
        ["LogWatch_AutoWhoTrigger"] = ("Countdown reached 1s - AutoWho queued", "倒计时到 1 秒 - AutoWho 已排队"),
        ["LogWatch_LeftGame"] = ("Left the match", "已离开对局"),
        ["LogWatch_DataCleared"] = ("Roster data cleared", "名单数据已清空"),
        ["LogWatch_ReadError"] = ("Log read error: {0}", "日志读取出错：{0}"),

        // Passthrough for dynamic values (e.g. a window title) that shouldn't be translated but still
        // need to flow through the same SetText/re-render pipeline as everything else.
        ["Plain"] = ("{0}", "{0}"),

        // Overlay window column headers - kept in English regardless of app language. The overlay
        // sits on top of the game as a compact HUD; it must never show Chinese even when the rest
        // of the app is set to Chinese.
        ["Overlay_Player"] = ("Player", "Player"),
        ["Overlay_Star"] = ("Star", "Star"),
        ["Overlay_Streak"] = ("Streak", "Streak"),
        ["Overlay_Fkdr"] = ("FKDR", "FKDR"),
        ["Overlay_Wins"] = ("Wins", "Wins"),
        ["Overlay_Beds"] = ("Beds", "Beds"),
        ["Overlay_Finals"] = ("Finals", "Finals"),
    };
}
