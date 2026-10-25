namespace Steward.Status.Registry;

/// <summary>
/// Условие запуска задачи
/// </summary>
public sealed record JobTrigger
{
    /// <summary>
    /// При запуске приложения
    /// </summary>
    public const string START = "start";

    /// <summary>
    /// Каждый день в заданное местное время
    /// </summary>
    public const string DAILY = "daily";

    /// <summary>
    /// Через заданное число минут после прошлого запуска
    /// </summary>
    public const string INTERVAL = "interval";

    /// <summary>
    /// Вид условия: <c>start</c>, <c>daily</c> или <c>interval</c>
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Время <c>HH:mm</c> для <c>daily</c>
    /// </summary>
    public string? At { get; init; }

    /// <summary>
    /// Число минут для <c>interval</c>
    /// </summary>
    public int? EveryMinutes { get; init; }
}
