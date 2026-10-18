namespace UltimateParser.Tray.Monitoring.Models;

/// <summary>
/// Тон записи журнала
/// </summary>
public enum LogTone
{
    /// <summary>
    /// Прогон ничего не изменил
    /// </summary>
    Quiet,

    /// <summary>
    /// В расписании появились правки
    /// </summary>
    Update,

    /// <summary>
    /// Прогон закончился сбоем
    /// </summary>
    Problem,
}
