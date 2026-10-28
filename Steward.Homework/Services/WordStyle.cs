using System.Globalization;

namespace Steward.Homework.Services;

/// <summary>
/// Оформление документа Word, которое задают методические указания: шрифт, размер, интервал, поля, выравнивание и красная строка. Агент пишет его блоком в начале текста между строками «---»
/// </summary>
/// <param name="Font">Название шрифта или <see langword="null"/>, тогда берётся шрифт по умолчанию</param>
/// <param name="SizePt">Размер основного текста в пунктах</param>
/// <param name="Spacing">Межстрочный интервал в долях, например 1.5</param>
/// <param name="Margins">Поля в миллиметрах: лево, верх, право, низ</param>
/// <param name="Justify">Выравнивание по ширине</param>
/// <param name="IndentMm">Красная строка в миллиметрах</param>
/// <param name="PageNumbers">Где нумеровать страницы внизу: «left», «center», «right» или <see langword="null"/>, если нумерации нет</param>
public sealed record WordStyle(string? Font, double? SizePt, double? Spacing, (int Left, int Top, int Right, int Bottom)? Margins, bool Justify, double? IndentMm, string? PageNumbers = null)
{
    /// <summary>
    /// Оформление не задано
    /// </summary>
    public static WordStyle None { get; } = new(null, null, null, null, false, null);

    /// <summary>
    /// Отделяет блок оформления от текста и разбирает его. Неизвестные ключи и неверные значения пропускаются
    /// </summary>
    /// <param name="markdown">Текст работы, возможно с блоком оформления в начале</param>
    /// <param name="body">Текст работы без блока оформления</param>
    /// <returns>Оформление или <see cref="None"/>, если блока нет</returns>
    public static WordStyle Extract(string markdown, out string body)
    {
        var text = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).TrimStart();
        body = text;
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
            return None;

        var end = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (end < 0)
            return None;

        body = text[(end + 4)..].TrimStart('\n');
        string? font = null;
        double? size = null;
        double? spacing = null;
        double? indent = null;
        (int, int, int, int)? margins = null;
        var justify = false;
        string? numbers = null;
        foreach (var line in text[4..end].Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
                continue;

            var key = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();
            switch (key)
            {
                case "font" when value.Length > 0:
                    font = value;
                    break;
                case "size":
                    size = Number(value);
                    break;
                case "spacing":
                    spacing = Number(value);
                    break;
                case "indent":
                    indent = Number(value);
                    break;
                case "numbers" when value.ToLowerInvariant() is "left" or "center" or "right":
                    numbers = value.ToLowerInvariant();
                    break;
                case "align":
                    justify = value.Equals("justify", StringComparison.OrdinalIgnoreCase);
                    break;
                case "margins":
                    var parts = value.Split([',', '/', ' ', ';'], StringSplitOptions.RemoveEmptyEntries).Select(part => (int?)(Number(part) is { } mm ? (int)Math.Round(mm) : null)).ToArray();
                    if (parts.Length == 4 && parts.All(part => part is > 0 and < 100))
                        margins = (parts[0]!.Value, parts[1]!.Value, parts[2]!.Value, parts[3]!.Value);

                    break;
            }
        }

        return new WordStyle(font, size is > 5 and < 40 ? size : null, spacing is > 0.5 and < 4 ? spacing : null, margins, justify, indent is > 0 and < 50 ? indent : null, numbers);
    }

    /// <summary>
    /// Читает число, допуская запятую вместо точки и единицы после числа
    /// </summary>
    /// <param name="text">Текст вроде «14», «1,5» или «12.5 мм»</param>
    /// <returns>Число или <see langword="null"/></returns>
    private static double? Number(string text)
    {
        var digits = new string([.. text.TakeWhile(symbol => char.IsDigit(symbol) || symbol is '.' or ',')]).Replace(',', '.');

        return double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}
