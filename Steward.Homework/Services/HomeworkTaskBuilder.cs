using System.Globalization;
using Steward.Homework.Models;
using Steward.Mail.Models;
using Steward.Schedule.Common.Helpers;

namespace Steward.Homework.Services;

/// <summary>
/// Собирает задачу SingularityApp по заданию из занятия
/// </summary>
public static class HomeworkTaskBuilder
{
    /// <summary>
    /// Начало внешнего ключа задачи ДЗ
    /// </summary>
    private const string EXTERNAL_PREFIX = "hw:";

    /// <summary>
    /// Внешний ключ: по нему повторная проверка узнаёт, что задача уже есть
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <returns>Строка вида <c>hw:&lt;занятие&gt;:&lt;номер&gt;</c></returns>
    public static string ExternalId(Lesson lesson, int number) => $"{EXTERNAL_PREFIX}{lesson.Id}:{number}";

    /// <summary>
    /// Собирает задачу. Начало задачи — день срока, а без срока день занятия
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="item">Задание</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <param name="format">Формат, в котором готовить работу</param>
    /// <param name="projectId">Проект SingularityApp или <see langword="null"/></param>
    /// <param name="offset">Часовой пояс начала дня</param>
    /// <param name="drafts">Пути к готовым файлам по этому заданию. Singularity не принимает вложения, поэтому в заметку идут ссылки на файлы на диске</param>
    /// <returns>Запрос на создание задачи</returns>
    public static SingularityTaskRequest Build(Lesson lesson, HomeworkItem item, int number, HomeworkFormatEnum format, string? projectId, TimeSpan offset, IReadOnlyList<string>? drafts = null)
    {
        var day = item.Due ?? lesson.Date;
        var note = new QuillDelta().Text($"Занятие: {lesson.Title}, {lesson.Date:dd.MM.yyyy}\nФормат: {HomeworkFormats.Title(format)}\n");
        if (item.Due is { } due)
            note.Text($"Срок: {due:dd.MM.yyyy}\n");

        if (item.Requirements.Length > 0)
            note.Text($"\n{item.Requirements}\n");

        if (item.Link.Length > 0)
            note.Divider().Link("Задание", item.Link);

        if (drafts is { Count: > 0 })
        {
            note.Text("\nГотовая работа:\n");
            foreach (var draft in drafts)
                note.Link(Path.GetFileName(draft), new Uri(draft).AbsoluteUri).Text("\n");
        }

        return new SingularityTaskRequest
        {
            Title = $"ДЗ: {item.Title}",
            Note = note.ToString(),
            Priority = 1,
            Start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), offset).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            ExternalId = ExternalId(lesson, number),
            ProjectId = projectId,
        };
    }
}