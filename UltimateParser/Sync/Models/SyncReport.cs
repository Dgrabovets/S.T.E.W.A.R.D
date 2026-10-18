namespace UltimateParser.Sync.Models;

/// <summary>
/// Счётчики одного прогона синхронизации
/// </summary>
public sealed record SyncReport
{
    /// <summary>
    /// Создаёт отчёт одного прогона
    /// </summary>
    /// <param name="created">
    /// Созданные задачи
    /// </param>
    /// <param name="updated">
    /// Задачи, у которых изменились поля
    /// </param>
    /// <param name="archived">
    /// Задачи, отправленные в архив, потому что ячейка опустела
    /// </param>
    /// <param name="unarchived">
    /// Задачи, возвращённые из архива, потому что ячейка снова заполнена
    /// </param>
    /// <param name="completed">
    /// Задачи, отмеченные выполненными, потому что занятие уже закончилось
    /// </param>
    /// <param name="skipped">
    /// Уже выполненные задачи, которые не менялись
    /// </param>
    /// <param name="deleted">
    /// Выполненные задачи, удалённые потому что время занятия ещё не прошло
    /// </param>
    public SyncReport(int created, int updated, int archived, int unarchived, int completed, int skipped, int deleted = 0)
    {
        Created = created;
        Updated = updated;
        Archived = archived;
        Unarchived = unarchived;
        Completed = completed;
        Skipped = skipped;
        Deleted = deleted;
    }

    /// <summary>
    /// Число созданных задач
    /// </summary>
    public int Created { get; init; }

    /// <summary>
    /// Число обновлённых задач
    /// </summary>
    public int Updated { get; init; }

    /// <summary>
    /// Число задач, отправленных в архив
    /// </summary>
    public int Archived { get; init; }

    /// <summary>
    /// Число задач, возвращённых из архива
    /// </summary>
    public int Unarchived { get; init; }

    /// <summary>
    /// Число задач, отмеченных выполненными
    /// </summary>
    public int Completed { get; init; }

    /// <summary>
    /// Число уже выполненных задач, которые оставлены как есть
    /// </summary>
    public int Skipped { get; init; }

    /// <summary>
    /// Число удалённых задач, чьё занятие ещё не закончилось
    /// </summary>
    public int Deleted { get; init; }
}
