using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Steward.Mail.Common;
using Steward.Mail.Models;

namespace Steward.Mail.Services;

/// <summary>
/// Собирает задачу SingularityApp из письма
/// </summary>
public static partial class MailTaskBuilder
{
    /// <summary>
    /// Собирает задачу для SingularityApp
    /// Для информационных писем задача не создаётся
    /// </summary>
    /// <param name="mail">Письмо из почтового ящика</param>
    /// <param name="category">Категория письма</param>
    /// <param name="settings">Настройки: из них берутся проект назначения и адрес веб-почты</param>
    /// <param name="today">День, с которого задача начинается</param>
    /// <returns>
    /// Задача с приоритетом по категории или <see langword="null"/> для информационного письма
    /// </returns>
    public static SingularityTaskRequest? Build(MailItem mail, MailCategoryEnum category, AppSettings settings, DateOnly today)
    {
        var note = new StringBuilder()
            .AppendLine($"От: {mail.FromName} <{mail.FromAddress}>")
            .AppendLine($"Тема: {mail.Subject}")
            .AppendLine($"Получено: {mail.Date.LocalDateTime:dd.MM.yyyy HH:mm}")
            .AppendLine()
            .AppendLine(MailText.Truncate(mail.Snippet, 500))
            .AppendLine()
            .AppendLine(settings.Mail.WebMailUrl)
            .ToString();

        var start = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return category switch
        {
            MailCategoryEnum.TeacherFeedback => new SingularityTaskRequest
            {
                Title = FeedbackTitle(mail.Subject),
                Note = note,
                Priority = 1,
                Start = start,
                ProjectId = settings.Singularity.ProjectId
            },
            MailCategoryEnum.PersonalReply => new SingularityTaskRequest
            {
                Title = $"Пришёл ответ от {MailText.ShortName(mail.FromName)}, нужно ответить: «{MailText.Truncate(mail.Subject, 80)}»",
                Note = note,
                Priority = 0,
                Start = start,
                ProjectId = settings.Singularity.ProjectId
            },
            _ => null
        };
    }

    /// <summary>
    /// «Пользователь Иванов Иван Иванович дал отзыв на Ваш ответ на задание «Задание 2».»
    /// превращается в «Отзыв преподавателя (Иванов И. И.): «Задание 2» — посмотреть»
    /// </summary>
    /// <param name="subject">Тема письма об отзыве</param>
    /// <returns>Название задачи. Если тема не похожа на отзыв, в нём остаётся сама тема</returns>
    private static string FeedbackTitle(string subject)
    {
        var match = FeedbackRegex().Match(subject);
        if (!match.Success)
            return $"Отзыв преподавателя — посмотреть: {MailText.Truncate(subject, 80)}";

        return $"Отзыв преподавателя ({MailText.ShortName(match.Groups["name"].Value)}): «{match.Groups["task"].Value}» — посмотреть";
    }

    /// <summary>
    /// Разбирает тему письма об отзыве: имя преподавателя и название задания
    /// </summary>
    /// <returns>
    /// Регулярное выражение с группами <c>name</c> и <c>task</c>
    /// </returns>
    [GeneratedRegex("Пользователь\\s+(?<name>.+?)\\s+дал\\s+отзыв.*?«(?<task>[^»]+)»")]
    private static partial Regex FeedbackRegex();
}