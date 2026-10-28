using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml.Linq;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;

namespace Steward.Preview;

/// <summary>
/// Собирает из файла читаемый документ для предпросмотра: Word, презентации, текст, Markdown и код
/// </summary>
public static partial class DocumentPreviewBuilder
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
    /// Пространство имён слайда
    /// </summary>
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    /// <summary>
    /// Расширения, которые показываются как код
    /// </summary>
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".py", ".js", ".ts", ".tsx", ".jsx", ".java", ".kt", ".cpp", ".c", ".h", ".go", ".rs", ".php", ".rb", ".sql", ".json", ".xml", ".html", ".css", ".yml", ".yaml", ".sh", ".ps1", ".lua", ".gd", ".swift",
    };

    /// <summary>
    /// Ключевые слова, которые подсвечиваются в коде на любом языке
    /// </summary>
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "if", "else", "elif", "for", "foreach", "while", "do", "switch", "case", "break", "continue", "return", "class", "struct", "enum", "interface", "public", "private", "protected", "static", "void", "int", "float", "double", "bool", "string", "var", "let", "const", "new", "null", "None", "true", "false", "True", "False", "def", "function", "import", "from", "using", "namespace", "try", "catch", "finally", "throw", "async", "await", "self", "this", "in", "is", "not", "and", "or", "extends", "func", "end", "then", "local",
    };

    /// <summary>
    /// Открывает файл и строит документ
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <returns>Документ для просмотра. Если файл не читается, в документе написано почему</returns>
    public static FlowDocument Build(string path)
    {
        var document = NewDocument();
        try
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".docx":
                    Word(document, path);
                    break;
                case ".pptx":
                    Slides(document, path);
                    break;
                case ".md" or ".markdown":
                    Markdown(document, File.ReadAllText(path));
                    break;
                case ".txt" or "":
                    Plain(document, File.ReadAllText(path));
                    break;
                default:
                    if (CodeExtensions.Contains(Path.GetExtension(path)))
                        Code(document, File.ReadAllText(path));
                    else
                        Note(document, "Для этого формата предпросмотра нет. Файл можно открыть кнопкой «Открыть файл».");
                    break;
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            document.Blocks.Clear();
            Note(document, "Не удалось открыть файл: " + exception.Message);
        }

        return document;
    }

    /// <summary>
    /// Строит документ из готового текста Markdown: для расшифровок и заметок
    /// </summary>
    /// <param name="text">Текст</param>
    /// <returns>Документ для просмотра</returns>
    public static FlowDocument FromMarkdown(string text)
    {
        var document = NewDocument();
        Markdown(document, text);

        return document;
    }

    /// <summary>
    /// Строит документ из простого текста: каждая строка отдельным абзацем, например реплики расшифровки
    /// </summary>
    /// <param name="text">Текст</param>
    /// <returns>Документ для просмотра</returns>
    public static FlowDocument FromText(string text)
    {
        var document = NewDocument();
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            document.Blocks.Add(new Paragraph(new Run(line)) { Margin = new Thickness(0, 0, 0, 6) });

        return document;
    }

    #region Documents

    /// <summary>
    /// Создаёт пустой документ в оформлении приложения
    /// </summary>
    /// <returns>Пустой документ</returns>
    private static FlowDocument NewDocument() => new()
    {
        FontFamily = (FontFamily)Application.Current.FindResource("FontBase"),
        FontSize = 14,
        LineHeight = 22,
        Foreground = Paint("TextBrush"),
        Background = Brushes.Transparent,
        PagePadding = new Thickness(4, 4, 12, 12),
        TextAlignment = TextAlignment.Left,
    };

    /// <summary>
    /// Находит кисть оформления приложения
    /// </summary>
    /// <param name="key">Имя кисти</param>
    /// <returns>Кисть</returns>
    private static System.Windows.Media.Brush Paint(string key) => (System.Windows.Media.Brush)Application.Current.FindResource(key);

    /// <summary>
    /// Пишет в документ короткое сообщение
    /// </summary>
    /// <param name="document">Документ</param>
    /// <param name="text">Сообщение</param>
    private static void Note(FlowDocument document, string text) => document.Blocks.Add(new Paragraph(new Run(text)) { Foreground = Paint("MutedBrush") });

    /// <summary>
    /// Простой текст: абзацы по пустым строкам
    /// </summary>
    /// <param name="document">Документ</param>
    /// <param name="text">Текст</param>
    private static void Plain(FlowDocument document, string text)
    {
        foreach (var block in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            document.Blocks.Add(new Paragraph(new Run(block.Trim())) { Margin = new Thickness(0, 0, 0, 10) });
    }

    #endregion Documents

    #region Markdown

    /// <summary>
    /// Markdown: заголовки, списки, блоки кода, жирный текст и код в строке
    /// </summary>
    /// <param name="document">Документ</param>
    /// <param name="text">Текст Markdown</param>
    private static void Markdown(FlowDocument document, string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var code = new List<string>();
        var inCode = false;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (inCode)
                {
                    Code(document, string.Join('\n', code));
                    code.Clear();
                }

                inCode = !inCode;
                continue;
            }

            if (inCode)
            {
                code.Add(raw);
                continue;
            }

            if (line.Length == 0)
                continue;

            var heading = HeadingRegex().Match(line);
            if (heading.Success)
            {
                var level = heading.Groups[1].Length;
                document.Blocks.Add(new Paragraph(Inline(heading.Groups[2].Value)) { FontSize = level switch { 1 => 24, 2 => 19, _ => 16 }, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, level == 1 ? 4 : 14, 0, 6) });
                continue;
            }

            var bullet = BulletRegex().Match(line);
            if (bullet.Success)
            {
                var paragraph = new Paragraph { Margin = new Thickness(18 + (bullet.Groups[1].Length * 6), 0, 0, 4), TextIndent = -14 };
                paragraph.Inlines.Add(new Run((char.IsDigit(bullet.Groups[2].Value[0]) ? bullet.Groups[2].Value : "•") + "  ") { Foreground = Paint("AccentTextBrush") });
                paragraph.Inlines.Add(Inline(bullet.Groups[3].Value));
                document.Blocks.Add(paragraph);
                continue;
            }

            document.Blocks.Add(new Paragraph(Inline(line)) { Margin = new Thickness(0, 0, 0, 8) });
        }

        if (code.Count > 0)
            Code(document, string.Join('\n', code));
    }

    /// <summary>
    /// Разбирает жирный текст и код внутри строки
    /// </summary>
    /// <param name="text">Строка</param>
    /// <returns>Фрагмент с форматированием</returns>
    private static Span Inline(string text)
    {
        var span = new Span();
        var last = 0;
        foreach (Match match in InlineRegex().Matches(text))
        {
            if (match.Index > last)
                span.Inlines.Add(new Run(text[last..match.Index]));

            if (match.Groups[1].Success)
                span.Inlines.Add(new Run(match.Groups[1].Value) { FontWeight = FontWeights.SemiBold });
            else
                span.Inlines.Add(new Run(match.Groups[2].Value) { FontFamily = new FontFamily("Consolas"), Background = Paint("RaisedBrush"), Foreground = Paint("AccentTextBrush") });

            last = match.Index + match.Length;
        }

        if (last < text.Length)
            span.Inlines.Add(new Run(text[last..]));

        return span;
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(\s*)([-*•]|\d+[.)])\s+(.*)$")]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*|`([^`]+)`")]
    private static partial Regex InlineRegex();

    #endregion Markdown

    #region Code

    /// <summary>
    /// Код: моноширинный шрифт, номера строк и подсветка строк, комментариев, чисел и ключевых слов
    /// </summary>
    /// <param name="document">Документ</param>
    /// <param name="text">Исходный код</param>
    private static void Code(FlowDocument document, string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd().Split('\n');
        var section = new Section { Background = Paint("WindowBrush"), BorderBrush = Paint("LineStrongBrush"), BorderThickness = new Thickness(1), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 10) };
        var width = lines.Length.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
        var paragraph = new Paragraph { FontFamily = new FontFamily("Consolas"), FontSize = 13, LineHeight = 19, Margin = new Thickness(0) };
        for (var i = 0; i < lines.Length; i++)
        {
            paragraph.Inlines.Add(new Run((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(width) + "  ") { Foreground = Paint("MutedBrush") });
            foreach (var token in Highlight(lines[i].Replace("\t", "    ", StringComparison.Ordinal)))
                paragraph.Inlines.Add(token);

            if (i < lines.Length - 1)
                paragraph.Inlines.Add(new LineBreak());
        }

        section.Blocks.Add(paragraph);
        document.Blocks.Add(section);
    }

    /// <summary>
    /// Раскрашивает одну строку кода
    /// </summary>
    /// <param name="line">Строка</param>
    /// <returns>Куски строки с цветом</returns>
    private static IEnumerable<Run> Highlight(string line)
    {
        var last = 0;
        foreach (Match match in TokenRegex().Matches(line))
        {
            if (match.Index > last)
                yield return new Run(line[last..match.Index]);

            var brush = match.Groups["comment"].Success ? "MutedBrush"
                : match.Groups["string"].Success ? "OkBrush"
                : match.Groups["number"].Success ? "AttentionBrush"
                : Keywords.Contains(match.Value) ? "AccentTextBrush"
                : "TextBrush";
            yield return new Run(match.Value) { Foreground = Paint(brush) };
            last = match.Index + match.Length;
        }

        if (last < line.Length)
            yield return new Run(line[last..]);
    }

    [GeneratedRegex(@"(?<comment>//.*|#.*|--.*)|(?<string>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*')|(?<number>\b\d+(?:\.\d+)?\b)|(?<word>[A-Za-z_][A-Za-z_0-9]*)")]
    private static partial Regex TokenRegex();

    #endregion Code

    #region Word

    /// <summary>
    /// Word: заголовки, абзацы, списки, жирный и курсив, таблицы
    /// </summary>
    /// <param name="document">Документ</param>
    /// <param name="path">Файл .docx</param>
    private static void Word(FlowDocument document, string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("В файле нет текста документа");
        using var stream = entry.Open();
        var body = XDocument.Load(stream).Root?.Element(W + "body") ?? throw new InvalidDataException("В файле нет текста документа");

        foreach (var node in body.Elements())
        {
            if (node.Name == W + "p")
                document.Blocks.Add(WordParagraph(node));
            else if (node.Name == W + "tbl")
                document.Blocks.Add(WordTable(node));
        }

        if (document.Blocks.Count == 0)
            Note(document, "В документе нет текста.");
    }

    /// <summary>
    /// Абзац Word с учётом стиля заголовка и списка
    /// </summary>
    /// <param name="node">Элемент абзаца</param>
    /// <returns>Абзац</returns>
    private static Paragraph WordParagraph(XElement node)
    {
        var style = node.Element(W + "pPr")?.Element(W + "pStyle")?.Attribute(W + "val")?.Value ?? string.Empty;
        var list = node.Element(W + "pPr")?.Element(W + "numPr") is not null;
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
        if (style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) || style.Equals("Title", StringComparison.OrdinalIgnoreCase))
        {
            var level = style.Equals("Title", StringComparison.OrdinalIgnoreCase) ? 1 : (int.TryParse(style["Heading".Length..], out var number) ? number : 2);
            paragraph.FontSize = level switch { 1 => 24, 2 => 19, _ => 16 };
            paragraph.FontWeight = FontWeights.SemiBold;
            paragraph.Margin = new Thickness(0, level == 1 ? 4 : 14, 0, 6);
        }
        else if (list)
        {
            paragraph.Margin = new Thickness(18, 0, 0, 4);
            paragraph.TextIndent = -14;
            paragraph.Inlines.Add(new Run("•  ") { Foreground = Paint("AccentTextBrush") });
        }

        foreach (var run in node.Descendants(W + "r"))
        {
            var props = run.Element(W + "rPr");
            var text = string.Concat(run.Elements().Where(item => item.Name == W + "t" || item.Name == W + "tab" || item.Name == W + "br").Select(item => item.Name == W + "t" ? item.Value : item.Name == W + "tab" ? "    " : "\n"));
            if (text.Length == 0)
                continue;

            var piece = new Run(text);
            if (props?.Element(W + "b") is not null)
                piece.FontWeight = FontWeights.SemiBold;

            if (props?.Element(W + "i") is not null)
                piece.FontStyle = FontStyles.Italic;

            if (props?.Element(W + "u") is not null)
                piece.TextDecorations = TextDecorations.Underline;

            paragraph.Inlines.Add(piece);
        }

        return paragraph;
    }

    /// <summary>
    /// Таблица Word
    /// </summary>
    /// <param name="node">Элемент таблицы</param>
    /// <returns>Таблица</returns>
    private static Table WordTable(XElement node)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 10), BorderBrush = Paint("LineStrongBrush"), BorderThickness = new Thickness(1) };
        var group = new TableRowGroup();
        var columns = 0;
        foreach (var row in node.Elements(W + "tr"))
        {
            var tableRow = new TableRow();
            foreach (var cell in row.Elements(W + "tc"))
            {
                var text = string.Join("\n", cell.Elements(W + "p").Select(item => string.Concat(item.Descendants(W + "t").Select(part => part.Value))));
                tableRow.Cells.Add(new TableCell(new Paragraph(new Run(text))) { Padding = new Thickness(8, 4, 8, 4), BorderBrush = Paint("LineStrongBrush"), BorderThickness = new Thickness(0.5) });
            }

            columns = Math.Max(columns, tableRow.Cells.Count);
            group.Rows.Add(tableRow);
        }

        for (var i = 0; i < columns; i++)
            table.Columns.Add(new TableColumn());

        table.RowGroups.Add(group);

        return table;
    }

    #endregion Word

    #region Slides

    /// <summary>
    /// Презентация: каждый слайд отдельной карточкой с заголовком и текстом
    /// </summary>
    /// <param name="document">Документ</param>
    /// <param name="path">Файл .pptx</param>
    private static void Slides(FlowDocument document, string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var slides = archive.Entries
            .Where(entry => SlideNameRegex().IsMatch(entry.FullName))
            .OrderBy(entry => int.Parse(SlideNameRegex().Match(entry.FullName).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

        if (slides.Length == 0)
            throw new InvalidDataException("В файле нет слайдов");

        var number = 0;
        foreach (var slide in slides)
        {
            number++;
            using var stream = slide.Open();
            var root = XDocument.Load(stream).Root!;
            var section = new Section { Background = Paint("SurfaceBrush"), BorderBrush = Paint("LineStrongBrush"), BorderThickness = new Thickness(1), Padding = new Thickness(18, 12, 18, 12), Margin = new Thickness(0, 0, 0, 12) };
            section.Blocks.Add(new Paragraph(new Run($"Слайд {number} из {slides.Length}")) { FontSize = 11, Foreground = Paint("MutedBrush"), Margin = new Thickness(0, 0, 0, 6) });

            foreach (var shape in root.Descendants(P + "sp"))
            {
                var kind = shape.Descendants(P + "ph").FirstOrDefault()?.Attribute("type")?.Value;
                var isTitle = kind is "title" or "ctrTitle";
                foreach (var paragraph in shape.Descendants(A + "p"))
                {
                    var text = string.Concat(paragraph.Descendants(A + "t").Select(part => part.Value)).Trim();
                    if (text.Length == 0)
                        continue;

                    section.Blocks.Add(isTitle
                        ? new Paragraph(new Run(text)) { FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) }
                        : new Paragraph(new Run("•  " + text)) { Margin = new Thickness(8, 0, 0, 4), TextIndent = -14 });
                }
            }

            document.Blocks.Add(section);
        }
    }

    [GeneratedRegex(@"^ppt/slides/slide(\d+)\.xml$")]
    private static partial Regex SlideNameRegex();

    #endregion Slides
}
