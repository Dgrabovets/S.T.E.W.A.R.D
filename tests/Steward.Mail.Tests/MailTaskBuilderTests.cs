using Steward.Mail.Common;
using Steward.Mail.Models;
using Steward.Mail.Services;

namespace Steward.Mail.Tests;

/// <summary>
/// Проверяет, какие задачи получаются из писем
/// </summary>
public class MailTaskBuilderTests
{
    /// <summary>
    /// День, с которого начинаются задачи
    /// </summary>
    private static readonly DateOnly Today = new(2026, 10, 25);

    /// <summary>
    /// Настройки с проектом назначения
    /// </summary>
    private static readonly AppSettings Settings = new() { Singularity = new SingularitySettings { ProjectId = "P-1" } };

    /// <summary>
    /// Отзыв называется по имени преподавателя и заданию из темы, приоритет обычный
    /// </summary>
    [Fact]
    public void Feedback_takes_the_teacher_and_the_assignment_from_the_subject()
    {
        var mail = Samples.Mail("1", subject: "Пользователь Петров Пётр Петрович дал отзыв на Ваш ответ на задание «Задание 2».");

        var task = MailTaskBuilder.Build(mail, MailCategoryEnum.TeacherFeedback, Settings, Today)!;

        Assert.Equal("Отзыв преподавателя (Петров П. П.): «Задание 2» — посмотреть", task.Title);
        Assert.Equal(1, task.Priority);
        Assert.Equal("2026-10-25", task.Start);
        Assert.Equal("P-1", task.ProjectId);
    }

    /// <summary>
    /// Если тема не похожа на отзыв, в названии остаётся сама тема
    /// </summary>
    [Fact]
    public void Feedback_with_an_unknown_subject_keeps_the_subject()
    {
        var task = MailTaskBuilder.Build(Samples.Mail("1", subject: "Оценка выставлена"), MailCategoryEnum.TeacherFeedback, Settings, Today)!;

        Assert.Equal("Отзыв преподавателя — посмотреть: Оценка выставлена", task.Title);
    }

    /// <summary>
    /// Личное письмо даёт задачу с высоким приоритетом, длинная тема обрезается
    /// </summary>
    [Fact]
    public void A_personal_reply_gets_a_high_priority_and_a_truncated_subject()
    {
        var mail = Samples.Mail("1", subject: new string('я', 120));

        var task = MailTaskBuilder.Build(mail, MailCategoryEnum.PersonalReply, Settings, Today)!;

        Assert.StartsWith("Пришёл ответ от Иванов И. И., нужно ответить: «", task.Title, StringComparison.Ordinal);
        Assert.EndsWith("…»", task.Title, StringComparison.Ordinal);
        Assert.Equal(0, task.Priority);
    }

    /// <summary>
    /// Справочное письмо задачи не создаёт
    /// </summary>
    [Fact]
    public void Info_creates_no_task() =>
        Assert.Null(MailTaskBuilder.Build(Samples.Mail("1"), MailCategoryEnum.Info, Settings, Today));

    /// <summary>
    /// В заметке есть отправитель, тема, начало письма и ссылка на почту
    /// </summary>
    [Fact]
    public void The_note_carries_the_sender_the_subject_the_snippet_and_the_web_mail_link()
    {
        var task = MailTaskBuilder.Build(Samples.Mail("1", subject: "Вопрос про зачёт"), MailCategoryEnum.PersonalReply, Settings, Today)!;

        Assert.Contains("От: Иванов Иван Иванович <ivanov@example.com>", task.Note, StringComparison.Ordinal);
        Assert.Contains("Тема: Вопрос про зачёт", task.Note, StringComparison.Ordinal);
        Assert.Contains("Начало письма", task.Note, StringComparison.Ordinal);
        Assert.Contains("https://mail.360.yandex.ru/", task.Note, StringComparison.Ordinal);
    }
}