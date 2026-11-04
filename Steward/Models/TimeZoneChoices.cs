using System.Globalization;

namespace Steward.Models;

/// <summary>
/// Собирает список часовых поясов компьютера и объясняет разницу между поясом таблицы и местным
/// </summary>
public static class TimeZoneChoices
{
    /// <summary>
    /// Все пояса компьютера с идентификаторами IANA. Пояс из настроек, которого нет в системе, добавляется первым, чтобы значение не потерялось
    /// </summary>
    /// <param name="current">Пояс, записанный сейчас в настройках</param>
    /// <returns>Пояса от западных к восточным</returns>
    public static IReadOnlyList<TimeZoneChoice> All(string current)
    {
        var choices = new List<TimeZoneChoice>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            if (!TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) || !seen.Add(iana))
                continue;

            choices.Add(new TimeZoneChoice(iana, $"{zone.DisplayName}  ·  {iana}"));
        }

        if (current.Trim().Length > 0 && seen.Add(current.Trim()))
            choices.Insert(0, new TimeZoneChoice(current.Trim(), current.Trim()));

        return choices;
    }

    /// <summary>
    /// Пишет, сколько сейчас времени в поясе таблицы и на компьютере и чем они отличаются
    /// </summary>
    /// <param name="timeZoneId">Пояс таблицы</param>
    /// <param name="now">Текущий момент</param>
    /// <returns>Фраза для подписи под выбором. Пусто, если пояс не опознан</returns>
    public static string Compare(string timeZoneId, DateTimeOffset now)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out var zone))
            return string.Empty;

        var table = TimeZoneInfo.ConvertTime(now, zone);
        var local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local);
        var difference = table.Offset - local.Offset;
        var times = $"Сейчас в таблице {table.ToString("HH:mm", CultureInfo.InvariantCulture)}, на этом компьютере {local.ToString("HH:mm", CultureInfo.InvariantCulture)}";

        if (difference == TimeSpan.Zero)
            return times + ". Пояса совпадают";

        var hours = Math.Abs(difference.TotalHours).ToString("0.##", CultureInfo.InvariantCulture);

        return $"{times}. Таблица {(difference > TimeSpan.Zero ? "впереди" : "позади")} на {hours} ч";
    }
}
