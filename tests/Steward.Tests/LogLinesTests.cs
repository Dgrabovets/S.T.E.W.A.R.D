using Steward.Models;

namespace Steward.Tests;

/// <summary>
/// Проверяет разбор вывода программы на плашки журнала
/// </summary>
public class LogLinesTests
{
    /// <summary>
    /// Каждая строка с временем становится отдельной записью, время отделяется от текста
    /// </summary>
    [Fact]
    public void Every_stamped_line_becomes_its_own_record()
    {
        var lines = LogLines.Parse("25.10.2026 18:18:44  Инфо: Яндекс ID  — Вы создали пароль\n25.10.2026 18:18:44  Прочее: 1");

        Assert.Equal(2, lines.Count);
        Assert.Equal("25.10.2026 18:18:44", lines[0].Time);
        Assert.Equal("Инфо: Яндекс ID  — Вы создали пароль", lines[0].Text);
        Assert.Equal("Прочее: 1", lines[1].Text);
    }

    /// <summary>
    /// Строки без времени (стек ошибки) входят в предыдущую запись, пустые строки пропускаются
    /// </summary>
    [Fact]
    public void Lines_without_a_time_continue_the_previous_record()
    {
        var lines = LogLines.Parse("25.10.2026 18:09:34  Ошибка: HttpRequestException: 400\r\nat Steward.Mail.Program.Main()\r\n\r\nat Steward.Mail.Run()\n");

        var line = Assert.Single(lines);
        Assert.Equal("Ошибка: HttpRequestException: 400\nat Steward.Mail.Program.Main()\nat Steward.Mail.Run()", line.Text);
    }

    /// <summary>
    /// Цвет плашки зависит от начала текста: ошибка красная, письма к действию жёлтые, остальное серое
    /// </summary>
    [Theory]
    [InlineData("Ошибка: что-то упало", PanelToneEnum.Problem)]
    [InlineData("Ответить: Иванов — Вопрос", PanelToneEnum.Attention)]
    [InlineData("Отзыв: Петров — Задание", PanelToneEnum.Attention)]
    [InlineData("[тест] Ответить: Иванов — Вопрос", PanelToneEnum.Attention)]
    [InlineData("Инфо: Яндекс ID", PanelToneEnum.Idle)]
    [InlineData("Новых важных писем нет", PanelToneEnum.Idle)]
    public void The_tone_follows_the_start_of_the_text(string text, PanelToneEnum expected) =>
        Assert.Equal(expected, Assert.Single(LogLines.Parse($"25.10.2026 18:00:00  {text}")).Tone);

    /// <summary>
    /// Текст без времени в самом начале остаётся отдельной записью без времени, пустой вывод даёт пустой список
    /// </summary>
    [Fact]
    public void Text_without_any_time_is_kept_and_empty_output_gives_nothing()
    {
        Assert.Equal(string.Empty, Assert.Single(LogLines.Parse("просто строка")).Time);
        Assert.Empty(LogLines.Parse(string.Empty));
    }
}