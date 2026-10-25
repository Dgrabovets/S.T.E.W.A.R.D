namespace Steward.Schedule.Singularity.Models;

/// <summary>
/// Задача из API Singularity, по которой выбирается следующее действие синхронизации
/// </summary>
public sealed record SingularityTask
{
    /// <summary>
    /// Создаёт снимок одной задачи Singularity
    /// </summary>
    /// <param name="id">
    /// Идентификатор задачи, начинается с <c>T-</c>
    /// </param>
    /// <param name="externalId">
    /// Ключ, записанный этой синхронизацией, или <see langword="null"/> у задачи, созданной вручную
    /// </param>
    /// <param name="title">Текущее название</param>
    /// <param name="note">Текущая заметка: обычный текст или дельта Quill</param>
    /// <param name="start">Значение начала, которое вернул API</param>
    /// <param name="useTime">
    /// <see langword="true"/>, если у задачи указано время, а не весь день
    /// </param>
    /// <param name="timeLength">Длительность в минутах, если API её прислал</param>
    /// <param name="checked">Состояние отметки: 0 пусто, 1 выполнено, 2 отменено</param>
    /// <param name="journalDate">
    /// Дата архивации или <see langword="null"/>, если задача активна
    /// </param>
    /// <param name="removed">
    /// <see langword="true"/>, если задача в корзине
    /// </param>
    /// <param name="notifies">
    /// Минуты до начала, за которые стоит напоминание, или <see langword="null"/>, если API их не прислал
    /// </param>
    /// <param name="tags">
    /// Идентификаторы тегов задачи или <see langword="null"/>, если API их не прислал
    /// </param>
    public SingularityTask(
        string id,
        string? externalId,
        string? title,
        string? note,
        string? start,
        bool useTime,
        int? timeLength,
        int? @checked,
        string? journalDate,
        bool removed,
        int[]? notifies = null,
        string[]? tags = null)
    {
        Id = id;
        ExternalId = externalId;
        Title = title;
        Note = note;
        Start = start;
        UseTime = useTime;
        TimeLength = timeLength;
        Checked = @checked;
        JournalDate = journalDate;
        Removed = removed;
        Notifies = notifies;
        Tags = tags;
    }

    /// <summary>
    /// Идентификатор задачи Singularity
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Внешний идентификатор, по которому слот расписания сопоставляется с задачей
    /// </summary>
    public string? ExternalId { get; init; }

    /// <summary>
    /// Название, которое сейчас хранится в Singularity
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Заметка, которая сейчас хранится в Singularity
    /// </summary>
    public string? Note { get; init; }

    /// <summary>
    /// Строка даты или даты со временем, которую вернул API
    /// </summary>
    public string? Start { get; init; }

    /// <summary>
    /// <see langword="true"/>, если у задачи указано время, а не весь день
    /// </summary>
    public bool UseTime { get; init; }

    /// <summary>
    /// Длительность в минутах или <see langword="null"/>, если API её не прислал
    /// </summary>
    public int? TimeLength { get; init; }

    /// <summary>
    /// Состояние отметки. <c>1</c> значит, что пользователь отметил задачу выполненной
    /// </summary>
    public int? Checked { get; init; }

    /// <summary>
    /// Дата архивации. Значение означает, что задача в журнале
    /// </summary>
    public string? JournalDate { get; init; }

    /// <summary>
    /// <see langword="true"/>, если задача отправлена в корзину
    /// </summary>
    public bool Removed { get; init; }

    /// <summary>
    /// Минуты до начала, за которые у задачи стоят напоминания
    /// </summary>
    public int[]? Notifies { get; init; }

    /// <summary>
    /// Идентификаторы тегов, которые сейчас стоят на задаче
    /// </summary>
    public string[]? Tags { get; init; }

    /// <summary>
    /// <see langword="true"/>, если пользователь отметил задачу выполненной
    /// </summary>
    public bool IsCompleted => Checked == 1;

    /// <summary>
    /// <see langword="true"/>, если задача в архиве
    /// </summary>
    public bool IsArchived => !string.IsNullOrWhiteSpace(JournalDate);
}
