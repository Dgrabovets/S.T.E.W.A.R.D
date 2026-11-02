using Steward.Homework.Models;
using Steward.Homework.Services;

namespace Steward.Homework.Storage;

/// <summary>
/// Задания из разбора занятия, которые владелец убрал, и состояние каждого задания
/// </summary>
public sealed partial class HomeworkStore
{
    /// <summary>
    /// Файл с заданиями из разбора, которые владелец убрал: по ключу занятия лежат их названия
    /// </summary>
    private string DismissedFile => Path.Combine(directory, "task-dismissed.json");

    /// <summary>
    /// Названия заданий занятия из разбора, которые владелец убрал. Такое задание не считается неразобранным и не показывается на вкладке «Задания»
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Названия без учёта регистра; пусто, если ничего не убирали или файл не читается</returns>
    public IReadOnlySet<string> DismissedTasks(string lessonId) =>
        (Read<Dictionary<string, List<string>>>(DismissedFile) ?? []).GetValueOrDefault(lessonId)?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Убирает задание из разбора занятия или возвращает его
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="title">Название задания в разборе</param>
    /// <param name="dismissed"><see langword="true"/>, чтобы убрать; <see langword="false"/>, чтобы вернуть</param>
    /// <exception cref="IOException">Файл занят другим процессом дольше ожидания или не записался</exception>
    public void SetTaskDismissed(string lessonId, string title, bool dismissed) => Locked(() =>
    {
        var all = new Dictionary<string, List<string>>(JsonFiles.ReadForWrite<Dictionary<string, List<string>>>(DismissedFile, Json) ?? [], StringComparer.Ordinal);
        var titles = (all.GetValueOrDefault(lessonId) ?? []).Where(item => !string.Equals(item, title.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (dismissed)
            titles.Add(title.Trim());

        if (titles.Count > 0)
            all[lessonId] = titles;
        else
            all.Remove(lessonId);

        WriteAtomic(DismissedFile, all);
    });

    /// <summary>
    /// Состояние каждого задания из разбора занятия: лежит ли оно в списке заданий (в том числе в архиве), заведена ли по нему задача в Singularity, убрал ли его владелец
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>Состояния по порядку заданий в разборе; пусто, если разбора нет</returns>
    public IReadOnlyList<(bool Listed, bool InSingularity, bool Dismissed)> AssignmentStates(Lesson lesson)
    {
        var tasks = ReadAnalysis(lesson.Id)?.Tasks ?? [];
        if (tasks.Count == 0)
            return [];

        var items = ReadItems(lesson.Id) ?? [];
        var created = LoadCreated();
        var dismissed = DismissedTasks(lesson.Id);

        return [.. tasks.Select((task, index) => (
            items.Any(item => TaskMatch.Same(item.Title, item.Evidence, task.Title, task.Quote)),
            SingularityKeysForQuote(lesson, index + 1).Any(created.Contains),
            dismissed.Contains(task.Title.Trim())))];
    }
}
