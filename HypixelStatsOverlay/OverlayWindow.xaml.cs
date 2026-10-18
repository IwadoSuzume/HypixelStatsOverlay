using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using HypixelStatsOverlay.Models;
using HypixelStatsOverlay.Native;
using HypixelStatsOverlay.Services;
using Color = System.Windows.Media.Color;

namespace HypixelStatsOverlay;

public partial class OverlayWindow : Window
{
    private const double MinScale = 0.6;
    private const double MaxScale = 2.0;

    private static readonly Color PanelColor = Color.FromRgb(0x10, 0x10, 0x10);

    private nint _hwnd;
    private double _scale = 1.0;

    public ObservableCollection<PlayerStats> Players { get; } = new();

    public OverlayWindow()
    {
        InitializeComponent();
        DataContext = this;

        ApplyLanguage();
        Loc.LanguageChanged += ApplyLanguage;
    }

    private void ApplyLanguage()
    {
        HeaderPlayer.Text = Loc.T("Overlay_Player");
        HeaderStar.Text = Loc.T("Overlay_Star");
        HeaderStreak.Text = Loc.T("Overlay_Streak");
        HeaderFkdr.Text = Loc.T("Overlay_Fkdr");
        HeaderWins.Text = Loc.T("Overlay_Wins");
        HeaderBeds.Text = Loc.T("Overlay_Beds");
        HeaderFinals.Text = Loc.T("Overlay_Finals");
    }

    /// <summary>Zoom factor for the whole overlay panel, set from a slider on the settings window's
    /// Player Lookup tab. Persisted separately from window size since SizeToContent means the
    /// window itself has no user-settable size - only the rendered content does.</summary>
    public double Scale
    {
        get => _scale;
        set
        {
            _scale = Math.Clamp(value, MinScale, MaxScale);
            ContentScale.ScaleX = _scale;
            ContentScale.ScaleY = _scale;
        }
    }

    /// <summary>Adjusts how see-through the overlay's background panel is, independent of the text/rank
    /// colors inside it, so a lighter setting doesn't also fade the stats into unreadability.</summary>
    public void SetBackgroundOpacity(double opacity)
    {
        var alpha = (byte)(Math.Clamp(opacity, 0.0, 1.0) * 255);
        var brush = new SolidColorBrush(Color.FromArgb(alpha, PanelColor.R, PanelColor.G, PanelColor.B));
        brush.Freeze();
        RootBorder.Background = brush;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Make the overlay click-through and non-activating so it never steals
        // focus/input from Minecraft while it's shown.
        _hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT |
                   NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
    }

    public void ShowAtTopLeft()
    {
        var screen = SystemParameters.WorkArea;
        Left = screen.Left + 16;
        Top = screen.Top + 16;
        Show();
    }

    public void HideOverlay()
    {
        Hide();
    }
}
