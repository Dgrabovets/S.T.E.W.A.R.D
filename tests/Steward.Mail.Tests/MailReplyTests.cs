using Steward.Mail.Services;

namespace Steward.Mail.Tests;

/// <summary>
/// Проверяет, как из письма берётся то, что написал собеседник
/// </summary>
public class MailReplyTests
{
    /// <summary>
    /// Короткое письмо без цитат остаётся целиком
    /// </summary>
    [Fact]
    public void A_plain_message_is_kept_whole() =>
        Assert.Equal("Добрый день!\nПришлите, пожалуйста, ответ до пятницы.", MailReply.Extract("Добрый день!\nПришлите, пожалуйста, ответ до пятницы.\n"));

    /// <summary>
    /// Текст обрывается там, где начинается цитата или пересланное письмо
    /// </summary>
    [Theory]
    [InlineData("> старый текст")]
    [InlineData("25.10.2026, 11:19, \"Данила\" <student@example.com>:")]
    [InlineData("Данила Грабовец написал(а):")]
    [InlineData("On Sun, Oct 25, 2026 at 11:19 AM Danila wrote:")]
    [InlineData("-----Original Message-----")]
    [InlineData("-------- Пересылаемое сообщение --------")]
    [InlineData("--")]
    [InlineData("От: student@example.com")]
    public void The_reply_stops_before_the_quoted_history(string quoteStart)
    {
        var body = $"Принято, жду исправления.\n\n{quoteStart}\nстарый текст студента";

        Assert.Equal("Принято, жду исправления.", MailReply.Extract(body));
    }

    /// <summary>
    /// Строка «Тема:» в самом начале письма цитатой не считается, пока своего текста ещё нет
    /// </summary>
    [Fact]
    public void A_header_like_first_line_is_kept_when_nothing_precedes_it() =>
        Assert.StartsWith("От: меня", MailReply.Extract("От: меня лично\nПродолжение"), StringComparison.Ordinal);

    /// <summary>
    /// Лишние пустые строки схлопываются, длинный текст обрезается по слову с многоточием
    /// </summary>
    [Fact]
    public void Blank_runs_collapse_and_a_long_text_is_cut_at_a_word()
    {
        Assert.Equal("Первый абзац\n\nВторой абзац", MailReply.Extract("Первый абзац\n\n\n\n\nВторой абзац"));

        var cut = MailReply.Extract(string.Join(' ', Enumerable.Repeat("слово", 600)));

        Assert.EndsWith("слово…", cut, StringComparison.Ordinal);
        Assert.True(cut.Length <= 1501);
    }

    /// <summary>
    /// Письмо из одной цитаты даёт пустой текст
    /// </summary>
    [Fact]
    public void A_message_that_is_only_a_quote_gives_nothing() =>
        Assert.Equal(string.Empty, MailReply.Extract("> только цитата\n> ещё строка"));
}