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
}
