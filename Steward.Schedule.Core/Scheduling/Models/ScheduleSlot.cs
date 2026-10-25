using Steward.Schedule.Common.Models;

namespace Steward.Schedule.Scheduling.Models;

/// <summary>
/// Один датированный временной слот выбранной колонки группы
/// </summary>
public sealed record ScheduleSlot
{
    /// <summary>
    /// Создаёт слот, в котором занятие может отсутствовать
    /// </summary>
    /// <param name="externalId">Ключ даты и времени начала, даже если ячейка пустая</param>
    /// <param name="lesson">
    /// Занятие в этом слоте или <see langword="null"/>, если ячейка пустая
    /// </param>
    public ScheduleSlot(string externalId, Lesson? lesson)
    {
        ExternalId = externalId;
        Lesson = lesson;
    }

    /// <summary>
    /// Идентификатор этой даты и времени начала внутри группы
    /// </summary>
    public string ExternalId { get; init; }

    /// <summary>
    /// Занятие в слоте. <see langword="null"/> означает, что соответствующую задачу нужно отметить выполненной
    /// </summary>
    public Lesson? Lesson { get; init; }
}
