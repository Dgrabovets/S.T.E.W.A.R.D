using Steward.Schedule.Singularity.Models;

namespace Steward.Schedule.Sync.Models;

/// <summary>
/// Что нужно знать одному прогону синхронизации, пока он обходит слоты, и счётчики его действий
/// </summary>
/// <param name="ProjectId">Проект, в который пишутся задачи</param>
/// <param name="TargetTaskIds">Задачи, которые уже лежат в этом проекте</param>
/// <param name="Notices">Пометки об отменах и переносах по внешнему идентификатору занятия</param>
/// <param name="SubjectTags">Идентификаторы тегов по названию предмета</param>
internal sealed record SyncRun(
    string ProjectId,
    HashSet<string> TargetTaskIds,
    Dictionary<string, List<string>> Notices,
    Dictionary<string, string> SubjectTags)
{
    /// <summary>
    /// Создано задач
    /// </summary>
    public int Created { get; set; }

    /// <summary>
    /// Обновлено задач
    /// </summary>
    public int Updated { get; set; }

    /// <summary>
    /// Возвращено из архива
    /// </summary>
    public int Unarchived { get; set; }

    /// <summary>
    /// Отмечено выполненными
    /// </summary>
    public int Completed { get; set; }

    /// <summary>
    /// Оставлено как есть, потому что уже выполнено
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// Удалено выполненных задач занятий, которые ещё не закончились
    /// </summary>
    public int Deleted { get; set; }

    /// <summary>
    /// Собирает итог прогона
    /// </summary>
    /// <returns>Отчёт со счётчиками</returns>
    public SyncReport ToReport() => new(Created, Updated, Unarchived, Completed, Skipped, Deleted);
}