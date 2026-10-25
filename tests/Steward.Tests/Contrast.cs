using System.Globalization;

namespace Steward.Tests;

/// <summary>
/// Считает контраст двух цветов по формуле WCAG
/// </summary>
public static class Contrast
{
    /// <summary>
    /// Отношение яркостей: от 1 (одинаковые цвета) до 21 (чёрный на белом)
    /// </summary>
    /// <param name="first">Цвет вида <c>#RRGGBB</c></param>
    /// <param name="second">Цвет вида <c>#RRGGBB</c></param>
    /// <returns>Отношение, большая яркость делится на меньшую</returns>
    public static double Ratio(string first, string second)
    {
        var light = Luminance(first);
        var dark = Luminance(second);

        return (Math.Max(light, dark) + 0.05) / (Math.Min(light, dark) + 0.05);
    }

    /// <summary>
    /// Относительная яркость цвета
    /// </summary>
    /// <param name="hex">Цвет вида <c>#RRGGBB</c></param>
    /// <returns>Яркость от 0 до 1</returns>
    private static double Luminance(string hex)
    {
        double Channel(int start)
        {
            var value = int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;

            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
    }
}