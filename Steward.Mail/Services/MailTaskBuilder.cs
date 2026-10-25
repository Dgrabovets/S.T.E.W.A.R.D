using System.Globalization;
using System.Text.RegularExpressions;
using Steward.Mail.Common;
using Steward.Mail.Models;
using Steward.Schedule.Common.Helpers;

namespace Steward.Mail.Services;

/// <summary>
/// Собирает задачу SingularityApp из письма
/// </summary>
public static partial class MailTaskBuilder
{
    /// <summary>
    /// Начало внешнего ключа задач из писем: отличает их от задач расписания и от созданных вручную
    /// </summary>
    private const string EXTERNAL_PREFIX = "mail:";

    /// <summary>
    /// Внешний ключ задачи для письма
    /// </summary>
    /// <param name="mail">Письмо</param>
    /// <returns>Ключ вида <c>mail:&lt;Message-ID&gt;</c>: одно письмо, одна задача</returns>
    public static string ExternalId(MailItem mail) => EXTERNAL_PREFIX + mail.MessageId;

    /// <summary>
    /// Собирает задачу для SingularityApp
    /// Для информационных писем задача не создаётся
    /// </summary>
    /// <param name="mail">Письмо из почтового ящика</param>
    /// <param name="category">Категория письма</param>
    /// <param name="settings">Настройки: из них берутся проект назначения и адрес веб-почты</param>
    /// <param name="dayStart">Начало сегодняшнего дня по местному времени со смещением: с него задача начинается</param>
    /// <returns>
    /// Задача с приоритетом по категории или <see langword="null"/> для информационного письма
    /// </returns>
    public static SingularityTaskRequest? Build(MailItem mail, MailCategoryEnum category, AppSettings settings, DateTimeOffset dayStart)
    {
        var note = Note(mail, category, settings);
        var start = dayStart.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

        return category switch
        {
            MailCategoryEnum.TeacherFeedback => new SingularityTaskRequest
            {
                Title = FeedbackTitle(mail.Subject),
                Note = note,
                Priority = 1,
                Start = start,
                ExternalId = ExternalId(mail),
                ProjectId = settings.Singularity.ProjectId
            },
            MailCategoryEnum.PersonalReply => new SingularityTaskRequest
            {
                Title = $"Пришёл ответ от {MailText.ShortName(mail.FromName)}, нужно ответить: «{MailText.Truncate(mail.Subject, 80)}»",
                Note = note,
                Priority = 0,
                Start = start,
                ExternalId = ExternalId(mail),
                ProjectId = settings.Singularity.ProjectId
            },
            _ => null
        };
    }

    /// <summary>
    /// Собирает заметку задачи. Отправитель и тема в ней не повторяются, они уже в названии и в самой задаче
    /// </summary>
    /// <param name="mail">Письмо</param>
    /// <param name="category">Категория письма: от неё зависит, что показывать</param>
    /// <param name="settings">Настройки: шаблон ссылки на письмо</param>
    /// <returns>Дельта Quill: время получения, текст собеседника (кроме отзывов), горизонтальная линия и ссылки с подписями «Задание» и «Письмо»</returns>
    private static string Note(MailItem mail, MailCategoryEnum category, AppSettings settings)
    {
        var note = new QuillDelta().Text($"Получено: {mail.Date.LocalDateTime:dd.MM.yyyy HH:mm}\n");

        var reply = category == MailCategoryEnum.PersonalReply ? MailReply.Extract(mail.Body) : string.Empty;
        if (reply.Length > 0)
            note.Text($"\n{reply}\n");

        note.Divider();

        if (category == MailCategoryEnum.TeacherFeedback && MailLinks.Assignment(mail) is { } assignment)
            note.Link("Задание", assignment);

        return note.Link("Письмо", MailLinks.Letter(mail, settings.Mail)).ToString();
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