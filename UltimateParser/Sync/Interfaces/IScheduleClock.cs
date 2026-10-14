namespace UltimateParser.Sync.Interfaces;

/// <summary>
/// Даёт дату, которую синхронизация считает сегодняшней
/// </summary>
public interface IScheduleClock
{
    /// <summary>
    /// Сегодняшняя дата в часовом поясе расписания
    /// </summary>
    DateOnly Today { get; }

    /// <summary>
    /// Текущий момент в часовом поясе расписания
    /// </summary>
    DateTimeOffset Now { get; }
}
