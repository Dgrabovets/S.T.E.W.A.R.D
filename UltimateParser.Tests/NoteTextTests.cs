using UltimateParser.Common;

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

        Assert.Equal("Предмет, преподаватель", NoteText.Plain(delta));
        Assert.Equal("обычный текст", NoteText.Plain("обычный текст"));
    }
}
