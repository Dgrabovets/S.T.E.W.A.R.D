using Steward.Homework.Models;
using Steward.Homework.Services;

namespace Steward.Homework.Storage;

/// <summary>
/// Задания из разбора занятия, которые владелец убрал, и состояние каждого задания
/// </summary>
public sealed partial class HomeworkStore
{
    /// <summary>
    /// Разделитель названия и слов преподавателя в записи убранного задания. Старые записи хранят одно название без разделителя
    /// </summary>
    private const char DISMISSED_SEPARATOR = '\n';

    /// <summary>
    /// Файл с заданиями из разбора, которые владелец убрал: по ключу занятия лежат их записи «название» или «название, разделитель, слова преподавателя»
    /// </summary>
    private string DismissedFile => Path.Combine(directory, "task-dismissed.json");

    /// <summary>
    /// Задания занятия из разбора, которые владелец убрал. Такое задание не считается неразобранным и не показывается на вкладке «Задания»
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Название и слова преподавателя; пусто, если ничего не убирали или файл не читается</returns>
    public IReadOnlyList<(string Title, string Quote)> DismissedTasks(string lessonId) =>
        [.. ((Read<Dictionary<string, List<string>>>(DismissedFile) ?? []).GetValueOrDefault(lessonId) ?? []).Select(entry =>
        {
            var at = entry.IndexOf(DISMISSED_SEPARATOR, StringComparison.Ordinal);

            return at < 0 ? (entry, string.Empty) : (entry[..at], entry[(at + 1)..]);
        })];

    /// <summary>
    /// Убирает задание из разбора занятия или возвращает его. Задание узнаётся по названию или по словам преподавателя, поэтому после пересчёта разбора с новой формулировкой оно не возвращается
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="title">Название задания в разборе</param>
    /// <param name="quote">Слова преподавателя из разбора; пусто, если их нет</param>
    /// <param name="dismissed"><see langword="true"/>, чтобы убрать; <see langword="false"/>, чтобы вернуть</param>
    /// <exception cref="IOException">Файл занят другим процессом дольше ожидания или не записался</exception>
    public void SetTaskDismissed(string lessonId, string title, string quote, bool dismissed) => Locked(() =>
    {
        var all = new Dictionary<string, List<string>>(JsonFiles.ReadForWrite<Dictionary<string, List<string>>>(DismissedFile, Json) ?? [], StringComparer.Ordinal);
        var entries = (all.GetValueOrDefault(lessonId) ?? []).Where(entry =>
        {
            var at = entry.IndexOf(DISMISSED_SEPARATOR, StringComparison.Ordinal);

            return !TaskMatch.Same(at < 0 ? entry : entry[..at], at < 0 ? string.Empty : entry[(at + 1)..], title, quote);
        }).ToList();
        if (dismissed)
            entries.Add(quote.Trim().Length > 0 ? title.Trim() + DISMISSED_SEPARATOR + quote.Trim() : title.Trim());

        if (entries.Count > 0)
            all[lessonId] = entries;
        else
            all.Remove(lessonId);

        WriteAtomic(DismissedFile, all);
    });

    /// <summary>
    /// Состояние каждого задания из разбора занятия: лежит ли оно в списке заданий (в том числе в архиве), заведена ли по нему задача в Singularity, убрал ли его владелец. Каждый файл читается один раз
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>Состояния по порядку заданий в разборе; пусто, если разбора нет</returns>
    public IReadOnlyList<(bool Listed, bool InSingularity, bool Dismissed)> AssignmentStates(Lesson lesson)
    {
        var tasks = ReadAnalysis(lesson.Id)?.Tasks ?? [];
        if (tasks.Count == 0)
            return [];

        var items = ReadItemsFile(lesson.Id);
        var created = LoadCreated();
        var dismissed = DismissedTasks(lesson.Id);

        return [.. tasks.Select((task, index) => (
            items.Any(item => TaskMatch.Same(item.Title, item.Evidence, task.Title, task.Quote)),
            HomeworkTaskBuilder.KeysForQuote(lesson, index + 1, items, tasks).Any(created.Contains),
            dismissed.Any(entry => TaskMatch.Same(entry.Title, entry.Quote, task.Title, task.Quote))))];
    }
}
