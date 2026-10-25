using System.Globalization;

namespace Steward.Status;

/// <summary>
/// Считает, когда задаче пора запускаться. Время по расписанию берётся по местным часам, поэтому переход на зимнее и летнее время ничего не ломает
/// </summary>
public static class JobSchedule
{
    /// <summary>
    /// Сообщает, есть ли у задачи условие заданного вида
    /// </summary>
    /// <param name="job">
    /// Задача
    /// </param>
    /// <param name="type">
    /// Вид условия
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если такое условие есть
    /// </returns>
    public static bool HasTrigger(JobDefinition job, string type) =>
        job.Triggers.Any(trigger => string.Equals(trigger.Type, type, StringComparison.Ordinal));

    /// <summary>
    /// Говорит, пора ли запускать задачу по времени. Пропущенный запуск наверстывается один раз, как «запустить при первой возможности» в Планировщике
    /// </summary>
    /// <param name="job">
    /// Задача
    /// </param>
    /// <param name="lastRunAt">
    /// Когда задача запускалась в последний раз или <see langword="null"/>, если ни разу
    /// </param>
    /// <param name="now">
    /// Текущий момент
    /// </param>
    /// <param name="zone">
    /// Часовой пояс, в котором понимаются времена <c>daily</c>
    /// </param>
    /// <returns>
    /// Причина запуска <c>daily</c> или <c>interval</c>, либо <see langword="null"/>, если ещё рано
    /// </returns>
    public static string? DueReason(JobDefinition job, DateTimeOffset? lastRunAt, DateTimeOffset now, TimeZoneInfo zone)
    {
        foreach (var trigger in job.Triggers)
        {
            if (trigger.Type == JobTrigger.DAILY && TryTime(trigger, out var time))
            {
                if (lastRunAt is null || lastRunAt < PreviousDaily(time, now, zone))
                    return JobTrigger.DAILY;
            }
            else if (trigger.Type == JobTrigger.INTERVAL && trigger.EveryMinutes is { } minutes)
            {
                if (lastRunAt is null || now - lastRunAt >= TimeSpan.FromMinutes(minutes))
                    return JobTrigger.INTERVAL;
            }
        }

        return null;
    }

    /// <summary>
    /// Находит ближайший будущий запуск по времени
    /// </summary>
    /// <param name="job">
    /// Задача
    /// </param>
    /// <param name="lastRunAt">
    /// Когда задача запускалась в последний раз или <see langword="null"/>
    /// </param>
    /// <param name="now">
    /// Текущий момент
    /// </param>
    /// <param name="zone">
    /// Часовой пояс для <c>daily</c>
    /// </param>
    /// <returns>
    /// Момент следующего запуска или <see langword="null"/>, если у задачи нет условий по времени
    /// </returns>
    public static DateTimeOffset? NextRun(JobDefinition job, DateTimeOffset? lastRunAt, DateTimeOffset now, TimeZoneInfo zone)
    {
        DateTimeOffset? best = null;
        foreach (var trigger in job.Triggers)
        {
            DateTimeOffset? candidate = null;
            if (trigger.Type == JobTrigger.DAILY && TryTime(trigger, out var time))
                candidate = NextDaily(time, now, zone);
            else if (trigger.Type == JobTrigger.INTERVAL && trigger.EveryMinutes is { } minutes)
                candidate = lastRunAt is null ? now : Max(now, lastRunAt.Value.AddMinutes(minutes));

            if (candidate is not null && (best is null || candidate < best))
                best = candidate;
        }

        return best;
    }

    #region Daily

    /// <summary>
    /// Последний момент <paramref name="time"/> не позже <paramref name="now"/>
    /// </summary>
    /// <param name="time">
    /// Местное время суток
    /// </param>
    /// <param name="now">
    /// Текущий момент
    /// </param>
    /// <param name="zone">
    /// Часовой пояс
    /// </param>
    /// <returns>
    /// Момент сегодняшнего или вчерашнего запуска
    /// </returns>
    private static DateTimeOffset PreviousDaily(TimeOnly time, DateTimeOffset now, TimeZoneInfo zone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var candidate = Occurrence(today, time, zone);
        return candidate <= now ? candidate : Occurrence(today.AddDays(-1), time, zone);
    }

    /// <summary>
    /// Первый момент <paramref name="time"/> строго после <paramref name="now"/>
    /// </summary>
    /// <param name="time">
    /// Местное время суток
    /// </param>
    /// <param name="now">
    /// Текущий момент
    /// </param>
    /// <param name="zone">
    /// Часовой пояс
    /// </param>
    /// <returns>
    /// Момент ближайшего запуска
    /// </returns>
    private static DateTimeOffset NextDaily(TimeOnly time, DateTimeOffset now, TimeZoneInfo zone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var candidate = Occurrence(today, time, zone);
        return candidate > now ? candidate : Occurrence(today.AddDays(1), time, zone);
    }

    /// <summary>
    /// Переводит местные дату и время в момент. Несуществующее время при переходе на летнее сдвигается на час вперёд, повторяющееся при переходе на зимнее берётся первым из двух
    /// </summary>
    /// <param name="day">
    /// Местная дата
    /// </param>
    /// <param name="time">
    /// Местное время суток
    /// </param>
    /// <param name="zone">
    /// Часовой пояс
    /// </param>
    /// <returns>
    /// Момент со смещением, верным для этой даты
    /// </returns>
    internal static DateTimeOffset Occurrence(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(time);
        if (zone.IsInvalidTime(local))
            local = local.AddHours(1);

        var offset = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    /// <summary>
    /// Читает время <c>HH:mm</c> из условия
    /// </summary>
    /// <param name="trigger">
    /// Условие вида <c>daily</c>
    /// </param>
    /// <param name="time">
    /// Время суток
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если время записано правильно
    /// </returns>
    private static bool TryTime(JobTrigger trigger, out TimeOnly time) =>
        TimeOnly.TryParseExact(trigger.At, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    /// <summary>
    /// Выбирает более поздний из двух моментов
    /// </summary>
    /// <param name="first">
    /// Первый момент
    /// </param>
    /// <param name="second">
    /// Второй момент
    /// </param>
    /// <returns>
    /// Более поздний
    /// </returns>
    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second) => first >= second ? first : second;

    #endregion Daily
}
