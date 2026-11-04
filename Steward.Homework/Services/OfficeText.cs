using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Steward.Homework.Services;

/// <summary>
/// Достаёт из Word и презентаций обычный текст и сведения об оформлении, чтобы агент мог прочитать методические указания и образцы
/// </summary>
public static partial class OfficeText
{
    /// <summary>
    /// Пространство имён текста Word
    /// </summary>
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>
    /// Пространство имён текста презентации
    /// </summary>
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>
    /// Читает документ Word: сначала строка с оформлением (шрифт, размер, интервал, поля, выравнивание), потом текст по абзацам с пометкой заголовков и списков
    /// </summary>
    /// <param name="path">Файл .docx</param>
    /// <returns>Текст для чтения агентом</returns>
    public static string Docx(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var document = Load(archive, "word/document.xml") ?? throw new InvalidDataException("В файле нет текста документа");
        var styles = Load(archive, "word/styles.xml");

        var builder = new StringBuilder();
        var hints = FormatHints(document, styles);
        if (hints.Length > 0)
            builder.AppendLine("Оформление документа (из самого файла): " + hints).AppendLine();

        var body = document.Root?.Element(W + "body");
        foreach (var node in body?.Elements() ?? [])
        {
            if (node.Name == W + "p")
            {
                var text = Paragraph(node);
                if (text.Length == 0)
                    continue;

                var style = node.Element(W + "pPr")?.Element(W + "pStyle")?.Attribute(W + "val")?.Value ?? string.Empty;
                var prefix = style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) || style.Equals("Title", StringComparison.OrdinalIgnoreCase) ? "# " : node.Element(W + "pPr")?.Element(W + "numPr") is not null ? "- " : string.Empty;
                builder.AppendLine(prefix + text);
            }
            else if (node.Name == W + "tbl")
            {
                foreach (var row in node.Elements(W + "tr"))
                    builder.AppendLine("| " + string.Join(" | ", row.Elements(W + "tc").Select(cell => string.Join(" ", cell.Elements(W + "p").Select(Paragraph)).Trim())) + " |");
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Читает презентацию: текст каждого слайда по порядку
    /// </summary>
    /// <param name="path">Файл .pptx</param>
    /// <returns>Текст для чтения агентом</returns>
    public static string Pptx(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var slides = archive.Entries
            .Where(entry => SlideName().IsMatch(entry.FullName))
            .OrderBy(entry => int.Parse(SlideName().Match(entry.FullName).Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();

        var builder = new StringBuilder();
        var number = 0;
        foreach (var slide in slides)
        {
            number++;
            using var stream = slide.Open();
            builder.AppendLine($"Слайд {number}:");
            foreach (var paragraph in XDocument.Load(stream).Descendants(A + "p"))
            {
                var text = string.Concat(paragraph.Descendants(A + "t").Select(part => part.Value)).Trim();
                if (text.Length > 0)
                    builder.AppendLine("- " + text);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Собирает текст абзаца Word
    /// </summary>
    /// <param name="paragraph">Элемент абзаца</param>
    /// <returns>Текст без форматирования</returns>
    private static string Paragraph(XElement paragraph) => string.Concat(paragraph.Descendants().Where(node => node.Name == W + "t" || node.Name == W + "tab").Select(node => node.Name == W + "t" ? node.Value : " ")).Trim();

    /// <summary>
    /// Находит часть пакета и читает её как XML
    /// </summary>
    /// <param name="archive">Пакет Office</param>
    /// <param name="name">Путь внутри пакета</param>
    /// <returns>Документ или <see langword="null"/>, если части нет</returns>
    private static XDocument? Load(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name);
        if (entry is null)
            return null;

        using var stream = entry.Open();

        return XDocument.Load(stream);
    }

    /// <summary>
    /// Описывает оформление одной строкой: шрифт, размер, межстрочный интервал, поля и выравнивание основного текста
    /// </summary>
    /// <param name="document">Текст документа</param>
    /// <param name="styles">Стили документа</param>
    /// <returns>Строка вида «шрифт Times New Roman, размер 14 pt, интервал 1.5, поля 30/20/15/20 мм (лево/верх/право/низ), выравнивание по ширине»</returns>
    private static string FormatHints(XDocument document, XDocument? styles)
    {
        var parts = new List<string>();
        var defaults = styles?.Root?.Element(W + "docDefaults");
        var fonts = defaults?.Descendants(W + "rFonts").FirstOrDefault();
        var font = fonts?.Attribute(W + "ascii")?.Value ?? document.Descendants(W + "rFonts").Select(node => node.Attribute(W + "ascii")?.Value).FirstOrDefault(value => !string.IsNullOrEmpty(value));
        if (!string.IsNullOrEmpty(font))
            parts.Add($"шрифт {font}");

        var size = defaults?.Descendants(W + "sz").FirstOrDefault()?.Attribute(W + "val")?.Value ?? document.Descendants(W + "sz").Select(node => node.Attribute(W + "val")?.Value).Where(value => !string.IsNullOrEmpty(value)).GroupBy(value => value).OrderByDescending(group => group.Count()).FirstOrDefault()?.Key;
        if (int.TryParse(size, out var halfPoints))
            parts.Add($"размер {(halfPoints / 2.0).ToString("0.#", CultureInfo.InvariantCulture)} pt");

        var line = defaults?.Descendants(W + "spacing").FirstOrDefault()?.Attribute(W + "line")?.Value ?? document.Descendants(W + "spacing").Select(node => node.Attribute(W + "line")?.Value).FirstOrDefault(value => !string.IsNullOrEmpty(value));
        if (int.TryParse(line, out var twips))
            parts.Add($"интервал {(twips / 240.0).ToString("0.##", CultureInfo.InvariantCulture)}");

        var margin = document.Descendants(W + "pgMar").FirstOrDefault();
        if (margin is not null)
        {
            string Mm(string name) => int.TryParse(margin.Attribute(W + name)?.Value, out var value) ? Math.Round(value / 56.7).ToString(CultureInfo.InvariantCulture) : "?";
            parts.Add($"поля {Mm("left")}/{Mm("top")}/{Mm("right")}/{Mm("bottom")} мм (лево/верх/право/низ)");
        }

        var justification = document.Descendants(W + "jc").Select(node => node.Attribute(W + "val")?.Value).GroupBy(value => value).OrderByDescending(group => group.Count()).FirstOrDefault()?.Key;
        if (justification is "both")
            parts.Add("выравнивание по ширине");

        if (document.Root?.Descendants(W + "footerReference").Any() == true)
            parts.Add("есть нумерация страниц внизу");

        return string.Join(", ", parts);
    }

    [GeneratedRegex(@"^ppt/slides/slide(\d+)\.xml$")]
    private static partial Regex SlideName();
}
