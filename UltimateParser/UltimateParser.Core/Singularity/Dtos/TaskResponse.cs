namespace UltimateParser.Singularity.Dtos;

/// <summary>
/// Поля задачи, которые читает клиент Singularity
/// </summary>
internal sealed class TaskResponse
{
    /// <summary>
    /// Идентификатор задачи
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Внешний идентификатор
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Название
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Заметка: обычный текст или дельта
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Дата начала или дата со временем
    /// </summary>
    public string? Start { get; set; }

    /// <summary>
    /// Признак того, что у задачи указано время
    /// </summary>
    public bool? UseTime { get; set; }

    /// <summary>
    /// Длительность в минутах. API присылает число JSON
    /// </summary>
    public double? TimeLength { get; set; }

    /// <summary>
    /// Состояние отметки
    /// </summary>
    public int? Checked { get; set; }

    /// <summary>
    /// Дата архивации
    /// </summary>
    public string? JournalDate { get; set; }

    /// <summary>
    /// Признак того, что задача в корзине
    /// </summary>
    public bool? Removed { get; set; }

    /// <summary>
    /// Минуты до начала, за которые стоят напоминания
    /// </summary>
    public int[]? Notifies { get; set; }

    /// <summary>
    /// Идентификаторы тегов задачи
    /// </summary>
    public string[]? Tags { get; set; }
}
