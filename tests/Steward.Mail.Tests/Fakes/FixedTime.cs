namespace Steward.Mail.Tests.Fakes;

/// <summary>
/// Часы, которые всегда показывают один момент по местному времени UTC
/// </summary>
/// <param name="now">Момент, который показывают часы</param>
public sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    /// <summary>
    /// Пояс часов: UTC, чтобы местный день не зависел от компьютера
    /// </summary>
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <summary>
    /// Возвращает заданный момент
    /// </summary>
    /// <returns>Момент из конструктора</returns>
    public override DateTimeOffset GetUtcNow() => now;
}