namespace Steward.Models;

/// <summary>
/// Общие подписи записей журнала: запуски задач и сверки расписания говорят об одном и том же одними словами
/// </summary>
public static class JournalWording
{
    /// <summary>
    /// Подпись у записи, под которую свёрнуто несколько одинаковых подряд
    /// </summary>
    /// <param name="count">Сколько одинаковых записей подряд</param>
    /// <returns>Подпись или пустая строка, если запись одна</returns>
    public static string Repeats(int count) => count > 1 ? $"Одинаковых подряд: {count}" : string.Empty;

    /// <summary>
    /// Подпись «сколько шла запись» для запуска задачи и для сверки расписания
    /// </summary>
    /// <param name="span">Сколько прошло от начала до конца</param>
    /// <returns>Например «меньше секунды», «8 с.», «1 мин. 5 с.»</returns>
    public static string Duration(TimeSpan span) => span.TotalSeconds switch
    {
        < 1 => "меньше секунды",
        < 60 => $"{(int)span.TotalSeconds} с.",
        < 3600 => $"{(int)span.TotalMinutes} мин. {span.Seconds} с.",
        _ => $"{(int)span.TotalHours} ч. {span.Minutes} мин.",
    };
}
