using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace HypixelStatsOverlay.Converters;

/// <summary>Bands FKDR into the requested at-a-glance thresholds: below 1 default, 1-2.99 green, 3-3.99 orange, 4+ red.</summary>
public class FkdrToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Default = new(Colors.White);
    private static readonly SolidColorBrush Green = new(Color.FromRgb(0x4C, 0xD4, 0x63));
    private static readonly SolidColorBrush Orange = new(Color.FromRgb(0xFF, 0x55, 0x00));
    private static readonly SolidColorBrush Red = new(Color.FromRgb(0xFF, 0x3B, 0x30));

    static FkdrToBrushConverter()
    {
        Default.Freeze();
        Green.Freeze();
        Orange.Freeze();
        Red.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fkdr = value is double d ? d : 0;

        return fkdr switch
        {
            >= 4 => Red,
            >= 3 => Orange,
            >= 1 => Green,
            _ => Default
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
