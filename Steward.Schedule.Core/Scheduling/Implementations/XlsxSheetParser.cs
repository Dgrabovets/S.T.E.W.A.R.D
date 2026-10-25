using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Steward.Schedule.Scheduling.Implementations;

/// <summary>
/// Читает первый лист книги xlsx и раскрывает объединённые ячейки
/// </summary>
public static class XlsxSheetParser
{
    /// <summary>
    /// Пространство имён разметки листа Office Open XML
    /// </summary>
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>
    /// Пространство имён связей пакета, где лежат адреса гиперссылок
    /// </summary>
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>
    /// Пространство имён атрибута <c>r:id</c> у гиперссылки ячейки
    /// </summary>
    private static readonly XNamespace OfficeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>
    /// Превращает книгу в CSV, копируя значение левой верхней ячейки на весь объединённый диапазон
    /// </summary>
    /// <param name="xlsx">
    /// Байты файла xlsx
    /// </param>
    /// <returns>
    /// Текст CSV. Числовые даты Excel записываются как <c>DD.MM</c>. Адрес гиперссылки ячейки дописывается отдельной строкой
    /// </returns>
    /// <exception cref="FormatException">
    /// В книге нет листа
    /// </exception>
    public static string ToCsv(byte[] xlsx)
    {
        using var archive = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        var strings = ReadSharedStrings(archive);
        var sheet = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? throw new FormatException("The workbook does not contain a worksheet.");
        using var sheetStream = sheet.Open();
        var document = XDocument.Load(sheetStream);
        var cells = ReadCells(document, strings);
        ApplyMerges(document, cells);
        var links = ReadHyperlinks(archive, document);
        ApplyMergeLinks(document, links);
        AppendLinks(cells, links);
        return WriteCsv(cells);
    }

    /// <summary>
    /// Читает общую таблицу строк книги
    /// </summary>
    /// <param name="archive">
    /// Открытая книга xlsx
    /// </param>
    /// <returns>
    /// Строки в порядке индексов. Пустой список, если таблицы нет
    /// </returns>
    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
            return [];

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Descendants(Spreadsheet + "si")
            .Select(item => string.Concat(item.Descendants(Spreadsheet + "t").Select(text => text.Value)))
            .ToList();
    }

    /// <summary>
    /// Собирает непустые ячейки первого листа
    /// </summary>
    /// <param name="document">
    /// Документ листа
    /// </param>
    /// <param name="sharedStrings">
    /// Общая таблица строк
    /// </param>
    /// <returns>
    /// Текст ячеек по координатам с нуля
    /// </returns>
    private static Dictionary<(int Row, int Column), string> ReadCells(XDocument document, List<string> sharedStrings)
    {
        var cells = new Dictionary<(int Row, int Column), string>();
        foreach (var cell in document.Descendants(Spreadsheet + "c"))
        {
            var reference = (string?)cell.Attribute("r");
            if (reference is null || !TryReference(reference, out var row, out var column))
                continue;

            var text = CellText(cell, sharedStrings);
            if (text.Length > 0)
                cells[(row, column)] = text;
        }

        return cells;
    }

    /// <summary>
    /// Копирует значение левой верхней ячейки в пустые ячейки её объединения
    /// </summary>
    /// <param name="document">
    /// Документ листа
    /// </param>
    /// <param name="cells">
    /// Уже прочитанные ячейки. Пустые позиции объединения заполняются на месте
    /// </param>
    private static void ApplyMerges(XDocument document, Dictionary<(int Row, int Column), string> cells)
    {
        foreach (var merge in document.Descendants(Spreadsheet + "mergeCell"))
        {
            var range = (string?)merge.Attribute("ref");
            if (range is null)
                continue;

            var bounds = range.Split(':');
            if (bounds.Length != 2
                || !TryReference(bounds[0], out var startRow, out var startColumn)
                || !TryReference(bounds[1], out var endRow, out var endColumn)
                || !cells.TryGetValue((startRow, startColumn), out var value))
            {
                continue;
            }

            for (var row = startRow; row <= endRow; row++)
            {
                for (var column = startColumn; column <= endColumn; column++)
                {
                    if (!cells.ContainsKey((row, column)))
                        cells[(row, column)] = value;
                }
            }
        }
    }

    /// <summary>
    /// Записывает сетку в CSV
    /// </summary>
    /// <param name="cells">
    /// Ячейки по координатам с нуля
    /// </param>
    /// <returns>
    /// Текст CSV
    /// </returns>
    private static string WriteCsv(Dictionary<(int Row, int Column), string> cells)
    {
        if (cells.Count == 0)
            return "";

        var rows = cells.Keys.Max(cell => cell.Row);
        var columns = cells.Keys.Max(cell => cell.Column);
        var builder = new StringBuilder();
        for (var row = 0; row <= rows; row++)
        {
            for (var column = 0; column <= columns; column++)
            {
                if (column > 0)
                    builder.Append(',');

                if (cells.TryGetValue((row, column), out var value))
                    builder.Append(Quote(value));
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    #region Hyperlinks

    /// <summary>
    /// Читает внешние гиперссылки листа
    /// </summary>
    /// <param name="archive">
    /// Открытая книга xlsx
    /// </param>
    /// <param name="document">
    /// Документ листа
    /// </param>
    /// <returns>
    /// Адрес ссылки по координатам ячейки с нуля. Внутренние ссылки пропускаются
    /// </returns>
    private static Dictionary<(int Row, int Column), string> ReadHyperlinks(ZipArchive archive, XDocument document)
    {
        var targets = ReadHyperlinkTargets(archive);
        var links = new Dictionary<(int Row, int Column), string>();
        foreach (var link in document.Descendants(Spreadsheet + "hyperlink"))
        {
            var reference = (string?)link.Attribute("ref");
            var id = (string?)link.Attribute(OfficeRelationships + "id");
            if (reference is null || id is null || !targets.TryGetValue(id, out var target))
                continue;

            if (!target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var cell in CellsInReference(reference))
                links[cell] = target;
        }

        return links;
    }

    /// <summary>
    /// Читает цели внешних гиперссылок из связей листа
    /// </summary>
    /// <param name="archive">
    /// Открытая книга xlsx
    /// </param>
    /// <returns>
    /// Адрес по идентификатору связи. Пустой словарь, если файла связей нет
    /// </returns>
    private static Dictionary<string, string> ReadHyperlinkTargets(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/worksheets/_rels/sheet1.xml.rels");
        if (entry is null)
            return [];

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        var targets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var relationship in document.Descendants(PackageRelationships + "Relationship"))
        {
            var id = (string?)relationship.Attribute("Id");
            var type = (string?)relationship.Attribute("Type");
            var target = (string?)relationship.Attribute("Target");
            if (id is null || target is null || type is null || !type.EndsWith("/hyperlink", StringComparison.Ordinal))
                continue;

            targets[id] = target;
        }

        return targets;
    }

    /// <summary>
    /// Копирует ссылку левой верхней ячейки на пустые позиции объединения
    /// </summary>
    /// <param name="document">
    /// Документ листа
    /// </param>
    /// <param name="links">
    /// Уже прочитанные ссылки. Пустые позиции объединения заполняются на месте
    /// </param>
    private static void ApplyMergeLinks(XDocument document, Dictionary<(int Row, int Column), string> links)
    {
        foreach (var merge in document.Descendants(Spreadsheet + "mergeCell"))
        {
            var range = (string?)merge.Attribute("ref");
            if (range is null)
                continue;

            var bounds = range.Split(':');
            if (bounds.Length != 2
                || !TryReference(bounds[0], out var startRow, out var startColumn)
                || !TryReference(bounds[1], out var endRow, out var endColumn)
                || !links.TryGetValue((startRow, startColumn), out var value))
            {
                continue;
            }

            for (var row = startRow; row <= endRow; row++)
            {
                for (var column = startColumn; column <= endColumn; column++)
                {
                    if (!links.ContainsKey((row, column)))
                        links[(row, column)] = value;
                }
            }
        }
    }

    /// <summary>
    /// Дописывает адрес ссылки отдельной строкой, если его ещё нет в тексте ячейки
    /// </summary>
    /// <param name="cells">
    /// Текст ячеек. Строка со ссылкой добавляется на месте
    /// </param>
    /// <param name="links">
    /// Ссылки по координатам
    /// </param>
    private static void AppendLinks(Dictionary<(int Row, int Column), string> cells, Dictionary<(int Row, int Column), string> links)
    {
        foreach (var (cell, url) in links)
        {
            if (!cells.TryGetValue(cell, out var text) || text.Length == 0)
            {
                cells[cell] = url;
                continue;
            }

            if (!text.Contains(url, StringComparison.Ordinal))
                cells[cell] = text + "\n" + url;
        }
    }

    /// <summary>
    /// Перечисляет ячейки адреса <c>H3</c> или диапазона <c>G3:H3</c>
    /// </summary>
    /// <param name="reference">
    /// Адрес гиперссылки
    /// </param>
    /// <returns>
    /// Координаты с нуля. Пустая последовательность, если адрес не разбирается
    /// </returns>
    private static IEnumerable<(int Row, int Column)> CellsInReference(string reference)
    {
        var bounds = reference.Split(':');
        if (!TryReference(bounds[0], out var startRow, out var startColumn))
            yield break;

        var endRow = startRow;
        var endColumn = startColumn;
        if (bounds.Length == 2 && !TryReference(bounds[1], out endRow, out endColumn))
            yield break;

        for (var row = startRow; row <= endRow; row++)
        {
            for (var column = startColumn; column <= endColumn; column++)
                yield return (row, column);
        }
    }

    #endregion Hyperlinks

    /// <summary>
    /// Возвращает видимый текст ячейки
    /// </summary>
    /// <param name="cell">
    /// Элемент ячейки
    /// </param>
    /// <param name="sharedStrings">
    /// Общая таблица строк
    /// </param>
    /// <returns>
    /// Текст ячейки. Число без типа строки считается датой Excel
    /// </returns>
    private static string CellText(XElement cell, List<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr")
            return string.Concat(cell.Descendants(Spreadsheet + "t").Select(text => text.Value));

        var raw = cell.Element(Spreadsheet + "v")?.Value;
        if (string.IsNullOrEmpty(raw))
            return "";

        if (type == "s"
            && int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            && index >= 0
            && index < sharedStrings.Count)
        {
            return sharedStrings[index];
        }

        if (type is null
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return DateTime.FromOADate(number).ToString("dd.MM", CultureInfo.InvariantCulture);
        }

        return raw;
    }

    /// <summary>
    /// Разбирает адрес ячейки вроде <c>H3</c>
    /// </summary>
    /// <param name="reference">
    /// Адрес или левая верхняя граница диапазона
    /// </param>
    /// <param name="row">
    /// Номер строки с нуля
    /// </param>
    /// <param name="column">
    /// Номер колонки с нуля
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если адрес состоит из букв колонки и номера строки
    /// </returns>
    private static bool TryReference(string reference, out int row, out int column)
    {
        row = 0;
        column = 0;
        var index = 0;
        while (index < reference.Length && char.IsLetter(reference[index]))
        {
            column = column * 26 + (char.ToUpperInvariant(reference[index]) - 'A' + 1);
            index++;
        }

        if (index == 0 || index == reference.Length
            || !int.TryParse(reference[index..], NumberStyles.None, CultureInfo.InvariantCulture, out var rowNumber)
            || rowNumber < 1)
        {
            return false;
        }

        column--;
        row = rowNumber - 1;
        return column >= 0;
    }

    /// <summary>
    /// Заключает поле в кавычки, если в нём есть запятая, кавычка или перевод строки
    /// </summary>
    /// <param name="value">
    /// Текст поля
    /// </param>
    /// <returns>
    /// Поле CSV
    /// </returns>
    private static string Quote(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
            return value;

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
