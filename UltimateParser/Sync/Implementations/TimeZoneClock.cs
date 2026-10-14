using UltimateParser.Common.Models;
using UltimateParser.Sync.Interfaces;

namespace UltimateParser.Sync.Implementations;

/// <summary>
/// Часы, которые сообщают текущую дату в <see cref="SyncOptions.TimeZoneId"/>
/// </summary>
public sealed class TimeZoneClock : IScheduleClock
{
    /// <summary>
    /// Пояс, через который текущее время UTC переводится в календарную дату
    /// </summary>
    private readonly TimeZoneInfo _timeZone;

    /// <summary>
    /// Создаёт часы для пояса, указанного в <paramref name="options"/>
    /// </summary>
    /// <param name="options">
    /// Настройки, чей идентификатор пояса разрешается один раз
    /// </param>
    public TimeZoneClock(SyncOptions options)
    {
        _timeZone = SyncOptions.ResolveTimeZone(options.TimeZoneId);
    }

    /// <summary>
    /// Текущий момент в настроенном часовом поясе
    /// </summary>
    public DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _timeZone);

    /// <summary>
    /// Текущая календарная дата в настроенном часовом поясе
    /// </summary>
    public DateOnly Today => DateOnly.FromDateTime(Now.Date);
}
