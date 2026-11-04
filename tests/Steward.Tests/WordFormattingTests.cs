using System.IO.Compression;
using System.Text.RegularExpressions;
using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Homework.Storage;

namespace Steward.Tests;

/// <summary>
/// Оформление Word по образцам владельца: измерение шрифта, интервала и полей, обычное оформление учебных работ, таблицы и подписи, текст просьбы агенту
/// </summary>
public sealed class WordFormattingTests : IDisposable
{
    /// <summary>
    /// Папка теста
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "steward-word-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>
    /// Создаёт папку теста
    /// </summary>
    public WordFormattingTests() => Directory.CreateDirectory(_root);

    /// <summary>
    /// Удаляет папку теста
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Записывает минимальный .docx со стилем по умолчанию и полями, какие бывают в учебных работах
    /// </summary>
    /// <param name="name">Имя файла</param>
    /// <param name="font">Шрифт</param>
    /// <param name="halfPoints">Размер в половинах пункта</param>
    /// <param name="left">Левое поле в twips</param>
    /// <returns>Путь к файлу</returns>
    private string Sample(string name, string font, int halfPoints, int left)
    {
        var path = Path.Combine(_root, name);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        void Part(string entry, string xml)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entry).Open(), new System.Text.UTF8Encoding(false));
            writer.Write(xml);
        }

        const string W = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
        Part("word/styles.xml", $"<w:styles {W}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"{font}\" w:hAnsi=\"{font}\"/><w:sz w:val=\"{halfPoints}\"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:line=\"360\" w:lineRule=\"auto\"/><w:ind w:firstLine=\"709\"/><w:jc w:val=\"both\"/></w:pPr></w:pPrDefault></w:docDefaults></w:styles>");
        Part("word/document.xml", $"<w:document {W}><w:body><w:p/><w:sectPr><w:pgMar w:top=\"1134\" w:right=\"851\" w:bottom=\"1134\" w:left=\"{left}\"/></w:sectPr></w:body></w:document>");

        return path;
    }

    /// <summary>
    /// Измерение читает шрифт, размер, интервал, красную строку, выравнивание и поля, берёт самое частое значение по образцам и пропускает файлы, которые не читаются
    /// </summary>
    [Fact]
    public void The_style_is_measured_from_the_samples_by_the_most_common_values()
    {
        var first = Sample("a.docx", "Times New Roman", 28, 1701);
        var second = Sample("b.docx", "Times New Roman", 28, 1701);
        var odd = Sample("c.docx", "Arial", 24, 1984);
        var broken = Path.Combine(_root, "d.docx");
        File.WriteAllText(broken, "не архив");

        var style = WordStyle.Measure([first, second, odd, broken, Path.Combine(_root, "нет.docx"), Path.Combine(_root, "e.md")])!;

        Assert.Equal("Times New Roman", style.Font);
        Assert.Equal(14, style.SizePt);
        Assert.Equal(1.5, style.Spacing);
        Assert.Equal(12.5, style.IndentMm);
        Assert.True(style.Justify);
        Assert.Equal((30, 20, 15, 20), style.Margins);
        Assert.Null(WordStyle.Measure([broken]));
        Assert.Contains("шрифт Times New Roman", style.Describe(), StringComparison.Ordinal);
        Assert.Contains("поля 30, 20, 15, 20 мм", style.Describe(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Оформление из текста работы сильнее измеренного, измеренное сильнее обычного, а без всего берётся обычное оформление учебных работ
    /// </summary>
    [Fact]
    public void The_text_wins_over_the_measured_style_and_the_measured_over_the_default()
    {
        var measured = new WordStyle("Georgia", 13, 1.15, (25, 25, 10, 25), false, 10, null);
        var fromText = new WordStyle("Arial", null, null, null, null, null);

        var merged = fromText.Over(measured).Over(WordStyle.Academic);

        Assert.Equal("Arial", merged.Font);
        Assert.Equal(13, merged.SizePt);
        Assert.False(merged.Justify);
        Assert.Equal("center", merged.PageNumbers);
        Assert.Equal(WordStyle.Academic, WordStyle.None.Over(WordStyle.Academic));
    }

    /// <summary>
    /// Таблица из строк со столбиками превращается в таблицу Word с шапкой, а подписи «Таблица» и «Рисунок» и заголовки получают оформление из указаний
    /// </summary>
    [Fact]
    public void Tables_captions_and_headings_follow_the_guides()
    {
        var path = Path.Combine(_root, "work.docx");

        OfficeWriter.WriteWord(path, "# Введение\n\nТекст работы.\n\nТаблица 1.1 — Сравнение\n\n| Вариант | Цена |\n|---|---|\n| Первый | 10 |\n| Второй | 20 |\n\nРисунок 1.1 — Схема\n\n## 1.1 Постановка задачи\n\nЕщё текст.");

        using var archive = ZipFile.OpenRead(path);
        using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open());
        var xml = reader.ReadToEnd();

        Assert.Single(Regex.Matches(xml, "<w:tbl>"));
        Assert.Equal(3, Regex.Matches(xml, "<w:tr>|<w:tr ").Count);
        Assert.Contains("Times New Roman", xml, StringComparison.Ordinal);
        Assert.Contains("<w:sz w:val=\"28\"", xml, StringComparison.Ordinal);
        Assert.Contains("<w:pgMar", xml, StringComparison.Ordinal);
        Assert.Contains("w:val=\"Title\"", xml, StringComparison.Ordinal);
        Assert.Contains("w:val=\"Heading1\"", xml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Просьба агенту для Word называет измеренное оформление образцов и правила элементов работы, а без образцов называет только правила
    /// </summary>
    [Fact]
    public void The_prompt_names_the_measured_style_and_the_element_rules()
    {
        var lesson = new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19));
        var item = new HomeworkItem("Отчёт", null, string.Empty, string.Empty);
        var measured = new GuideMaterials(["g.md"], ["e.md"], [], new WordStyle("Times New Roman", 14, 1.5, (30, 20, 15, 20), true, 12.5, null));

        var withStyle = DraftBuilder.Prompt(lesson, item, 1, HomeworkFormatEnum.Word, "t.txt", "r.md", measured);
        var without = DraftBuilder.Prompt(lesson, item, 1, HomeworkFormatEnum.Word, "t.txt", "r.md", new GuideMaterials(["g.md"], [], []));

        Assert.Contains("Оформление образцов владельца, измеренное по их файлам Word: шрифт Times New Roman", withStyle, StringComparison.Ordinal);
        Assert.Contains("«Таблица N — Название»", withStyle, StringComparison.Ordinal);
        Assert.Contains("«Рисунок N — Название»", withStyle, StringComparison.Ordinal);
        Assert.DoesNotContain("измеренное", without, StringComparison.Ordinal);
        Assert.Contains("«Таблица N — Название»", without, StringComparison.Ordinal);
    }
}
