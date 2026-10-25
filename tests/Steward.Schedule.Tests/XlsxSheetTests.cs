using Steward.Schedule.Common.Helpers;
using Steward.Schedule.Common.Models;
using Steward.Schedule.Scheduling.Implementations;
using System.IO.Compression;
using System.Text;

namespace Steward.Schedule.Tests;

/// <summary>
/// Проверяет, что объединённая ячейка доходит до колонки группы
/// </summary>
public class XlsxSheetTests
{
    /// <summary>
    /// Общие строки книги: названия колонок, время и занятие
    /// </summary>
    private const string SharedStrings = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <si><t>Дата</t></si>
          <si><t>время</t></si>
          <si><t>РКИ-1-26</t></si>
          <si><t>РКИ-2-26</t></si>
          <si><t>18.50-20.10</t></si>
          <si><t>Основы программирования на C# (Лекция), Богданов Георгий Владимирович</t></si>
        </sst>
        """;

    /// <summary>
    /// Значение из левой верхней ячейки объединения попадает в соседнюю колонку группы, а дата Excel становится <c>DD.MM</c>
    /// </summary>
    [Fact]
    public void ToCsv_copies_a_merged_lesson_into_the_group_column()
    {
        var lesson = SingleLesson(Workbook(withLink: false));

        Assert.NotNull(lesson);
        Assert.Equal("Основы программирования на C# (Лекция)", lesson.Title);
        Assert.Equal("up:rki-2-26:2026-10-01:1850", lesson.ExternalId);
    }

    /// <summary>
    /// Гиперссылка левой верхней ячейки объединения попадает в заметку колонки группы
    /// </summary>
    [Fact]
    public void ToCsv_appends_a_merged_cell_hyperlink_to_the_group_note()
    {
        var lesson = SingleLesson(Workbook(withLink: true));

        Assert.NotNull(lesson);
        Assert.Contains("https://telemost.360.yandex.ru/j/1922819985", lesson.Note, StringComparison.Ordinal);
        Assert.Equal("Основы программирования на C# (Лекция)", lesson.Title);
    }

    /// <summary>
    /// Переводит книгу в CSV, разбирает его и берёт единственное занятие
    /// </summary>
    /// <param name="workbook">Байты xlsx</param>
    /// <returns>Занятие колонки <c>РКИ-2-26</c></returns>
    private static Lesson? SingleLesson(byte[] workbook)
    {
        var slots = ScheduleParser.Parse(XlsxSheetParser.ToCsv(workbook), "РКИ-2-26", TimeHelper.ResolveTimeZone("Europe/Moscow"), new DateOnly(2026, 10, 8));

        return Assert.Single(slots, slot => slot.Lesson is not null).Lesson;
    }

    /// <summary>
    /// Собирает книгу, где занятие записано в <c>РКИ-1-26</c> и объединено с <c>РКИ-2-26</c>
    /// </summary>
    /// <param name="withLink">Есть ли у объединённой ячейки внешняя гиперссылка</param>
    /// <returns>Байты xlsx</returns>
    private static byte[] Workbook(bool withLink)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "xl/sharedStrings.xml", SharedStrings);

            Write(archive, "xl/worksheets/sheet1.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheetData>
                    <row r="1">
                      <c r="A1" t="s"><v>0</v></c>
                      <c r="B1" t="s"><v>1</v></c>
                      <c r="C1" t="s"><v>2</v></c>
                      <c r="D1" t="s"><v>3</v></c>
                    </row>
                    <row r="2">
                      <c r="A2"><v>46296</v></c>
                      <c r="B2" t="s"><v>4</v></c>
                      <c r="C2" t="s"><v>5</v></c>
                    </row>
                  </sheetData>
                  <mergeCells>
                    <mergeCell ref="C2:D2"/>
                  </mergeCells>
                  {(withLink ? """<hyperlinks><hyperlink ref="C2" r:id="rId1"/></hyperlinks>""" : string.Empty)}
                </worksheet>
                """);

            if (withLink)
            {
                Write(archive, "xl/worksheets/_rels/sheet1.xml.rels", """
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink" Target="https://telemost.360.yandex.ru/j/1922819985" TargetMode="External"/>
                    </Relationships>
                    """);
            }
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Кладёт текст в запись архива
    /// </summary>
    /// <param name="archive">Создаваемая книга</param>
    /// <param name="path">Путь записи внутри архива</param>
    /// <param name="content">Текст XML</param>
    private static void Write(ZipArchive archive, string path, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}