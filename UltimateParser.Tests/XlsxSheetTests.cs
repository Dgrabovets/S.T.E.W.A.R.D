using System.IO.Compression;
using System.Text;
using UltimateParser.Common.Models;
using UltimateParser.Scheduling.Implementations;

namespace UltimateParser.Tests;

/// <summary>
/// Проверяет, что объединённая ячейка доходит до колонки группы
/// </summary>
public class XlsxSheetTests
{
    /// <summary>
    /// Значение из левой верхней ячейки объединения попадает в соседнюю колонку группы, а дата Excel становится <c>DD.MM</c>
    /// </summary>
    [Fact]
    public void ToCsv_copies_a_merged_lesson_into_the_group_column()
    {
        var csv = XlsxSheet.ToCsv(Workbook());
        var slots = ScheduleParser.Parse(csv, "РКИ-2-26", SyncOptions.ResolveTimeZone("Europe/Moscow"), new DateOnly(2026, 10, 8));
        var lesson = Assert.Single(slots, slot => slot.Lesson is not null).Lesson;

        Assert.NotNull(lesson);
        Assert.Equal("Основы программирования на C# (Лекция)", lesson.Title);
        Assert.Equal("up:rki-2-26:2026-10-01:1850", lesson.ExternalId);
    }

    /// <summary>
    /// Собирает книгу, где занятие записано в <c>РКИ-1-26</c> и объединено с <c>РКИ-2-26</c>
    /// </summary>
    /// <returns>
    /// Байты xlsx
    /// </returns>
    private static byte[] Workbook()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "xl/sharedStrings.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <si><t>Дата</t></si>
                  <si><t>время</t></si>
                  <si><t>РКИ-1-26</t></si>
                  <si><t>РКИ-2-26</t></si>
                  <si><t>18.50-20.10</t></si>
                  <si><t>Основы программирования на C# (Лекция), Богданов Георгий Владимирович</t></si>
                </sst>
                """);
            Write(archive, "xl/worksheets/sheet1.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
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
                </worksheet>
                """);
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Кладёт текст в запись архива
    /// </summary>
    /// <param name="archive">
    /// Создаваемая книга
    /// </param>
    /// <param name="path">
    /// Путь записи внутри архива
    /// </param>
    /// <param name="content">
    /// Текст XML
    /// </param>
    private static void Write(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}
