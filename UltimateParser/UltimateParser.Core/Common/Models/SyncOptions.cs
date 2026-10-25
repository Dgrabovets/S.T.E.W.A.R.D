namespace UltimateParser.Common.Models;

/// <summary>
/// Настройки листа, проекта назначения и частоты синхронизации
/// </summary>
public sealed record SyncOptions
{
    /// <summary>
    /// Токен доступа к REST API Singularity, обязателен
    /// </summary>
    public required string SingularityToken { get; init; }

    /// <summary>
    /// Идентификатор таблицы Google из её адреса
    /// </summary>
    public required string SpreadsheetId { get; init; }

    /// <summary>
    /// Gid листа из параметра <c>gid</c>
    /// </summary>
    public required string SheetGid { get; init; }

    /// <summary>
    /// Заголовок колонки группы, из которой создаются задачи
    /// </summary>
    public required string GroupColumn { get; init; }

    /// <summary>
    /// Название проекта Singularity, в который попадают задачи
    /// </summary>
    public required string ProjectTitle { get; init; }

    /// <summary>
    /// Пояс, в котором выбирается год даты без года и ставится время начала
    /// </summary>
    public required string TimeZoneId { get; init; }

    /// <summary>
    /// Пауза между прогонами в минутах, пока процесс работает
    /// </summary>
    public required int IntervalMinutes { get; init; }

    /// <summary>
    /// <see langword="true"/>, если процесс должен завершиться после одного прогона
    /// </summary>
    public bool RunOnce { get; init; }
}
