namespace Steward.Schedule.Common.Constants;

/// <summary>
/// Начала строк журнала, по которым приложение в трее узнаёт, что сделала синхронизация. Сервис пишет их, приложение читает, поэтому текст живёт в одном месте
/// </summary>
public static class SyncLogMessages
{
    /// <summary>
    /// Задача создана
    /// </summary>
    public const string CREATED = "Created ";

    /// <summary>
    /// Задача обновлена
    /// </summary>
    public const string UPDATED = "Updated ";

    /// <summary>
    /// Задача возвращена из архива
    /// </summary>
    public const string UNARCHIVED = "Unarchived ";

    /// <summary>
    /// Прошедшее занятие отмечено выполненным
    /// </summary>
    public const string COMPLETED_PAST = "Completed past lesson ";

    /// <summary>
    /// Опустевшая ячейка отмечена выполненной
    /// </summary>
    public const string COMPLETED_REMOVED = "Completed removed lesson ";

    /// <summary>
    /// Выполненная задача занятия, которое ещё не закончилось, удалена
    /// </summary>
    public const string DELETED_FUTURE = "Deleted completed future task ";

    /// <summary>
    /// Выполненная задача оставлена как есть
    /// </summary>
    public const string SKIPPED_COMPLETED = "Skipped completed task ";

    /// <summary>
    /// Итог прогона со счётчиками
    /// </summary>
    public const string FINISHED = "Sync finished: ";

    /// <summary>
    /// Прогон не удался. Причина идёт в следующей строке
    /// </summary>
    public const string FAILED = "Sync failed";
}