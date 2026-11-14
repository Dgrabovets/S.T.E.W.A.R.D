using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Homework.Services;

/// <summary>
/// Срок задачи по домашнему заданию: задание сдают на следующем занятии предмета, поэтому крайний срок стоит на день занятия, а задача встаёт в плане за день до него
/// </summary>
public static class HomeworkDeadline
{
    /// <summary>
    /// Начало внешних ключей занятий расписания в SingularityApp
    /// </summary>
    private const string SCHEDULE_KEY_PREFIX = "up:";

    /// <summary>
    /// За сколько дней до сдачи задача встаёт в план
    /// </summary>
    private const int START_DAYS_BEFORE = 1;

    /// <summary>
    /// Считает день начала и срок задачи. Без дня сдачи задача встаёт в день занятия и срока не имеет
    /// </summary>
    /// <param name="lessonDate">День занятия, на котором задали работу</param>
    /// <param name="due">Срок, названный преподавателем. <see langword="null"/>, если не названа дата</param>
    /// <param name="nextLesson">Следующее занятие предмета по расписанию. <see langword="null"/>, если расписания нет</param>
    /// <param name="today">Сегодняшний день: раньше него и раньше занятия задача не встаёт</param>
    /// <returns>День начала и срок. Срок <see langword="null"/>, если день сдачи неизвестен</returns>
    public static (DateOnly Start, DateOnly? Deadline) Window(DateOnly lessonDate, DateOnly? due, DateOnly? nextLesson, DateOnly today)
    {
        if ((due ?? nextLesson) is not { } handIn || handIn <= lessonDate)
            return (due ?? lessonDate, null);

        var earliest = lessonDate > today ? lessonDate : today;
        var start = handIn.AddDays(-START_DAYS_BEFORE);
        if (start < earliest)
            start = earliest;

        // Крайний срок это сам день сдачи; он не бывает раньше начала
        return (start, handIn < start ? start : handIn);
    }

    /// <summary>
    /// Находит в расписании SingularityApp ближайшее занятие предмета после заданного дня
    /// </summary>
    /// <param name="notes">Чтение задач SingularityApp</param>
    /// <param name="subject">Название предмета</param>
    /// <param name="after">День, после которого ищется занятие</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>День занятия или <see langword="null"/>, если занятий не нашлось или SingularityApp не ответил</returns>
    public static async Task<DateOnly?> NextLessonAsync(ISingularityNotes notes, string subject, DateOnly after, CancellationToken cancellationToken)
    {
        try
        {
            // Занятие, которое уже прошло, сдачей не станет: у старого задания берётся ближайшее занятие от сегодняшнего дня
            var yesterday = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
            var effective = after > yesterday ? after : yesterday;
            var from = new DateTimeOffset(effective.ToDateTime(TimeOnly.MinValue), TimeZoneInfo.Local.GetUtcOffset(DateTime.Now));

            return NextLesson(subject, effective, await notes.ListTasksAsync(from, SCHEDULE_KEY_PREFIX, cancellationToken));
        }
        catch (HttpRequestException)
        {
            // Расписание не прочиталось: задача встанет по старому правилу, а не пропадёт
            return null;
        }
    }

    /// <summary>
    /// Выбирает из занятий расписания ближайшее занятие предмета после заданного дня
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="after">День, после которого ищется занятие</param>
    /// <param name="schedule">Задачи расписания с названием и началом</param>
    /// <returns>День занятия или <see langword="null"/>, если подходящего нет</returns>
    public static DateOnly? NextLesson(string subject, DateOnly after, IEnumerable<SingularityTaskInfo> schedule) => schedule
        .Where(task => !task.Removed && task.Start is not null && SameSubject(task.Title, subject))
        .Select(task => DateOnly.FromDateTime(task.Start!.Value.ToLocalTime().DateTime))
        .Where(day => day > after)
        .Order()
        .Select(day => (DateOnly?)day)
        .FirstOrDefault();

    /// <summary>
    /// Проверяет, что занятие расписания относится к предмету: тип занятия в скобках («Гейм-дизайн (Практика)») не мешает
    /// </summary>
    /// <param name="scheduleTitle">Название занятия в расписании</param>
    /// <param name="subject">Название предмета</param>
    /// <returns>
    /// <see langword="true"/>, если это один предмет
    /// </returns>
    private static bool SameSubject(string scheduleTitle, string subject)
    {
        var open = scheduleTitle.LastIndexOf(" (", StringComparison.Ordinal);
        var name = (open > 0 && scheduleTitle.EndsWith(')') ? scheduleTitle[..open] : scheduleTitle).Trim();

        return subject.Length > 0 && name.Equals(subject.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
