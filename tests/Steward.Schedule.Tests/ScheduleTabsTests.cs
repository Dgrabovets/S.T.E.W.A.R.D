using Steward.Schedule.Scheduling.Implementations;
using System.IO.Compression;
using System.Text;

namespace Steward.Schedule.Tests;

/// <summary>
/// Выбор вкладок месяцев в таблице расписания: название вкладки, окно из прошлого, текущего и следующего месяцев и чтение всех листов книги
/// </summary>
public class ScheduleTabsTests
{
    /// <summary>
    /// Месяц определяется по названию вкладки в любом регистре и с пометками, а слова не про месяц месяцем не считаются
    /// </summary>
    [Theory]
    [InlineData("СЕНТЯБРЬ", 9)]
    [InlineData("Октябрь", 10)]
    [InlineData("ноябрь", 11)]
    [InlineData("НОЯБРЬ 2026", 11)]
    [InlineData("  Декабрь (новое) ", 12)]
    [InlineData("Март", 3)]
    [InlineData("Май", 5)]
    [InlineData("Январь", 1)]
    public void The_month_is_taken_from_the_tab_name(string name, int month) => Assert.Equal(month, ScheduleTabs.MonthOf(name));

    /// <summary>
    /// Названия без месяца дают пустой ответ
    /// </summary>
    [Theory]
    [InlineData("Лист1")]
    [InlineData("Мастер-класс")]
    [InlineData("")]
    [InlineData("2026")]
    public void A_name_without_a_month_gives_nothing(string name) => Assert.Null(ScheduleTabs.MonthOf(name));

    /// <summary>
    /// Читаются прошлый, текущий и следующий месяцы по порядку от старого, остальные вкладки и вкладки без месяца пропускаются
    /// </summary>
    [Fact]
    public void Previous_current_and_next_months_are_chosen_in_order()
    {
        var sheets = new[] { ("СЕНТЯБРЬ", "s"), ("ОКТЯБРЬ", "o"), ("НОЯБРЬ", "n"), ("ДЕКАБРЬ", "d"), ("Лист1", "x") };

        Assert.Equal(["o", "n", "d"], ScheduleTabs.Choose(sheets, new DateOnly(2026, 11, 2)).Select(sheet => sheet.Csv).ToArray());
        Assert.Equal(["s", "o", "n"], ScheduleTabs.Choose(sheets, new DateOnly(2026, 10, 15)).Select(sheet => sheet.Csv).ToArray());
        Assert.Equal(["o", "n"], ScheduleTabs.Choose(sheets.Take(3).ToArray(), new DateOnly(2026, 11, 30)).Select(sheet => sheet.Csv).ToArray());
        Assert.Empty(ScheduleTabs.Choose([("Лист1", "x")], new DateOnly(2026, 11, 2)));
    }

    /// <summary>
    /// На стыке годов декабрь остаётся прошлым месяцем января, а январь следующим месяцем декабря
    /// </summary>
    [Fact]
    public void The_window_wraps_around_the_new_year()
    {
        var sheets = new[] { ("ДЕКАБРЬ", "d"), ("ЯНВАРЬ", "j") };

        Assert.Equal(["d", "j"], ScheduleTabs.Choose(sheets, new DateOnly(2027, 1, 12)).Select(sheet => sheet.Csv).ToArray());
        Assert.Equal(["d", "j"], ScheduleTabs.Choose(sheets, new DateOnly(2026, 12, 20)).Select(sheet => sheet.Csv).ToArray());
    }

    /// <summary>
    /// Книга со всеми вкладками читается целиком: у каждого листа своё название и свой CSV, а книга с одним листом без списка листов читается как раньше
    /// </summary>
    [Fact]
    public void Every_sheet_of_a_workbook_is_read_by_its_tab_name()
    {
        var sheets = XlsxSheetParser.ToCsvSheets(Workbook(("ОКТЯБРЬ", "29.10"), ("НОЯБРЬ", "02.11")));

        Assert.Equal(["ОКТЯБРЬ", "НОЯБРЬ"], sheets.Select(sheet => sheet.Name).ToArray());
        Assert.Contains("29.10", sheets[0].Csv, StringComparison.Ordinal);
        Assert.DoesNotContain("02.11", sheets[0].Csv, StringComparison.Ordinal);
        Assert.Contains("02.11", sheets[1].Csv, StringComparison.Ordinal);

        var legacy = XlsxSheetParser.ToCsvSheets(Workbook(("", "05.10")));

        Assert.Equal(string.Empty, Assert.Single(legacy).Name);
        Assert.Contains("05.10", legacy[0].Csv, StringComparison.Ordinal);
    }

    /// <summary>
    /// Собирает книгу xlsx с листами, в каждом по одной ячейке с текстом
    /// </summary>
    /// <param name="sheets">Название вкладки и текст ячейки A1. Пустое название: книга без списка листов</param>
    /// <returns>Байты xlsx</returns>
    private static byte[] Workbook(params (string Name, string Text)[] sheets)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Part(string path, string xml)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(xml);
            }

            const string MAIN = "xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"";
            for (var index = 0; index < sheets.Length; index++)
                Part($"xl/worksheets/sheet{index + 1}.xml", $"<worksheet {MAIN}><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>{sheets[index].Text}</t></is></c></row></sheetData></worksheet>");

            if (sheets.All(sheet => sheet.Name.Length > 0))
            {
                Part("xl/workbook.xml", $"<workbook {MAIN} xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>"
                    + string.Concat(sheets.Select((sheet, index) => $"<sheet name=\"{sheet.Name}\" sheetId=\"{index + 1}\" r:id=\"rId{index + 1}\"/>"))
                    + "</sheets></workbook>");
                Part("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                    + string.Concat(sheets.Select((_, index) => $"<Relationship Id=\"rId{index + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{index + 1}.xml\"/>"))
                    + "</Relationships>");
            }
        }

        return stream.ToArray();
    }
}
