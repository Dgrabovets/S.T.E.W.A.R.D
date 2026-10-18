namespace UltimateParser.Tray.Monitoring.Models;

/// <summary>
/// Состояние контейнера синхронизации на момент опроса
/// </summary>
public sealed record SyncSnapshot
{
    /// <summary>
    /// Docker отвечает на команды
    /// </summary>
    public bool DockerAvailable { get; init; }

    /// <summary>
    /// Контейнер сервиса <c>sync</c> сейчас запущен
    /// </summary>
    public bool ContainerRunning { get; init; }

    /// <summary>
    /// Местное время последнего успешного прогона
    /// </summary>
    public DateTimeOffset? LastSuccess { get; init; }

    /// <summary>
    /// Текст текущей проблемы. Пусто, если последний прогон завершился и сбоя нет
    /// </summary>
    public string? CurrentError { get; init; }

    /// <summary>
    /// Журнал прогонов от новых к старым
    /// </summary>
    public IReadOnlyList<LogEntry> Entries { get; init; } = [];
}
