using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HypixelStatsOverlay.Converters;

/// <summary>Turns a 0-1 progress fraction into a GridLength for a two-column "track" Grid: the fill
/// column gets Star(progress), the remainder column gets Star(1-progress) via ConverterParameter="Invert" -
/// Star sizing keeps the bar responsive to the card's actual width instead of a hardcoded pixel figure.</summary>
public class ProgressToStarConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var progress = Math.Clamp(value is double d ? d : 0, 0, 1);
        if (parameter is string s && s == "Invert")
            progress = 1 - progress;

        // A zero-width Star column can make WPF collapse the whole track oddly, so floor it just above zero.
        return new GridLength(Math.Max(progress, 0.0001), GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
