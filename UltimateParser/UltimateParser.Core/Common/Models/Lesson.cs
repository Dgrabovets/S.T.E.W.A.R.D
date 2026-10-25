namespace UltimateParser.Common.Models;

/// <summary>
/// Одно занятие из заполненной ячейки группы
/// </summary>
public sealed record Lesson
{
    /// <summary>
    /// Создаёт занятие, которое можно записать в Singularity
    /// </summary>
    /// <param name="externalId">
    /// Стабильный ключ, сохраняемый в <c>externalId</c> задачи
    /// </param>
    /// <param name="title">
    /// Короткое название предмета, с типом занятия, если он найден
    /// </param>
    /// <param name="note">
    /// Полный текст ячейки, который уходит в заметку задачи
    /// </param>
    /// <param name="start">
    /// Местное время начала занятия со смещением часового пояса расписания
    /// </param>
    /// <param name="timeLengthMinutes">
    /// Длительность занятия в минутах
    /// </param>
    public Lesson(string externalId, string title, string note, DateTimeOffset start, int timeLengthMinutes)
    {
        ExternalId = externalId;
        Title = title;
        Note = note;
        Start = start;
        TimeLengthMinutes = timeLengthMinutes;
    }

    /// <summary>
    /// Стабильный ключ вида <c>up:{group}:{yyyy-MM-dd}:{HHmm}</c>
    /// </summary>
    public string ExternalId { get; init; }

    /// <summary>
    /// Предмет, который становится названием задачи в Singularity
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Преподаватель и должность, которые хранятся в заметке задачи
    /// </summary>
    public string Note { get; init; }

    /// <summary>
    /// Момент начала занятия
    /// </summary>
    public DateTimeOffset Start { get; init; }

    /// <summary>
    /// Длительность в минутах, взятая из диапазона слота
    /// </summary>
    public int TimeLengthMinutes { get; init; }

    /// <summary>
    /// Идентификаторы тегов, которые должны стоять на задаче. Пусто, пока теги предметов выключены
    /// </summary>
    public string[] TagIds { get; init; } = [];

    /// <summary>
    /// Предмет без типа занятия: из названия вида <c>Предмет (Практика)</c> остаётся <c>Предмет</c>
    /// </summary>
    public string Subject
    {
        get
        {
            var open = Title.LastIndexOf(" (", StringComparison.Ordinal);
            return open > 0 && Title.EndsWith(')') ? Title[..open].Trim() : Title.Trim();
        }
    }
}
