using System.Globalization;

namespace Steward.Homework.Services;

/// <summary>
/// Оформление документа Word, которое задают методические указания: шрифт, размер, интервал, поля, выравнивание и красная строка. Агент пишет его блоком в начале текста между строками «---»
/// </summary>
/// <param name="Font">Название шрифта или <see langword="null"/>, тогда берётся шрифт по умолчанию</param>
/// <param name="SizePt">Размер основного текста в пунктах</param>
/// <param name="Spacing">Межстрочный интервал в долях, например 1.5</param>
/// <param name="Margins">Поля в миллиметрах: лево, верх, право, низ</param>
/// <param name="Justify">Выравнивание по ширине: <see langword="true"/> по ширине, <see langword="false"/> по левому краю, <see langword="null"/> не задано</param>
/// <param name="IndentMm">Красная строка в миллиметрах</param>
/// <param name="PageNumbers">Где нумеровать страницы внизу: «left», «center», «right» или <see langword="null"/>, если нумерации нет</param>
public sealed record WordStyle(string? Font, double? SizePt, double? Spacing, (int Left, int Top, int Right, int Bottom)? Margins, bool? Justify, double? IndentMm, string? PageNumbers = null)
{
    /// <summary>
    /// Оформление не задано
    /// </summary>
    public static WordStyle None { get; } = new(null, null, null, null, null, null);

    /// <summary>
    /// Обычное оформление учебных работ: Times New Roman 14 пт, полуторный интервал, по ширине, красная строка 1,25 см, поля 30, 20, 15, 20 мм, номера страниц по центру. Берётся там, где ни указания, ни образцы владельца оформления не задали
    /// </summary>
    public static WordStyle Academic { get; } = new("Times New Roman", 14, 1.5, (30, 20, 15, 20), true, 12.5, "center");

    /// <summary>
    /// Дополняет оформление недостающими значениями другого: то, что задано здесь, остаётся, остальное берётся из <paramref name="fallback"/>
    /// </summary>
    /// <param name="fallback">Оформление, из которого берутся незаданные значения</param>
    /// <returns>Полное оформление</returns>
    public WordStyle Over(WordStyle fallback) => new(
        Font ?? fallback.Font,
        SizePt ?? fallback.SizePt,
        Spacing ?? fallback.Spacing,
        Margins ?? fallback.Margins,
        Justify ?? fallback.Justify,
        IndentMm ?? fallback.IndentMm,
        PageNumbers ?? fallback.PageNumbers);

    /// <summary>
    /// Измеряет оформление в готовых работах владельца (файлы .docx): шрифт, размер, интервал, красную строку, выравнивание и поля. По каждому значению берётся то, что встречается в образцах чаще всего. Файлы, которые не читаются, пропускаются
    /// </summary>
    /// <param name="files">Файлы образцов</param>
    /// <returns>Измеренное оформление или <see langword="null"/>, если ничего измерить не удалось</returns>
    public static WordStyle? Measure(IEnumerable<string> files)
    {
        var measured = files.Select(MeasureOne).OfType<WordStyle>().ToArray();
        if (measured.Length == 0)
            return null;

        T? Mode<T>(Func<WordStyle, T?> pick) where T : struct => measured.Select(pick).Where(value => value is not null).GroupBy(value => value).OrderByDescending(group => group.Count()).Select(group => group.Key).FirstOrDefault();

        string? font = measured.Select(style => style.Font).Where(value => value is not null).GroupBy(value => value).OrderByDescending(group => group.Count()).Select(group => group.Key).FirstOrDefault();
        (int, int, int, int)? margins = measured.Select(style => style.Margins).Where(value => value is not null).GroupBy(value => value).OrderByDescending(group => group.Count()).Select(group => group.Key).FirstOrDefault();

        return new WordStyle(font, Mode(style => style.SizePt), Mode(style => style.Spacing), margins is { } found ? (found.Item1, found.Item2, found.Item3, found.Item4) : null, Mode(style => style.Justify), Mode(style => style.IndentMm));
    }

    /// <summary>
    /// Измеряет оформление одного файла .docx по стилю документа по умолчанию и полям первого раздела
    /// </summary>
    /// <param name="file">Файл .docx</param>
    /// <returns>Оформление или <see langword="null"/>, если файл не читается или в нём ничего нет</returns>
    private static WordStyle? MeasureOne(string file)
    {
        const long LIMIT = 30_000_000;
        try
        {
            if (!file.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
                return null;

            using var archive = System.IO.Compression.ZipFile.OpenRead(file);
            System.Xml.Linq.XDocument? Part(string name) => archive.GetEntry(name) is { Length: > 0 and <= LIMIT } entry ? System.Xml.Linq.XDocument.Load(entry.Open()) : null;
            System.Xml.Linq.XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var styles = Part("word/styles.xml");
            var document = Part("word/document.xml");
            string? Attribute(System.Xml.Linq.XElement? element, string name) => element?.Attribute(w + name)?.Value;

            // Стиль «по умолчанию»: общие настройки документа, а поверх них стиль обычного абзаца
            var defaults = styles?.Root?.Element(w + "docDefaults");
            var normal = styles?.Root?.Elements(w + "style").FirstOrDefault(style => Attribute(style, "type") == "paragraph" && Attribute(style, "default") == "1");
            System.Xml.Linq.XElement? Run(string tag) => normal?.Element(w + "rPr")?.Element(w + tag) ?? defaults?.Element(w + "rPrDefault")?.Element(w + "rPr")?.Element(w + tag);
            System.Xml.Linq.XElement? Paragraph(string tag) => normal?.Element(w + "pPr")?.Element(w + tag) ?? defaults?.Element(w + "pPrDefault")?.Element(w + "pPr")?.Element(w + tag);

            var font = Attribute(Run("rFonts"), "ascii");
            double? size = double.TryParse(Attribute(Run("sz"), "val"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var half) ? half / 2 : null;
            double? spacing = Attribute(Paragraph("spacing"), "lineRule") is null or "auto" && double.TryParse(Attribute(Paragraph("spacing"), "line"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var line) ? Math.Round(line / 240, 2) : null;
            double? indent = double.TryParse(Attribute(Paragraph("ind"), "firstLine"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var first) ? Math.Round(first / 56.7, 1) : null;
            bool? justify = Attribute(Paragraph("jc"), "val") is { } alignment ? alignment is "both" or "distribute" : null;
            var pageMargin = document?.Descendants(w + "pgMar").FirstOrDefault();
            (int, int, int, int)? margins = null;
            if (pageMargin is not null)
            {
                int? Mm(string name) => int.TryParse(Attribute(pageMargin, name), out var twips) ? (int)Math.Round(twips / 56.7) : null;
                if (Mm("left") is { } left && Mm("top") is { } top && Mm("right") is { } right && Mm("bottom") is { } bottom)
                    margins = (left, top, right, bottom);
            }

            return font is null && size is null && spacing is null && indent is null && justify is null && margins is null
                ? null
                : new WordStyle(font, size is > 5 and < 40 ? size : null, spacing is > 0.5 and < 4 ? spacing : null, margins is { } found ? (found.Item1, found.Item2, found.Item3, found.Item4) : null, justify, indent is > 0 and < 50 ? indent : null);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Xml.XmlException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Описывает оформление словами, чтобы вставить его в просьбу агенту
    /// </summary>
    /// <returns>Строка вроде «шрифт Times New Roman, размер 14 пт, межстрочный интервал 1,5»</returns>
    public string Describe()
    {
        static string Number(double value) => value.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
        var parts = new List<string>();
        if (Font is not null)
            parts.Add($"шрифт {Font}");

        if (SizePt is { } size)
            parts.Add($"размер {Number(size)} пт");

        if (Spacing is { } spacing)
            parts.Add($"межстрочный интервал {Number(spacing)}");

        if (Justify is { } justify)
            parts.Add(justify ? "выравнивание по ширине" : "выравнивание по левому краю");

        if (IndentMm is { } indent)
            parts.Add($"красная строка {Number(indent)} мм");

        if (Margins is { } margins)
            parts.Add($"поля {margins.Left}, {margins.Top}, {margins.Right}, {margins.Bottom} мм (лево, верх, право, низ)");

        if (PageNumbers is { } numbers)
            parts.Add($"номера страниц внизу: {numbers}");

        return string.Join(", ", parts);
    }

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
        bool? justify = null;
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
