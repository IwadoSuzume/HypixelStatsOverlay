using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace HypixelStatsOverlay.Converters;

/// <summary>
/// Maps a Bedwars star/level to Hypixel's prestige color banding (matches the
/// coloring Hypixel itself uses in the tab list / level display).
/// Levels 1000+ cycle through the same 10 colors again per 1000-block "prestige";
/// this is rendered as a gold/rainbow-ish accent rather than reproducing every
/// exact per-prestige icon/color, since that detail isn't load-bearing for at-a-glance
/// skill assessment.
/// </summary>
public class BedwarsLevelToBrushConverter : IValueConverter
{
    private static readonly Color[] Tiers =
    {
        Color.FromRgb(0xAA, 0xAA, 0xAA), // 0-99   Stone    - Gray
        Colors.White,                     // 100-199 Iron     - White
        Color.FromRgb(0xFF, 0xAA, 0x00), // 200-299 Gold     - Gold
        Color.FromRgb(0x55, 0xFF, 0xFF), // 300-399 Diamond  - Aqua
        Color.FromRgb(0x00, 0xAA, 0x00), // 400-499 Emerald  - Dark Green
        Color.FromRgb(0x00, 0xAA, 0xAA), // 500-599 Sapphire - Dark Aqua
        Color.FromRgb(0xAA, 0x00, 0x00), // 600-699 Ruby     - Dark Red
        Color.FromRgb(0xFF, 0x55, 0xFF), // 700-799 Crystal  - Light Purple
        Color.FromRgb(0x55, 0x55, 0xFF), // 800-899 Opal     - Blue
        Color.FromRgb(0xAA, 0x00, 0xAA), // 900-999 Amethyst - Dark Purple
    };

    private static readonly Color RainbowAccent = Color.FromRgb(0xFF, 0xD7, 0x00); // 1000+ gold accent

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value switch
        {
            double d => (int)d,
            int i => i,
            _ => 0
        };

        var color = level >= 1000
            ? RainbowAccent
            : Tiers[Math.Clamp(level / 100, 0, Tiers.Length - 1)];

        // Bindable straight onto Color-typed properties (e.g. DropShadowEffect.Color for a
        // level-tinted glow) as well as the usual Brush-typed ones (Foreground, Background, ...).
        return targetType == typeof(Color) ? color : new SolidColorBrush(color);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
