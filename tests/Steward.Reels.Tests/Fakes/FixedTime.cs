namespace Steward.Reels.Tests.Fakes;

/// <summary>
/// Часы, которые показывают одно и то же время
/// </summary>
/// <param name="now">Время часов</param>
public sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    /// <summary>
    /// Текущее время в UTC
    /// </summary>
    /// <returns>Заданное время</returns>
    public override DateTimeOffset GetUtcNow() => now;

    /// <summary>
    /// Часовой пояс часов
    /// </summary>
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}