using System.Globalization;
using Steward.Homework.Models;
using Steward.Lms.Models;
using Steward.Mail.Models;
using Steward.Schedule.Common.Helpers;

namespace Steward.Lms.Services;

/// <summary>
/// Сверяет работы из LMS с задачами в SingularityApp: заводит задачу на ещё не сданную работу и закрывает её, когда работа сдана. В LMS программа только читает
/// </summary>
public static class ActivityTaskSync
{
    /// <summary>
    /// Что нужно сделать в SingularityApp, чтобы задачи совпали с LMS
    /// </summary>
    /// <param name="ToCreate">Работы, на которые ещё нет задачи</param>
    /// <param name="ToClose">Открытые задачи на работы, которые уже сданы</param>
    public sealed record Plan(IReadOnlyList<LmsActivity> ToCreate, IReadOnlyList<(SingularityTaskInfo Task, LmsActivity Activity)> ToClose);

    /// <summary>
    /// Строит план: задачи заводятся только по предметам с отметкой «расшифровывать», а закрываются по любым, если работа сдана
    /// </summary>
    /// <param name="activities">Работы из LMS с состоянием</param>
    /// <param name="tasks">Задачи SingularityApp с ключом <c>lms:</c></param>
    /// <param name="subjectState">Отметка предмета по его названию</param>
    /// <returns>Что создать и что закрыть</returns>
    public static Plan Build(IReadOnlyList<LmsActivity> activities, IReadOnlyList<SingularityTaskInfo> tasks, Func<string, LessonStateEnum> subjectState)
    {
        var byKey = tasks.GroupBy(task => task.ExternalId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var create = activities
            .Where(activity => !activity.Done && subjectState(activity.Subject) is LessonStateEnum.Fetch or LessonStateEnum.LmsOnly && !byKey.ContainsKey(activity.ExternalId))
            .ToArray();
        var close = activities
            .Where(activity => activity.Done && byKey.TryGetValue(activity.ExternalId, out var task) && !task.IsClosed)
            .Select(activity => (byKey[activity.ExternalId], activity))
            .ToArray();

        return new Plan(create, close);
    }

    /// <summary>
    /// Собирает задачу SingularityApp по работе: название, срок, состояние и ссылка на страницу работы
    /// </summary>
    /// <param name="activity">Работа из LMS</param>
    /// <param name="projectId">Проект SingularityApp или <see langword="null"/>, тогда задача идёт во входящие</param>
    /// <param name="today">Сегодняшний день: срок начала для работ без срока</param>
    /// <param name="offset">Часовой пояс для времени начала</param>
    /// <returns>Запрос на создание задачи</returns>
    public static SingularityTaskRequest Build(LmsActivity activity, string? projectId, DateOnly today, TimeSpan offset)
    {
        var day = activity.Due is { } due ? DateOnly.FromDateTime(due) : today;
        var note = new QuillDelta().Text($"Предмет: {activity.Subject}\n");
        if (activity.Section.Length > 0)
            note.Text($"Тема: {activity.Section}\n");

        if (activity.Due is { } deadline)
            note.Text($"Срок: {deadline.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)}\n");

        note.Text($"Состояние в LMS: {activity.Status}\n").Divider().Link("Страница в LMS", activity.Url);

        return new SingularityTaskRequest
        {
            Title = $"{(activity.Kind == ActivityKindEnum.Quiz ? "Тест" : "ДЗ")}: {activity.Name} ({activity.Subject})",
            Note = note.ToString(),
            Priority = 1,
            Start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), offset).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            ExternalId = activity.ExternalId,
            ProjectId = projectId,
        };
    }
}
