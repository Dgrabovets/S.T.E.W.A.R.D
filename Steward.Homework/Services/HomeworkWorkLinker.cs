using Steward.Homework.Models;
using Steward.Homework.Storage;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;
using Steward.Schedule.Common.Helpers;

namespace Steward.Homework.Services;

/// <summary>
/// Дописывает в заметку задачи ДЗ в SingularityApp ссылку на готовый файл работы. Ссылка идёт данными (имя файла и адрес <c>file://</c>), ничего из файла в заметку не попадает
/// </summary>
/// <param name="store">Папка домашних заданий</param>
/// <param name="notes">Клиент SingularityApp для чтения задач и дописывания заметок</param>
/// <param name="timeProvider">Часы: часовой пояс границы поиска задач</param>
/// <param name="log">Принимает строки лога</param>
/// <param name="dryRun">Только писать в лог, что было бы дописано: клиент не вызывается и отметки не ставятся</param>
public sealed class HomeworkWorkLinker(HomeworkStore store, ISingularityNotes notes, TimeProvider timeProvider, Action<string> log, bool dryRun = false)
{
    /// <summary>
    /// Находит задания с готовым файлом и существующей задачей и дописывает в её заметку ссылку на файл. Отметка в <c>created.json</c> ставится после успешной записи, поэтому повторный запуск ничего не дописывает, а сбой повторится в следующий раз
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сколько заметок дописано. При <c>dryRun</c> всегда ноль</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var pending = Pending();
        if (pending.Count == 0)
            return 0;

        if (dryRun)
        {
            foreach (var work in pending)
                log($"Добавил бы ссылку на готовую работу в задачу: {work.Item.Title}");

            return 0;
        }

        IReadOnlyList<SingularityTaskInfo> tasks;
        try
        {
            var from = new DateTimeOffset(pending.Min(work => work.Lesson.Date).AddDays(-1).ToDateTime(TimeOnly.MinValue), timeProvider.GetLocalNow().Offset);
            tasks = await notes.ListTasksAsync(from, "hw:", cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            log($"Не удалось прочитать задачи ДЗ в Singularity: {exception.GetType().Name}: {exception.Message}");

            return 0;
        }

        var byKey = tasks.GroupBy(task => task.ExternalId).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var written = 0;
        foreach (var work in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!byKey.TryGetValue(HomeworkTaskBuilder.ExternalId(work.Lesson, work.Number), out var task) || task.Removed || task.Archived || task.Checked != 0)
                continue;

            try
            {
                var note = new QuillDelta().Text("Готовая работа:\n").Link(Path.GetFileName(work.File), new Uri(Path.GetFullPath(work.File)).AbsoluteUri).Text("\n").ToString();
                await notes.UpdateNoteAsync(task.Id, note, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                log($"Не удалось дописать ссылку на работу в задачу «{work.Item.Title}»: {exception.GetType().Name}: {exception.Message}");

                continue;
            }

            store.MarkCreated([work.Mark]);
            log($"Добавил ссылку на готовую работу в задачу: {work.Item.Title}");
            written++;
        }

        return written;
    }

    /// <summary>
    /// Собирает задания, у которых есть готовый файл, ссылка на него ещё не дописана, а само задание не сдано и не в архиве
    /// </summary>
    /// <returns>Занятие, задание, номер, путь к файлу и отметка для <c>created.json</c></returns>
    private List<(Lesson Lesson, HomeworkItem Item, int Number, string File, string Mark)> Pending()
    {
        var pending = new List<(Lesson Lesson, HomeworkItem Item, int Number, string File, string Mark)>();
        var remembered = store.LoadCreated();
        var doneWorks = store.DoneWorkIds();

        foreach (var lesson in store.LoadLessons())
        {
            if (store.ReadItems(lesson.Id) is not { } items)
                continue;

            var submitted = store.SubmittedAnywhere(lesson.Id, items, doneWorks);
            var archived = store.ArchivedNumbers(lesson.Id);
            for (var number = 1; number <= items.Count; number++)
            {
                if (submitted.Contains(number) || archived.Contains(number) || store.SampleCandidate(lesson.Id, number) is not { } file)
                    continue;

                var mark = $"draft:{lesson.Id}:{number}:{Path.GetFileName(file)}";
                if (!remembered.Contains(mark))
                    pending.Add((lesson, items[number - 1], number, file, mark));
            }
        }

        return pending;
    }
}
