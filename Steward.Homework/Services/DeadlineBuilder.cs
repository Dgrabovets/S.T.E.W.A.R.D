using Steward.Homework.Models;

namespace Steward.Homework.Services;

/// <summary>
/// Собирает напоминания о сроках: несданные работы из LMS и найденные в расшифровках задания, у которых срок скоро или уже прошёл
/// </summary>
public static class DeadlineBuilder
{
    /// <summary>
    /// Собирает список сроков от ближайших к дальним. Работа из LMS главнее: найденное в расшифровке задание, которое к ней относится, не повторяется
    /// </summary>
    /// <param name="works">Работы из LMS</param>
    /// <param name="items">Найденные задания, которые ещё не сданы и не в архиве, с названием предмета</param>
    /// <param name="today">Сегодняшняя дата</param>
    /// <param name="aheadDays">На сколько дней вперёд показывать сроки</param>
    /// <param name="overdueDays">Сколько дней после срока ещё напоминать</param>
    /// <returns>Сроки по возрастанию даты</returns>
    public static IReadOnlyList<Deadline> Build(IReadOnlyList<ActivityInfo> works, IReadOnlyList<(string Subject, HomeworkItem Item)> items, DateOnly today, int aheadDays, int overdueDays)
    {
        var lms = works
            .Where(work => !work.Done && !work.IsQuiz && work.Due is not null)
            .Select(work => new Deadline(work.Subject, work.Name, work.Due!.Value, true))
            .ToList();

        var own = items
            .Where(row => !row.Item.Done && row.Item.Due is not null)
            .Where(row => !works.Any(work => IsSameWork(work, row.Subject, row.Item)))
            .Select(row => new Deadline(row.Subject, row.Item.Title, row.Item.Due!.Value, false));

        return
        [
            .. lms.Concat(own)
                .Where(deadline => deadline.Due >= today.AddDays(-overdueDays) && deadline.Due <= today.AddDays(aheadDays))
                .OrderBy(deadline => deadline.Due)
                .ThenBy(deadline => deadline.Subject, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>
    /// Подпись срока для карточки: «сегодня», «завтра», «через N дн.» или «просрочено на N дн.»
    /// </summary>
    /// <param name="due">Дата сдачи</param>
    /// <param name="today">Сегодняшняя дата</param>
    /// <returns>Короткая подпись</returns>
    public static string When(DateOnly due, DateOnly today)
    {
        var days = due.DayNumber - today.DayNumber;

        return days switch
        {
            0 => "сегодня",
            1 => "завтра",
            > 1 => $"через {days} дн.",
            _ => $"просрочено на {-days} дн.",
        };
    }

    /// <summary>
    /// Найденное в расшифровке задание относится к работе из LMS: тот же номер работы или тот же предмет и срок
    /// </summary>
    /// <param name="work">Работа из LMS</param>
    /// <param name="subject">Предмет задания</param>
    /// <param name="item">Задание</param>
    /// <returns>
    /// <see langword="true"/>, если это одна и та же работа
    /// </returns>
    private static bool IsSameWork(ActivityInfo work, string subject, HomeworkItem item) =>
        (item.ActivityId.Length > 0 && string.Equals(item.ActivityId, work.Id, StringComparison.Ordinal))
        || (string.Equals(work.Subject, subject, StringComparison.OrdinalIgnoreCase) && work.Due is not null && work.Due == item.Due);
}
