using UltimateParser.Common.Helpers;

namespace UltimateParser.Tests;

/// <summary>
/// Проверяет текст заметки, извлечённый из обычной строки и из дельты Quill
/// </summary>
public class NoteTextTests
{
    /// <summary>
    /// Текст вставки дельты совпадает с обычной заметкой, без завершающего перевода строки Quill
    /// </summary>
    [Fact]
    public void Plain_reads_quill_delta_notes()
    {
        const string delta = """{"ops":[{"insert":"Предмет, преподаватель\n"}]}""";

        Assert.Equal("Предмет, преподаватель", NoteTextHelper.GetPlainText(delta));
        Assert.Equal("обычный текст", NoteTextHelper.GetPlainText("обычный текст"));
    }

    /// <summary>
    /// Последняя строка с адресом становится массивом операций Quill со словом «Ссылка»
    /// </summary>
    [Fact]
    public void Format_turns_the_last_line_into_a_quill_operation_array()
    {
        const string note = "Предмет, Иванов\nhttps://telemost.360.yandex.ru/j/1";

        var formatted = NoteTextHelper.Format(note);

        Assert.StartsWith("[", formatted, StringComparison.Ordinal);
        Assert.Contains("\"insert\":\"Ссылка\"", formatted, StringComparison.Ordinal);
        Assert.Contains("\"link\":\"https://telemost.360.yandex.ru/j/1\"", formatted, StringComparison.Ordinal);
        Assert.True(NoteTextHelper.Same(formatted, note));
        Assert.False(NoteTextHelper.Same(note, note));
        Assert.False(NoteTextHelper.Same("""{"ops":[{"insert":"Ссылка","attributes":{"link":"https://telemost.360.yandex.ru/j/1"}},{"insert":"\n"}]}""", note));
    }
}
