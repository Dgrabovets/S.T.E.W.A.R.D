using Steward.Mail.Models;
using Steward.Mail.Services;

namespace Steward.Mail.Tests;

/// <summary>
/// Проверяет текст сводки
/// </summary>
public class MailSummaryTests
{
    /// <summary>
    /// Без писем сводка говорит, что важного нет
    /// </summary>
    [Fact]
    public void An_empty_check_says_there_is_nothing_important() =>
        Assert.Equal("Новых важных писем нет", MailSummary.Build([], [], []));

    /// <summary>
    /// Строки идут по категориям, повторяющиеся отправители называются один раз
    /// </summary>
    [Fact]
    public void Categories_are_listed_and_senders_are_not_repeated()
    {
        IReadOnlyList<MailItem> replies = [Samples.Mail("1"), Samples.Mail("2"), Samples.Mail("3", fromName: "Петров Пётр Петрович")];
        IReadOnlyList<MailItem> feedback = [Samples.Mail("4")];
        IReadOnlyList<MailItem> info = [Samples.Mail("5"), Samples.Mail("6")];

        var summary = MailSummary.Build(feedback, replies, info);

        Assert.Equal("Ответить: 3 (Иванов И. И., Петров П. П.)\nОтзывы и оценки: 1\nПрочее: 2".Replace("\n", Environment.NewLine), summary);
    }
}