using System.Globalization;
using Steward.Homework.Models;
using Steward.Mail.Models;
using Steward.Schedule.Common.Helpers;

namespace Steward.Homework.Services;

/// <summary>
/// Собирает задачу SingularityApp по заданию из занятия
/// </summary>
public static class HomeworkTaskBuilder
{
    /// <summary>
    /// Начало внешнего ключа задачи ДЗ
    /// </summary>
    private const string EXTERNAL_PREFIX = "hw:";

    /// <summary>
    /// Внешний ключ: по нему повторная проверка узнаёт, что задача уже есть
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <returns>Строка вида <c>hw:&lt;занятие&gt;:&lt;номер&gt;</c></returns>
    public static string ExternalId(Lesson lesson, int number) => $"{EXTERNAL_PREFIX}{lesson.Id}:{number}";

    /// <summary>
    /// Внешний ключ задачи, заведённой прямо из отрывка расшифровки, без добавления задания в список домашних
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="index">Номер задания в разборе занятия, с единицы</param>
    /// <returns>Строка вида <c>hwq:&lt;занятие&gt;:&lt;номер&gt;</c></returns>
    public static string QuoteExternalId(Lesson lesson, int index) => $"hwq:{lesson.Id}:{index}";

    /// <summary>
    /// Ключи, под которыми задача по заданию из списка могла быть заведена в SingularityApp: свой ключ задания и ключи задач, заведённых по тому же заданию из отрывка расшифровки. Нужны, чтобы две кнопки не создали две задачи
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания в списке занятия, с единицы</param>
    /// <param name="items">Задания занятия</param>
    /// <param name="tasks">Задания из разбора занятия</param>
    /// <returns>Ключи задач; первый всегда ключ самого задания</returns>
    public static IReadOnlyList<string> KeysForItem(Lesson lesson, int number, IReadOnlyList<HomeworkItem> items, IReadOnlyList<AnalysisTask> tasks)
    {
        var keys = new List<string> { ExternalId(lesson, number) };
        if (number < 1 || number > items.Count)
            return keys;

        for (var index = 1; index <= tasks.Count; index++)
        {
            if (TaskMatch.Same(items[number - 1].Title, items[number - 1].Evidence, tasks[index - 1].Title, tasks[index - 1].Quote))
                keys.Add(QuoteExternalId(lesson, index));
        }

        return keys;
    }

    /// <summary>
    /// Ключи, под которыми задача по заданию из отрывка расшифровки могла быть заведена в SingularityApp: свой ключ и ключи задач по тем же заданиям из списка занятия
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="index">Номер задания в разборе занятия, с единицы</param>
    /// <param name="items">Задания занятия</param>
    /// <param name="tasks">Задания из разбора занятия</param>
    /// <returns>Ключи задач; первый всегда ключ самого задания из отрывка</returns>
    public static IReadOnlyList<string> KeysForQuote(Lesson lesson, int index, IReadOnlyList<HomeworkItem> items, IReadOnlyList<AnalysisTask> tasks)
    {
        var keys = new List<string> { QuoteExternalId(lesson, index) };
        if (index < 1 || index > tasks.Count)
            return keys;

        for (var number = 1; number <= items.Count; number++)
        {
            if (TaskMatch.Same(items[number - 1].Title, items[number - 1].Evidence, tasks[index - 1].Title, tasks[index - 1].Quote))
                keys.Add(ExternalId(lesson, number));
        }

        return keys;
    }

    /// <summary>
    /// Собирает задачу по заданию из отрывка расшифровки: слова преподавателя и реплики вокруг них идут в заметку. Начало задачи это день срока, а без срока день занятия
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="task">Задача из разбора занятия</param>
    /// <param name="index">Номер задания в разборе занятия, с единицы</param>
    /// <param name="projectId">Проект SingularityApp или <see langword="null"/></param>
    /// <param name="offset">Часовой пояс начала дня</param>
    /// <returns>Запрос на создание задачи</returns>
    public static SingularityTaskRequest BuildFromQuote(Lesson lesson, AnalysisTask task, int index, string? projectId, TimeSpan offset)
    {
        var due = DueParser.Parse(task.Due, lesson.Date);
        var note = new QuillDelta().Text($"Занятие: {lesson.Title}, {lesson.Date:dd.MM.yyyy}\n");
        if (due is { } day)
            note.Text($"Срок: {day:dd.MM.yyyy}\n");
        else if (task.Due.Length > 0)
            note.Text($"Срок: {task.Due}\n");

        string[] lines = task.HasExcerpt ? [.. task.Before ?? [], .. task.Said!, .. task.After ?? []] : task.Quote.Length > 0 ? [task.Quote] : [];
        if (lines.Length > 0)
        {
            note.Text("\nИз расшифровки:\n");
            foreach (var line in lines)
                note.Text($"{line}\n");
        }

        return new SingularityTaskRequest
        {
            Title = $"ДЗ: {task.Title}",
            Note = note.ToString(),
            Priority = 1,
            Start = new DateTimeOffset((due ?? lesson.Date).ToDateTime(TimeOnly.MinValue), offset).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            ExternalId = QuoteExternalId(lesson, index),
            ProjectId = projectId,
        };
    }

    /// <summary>
    /// Собирает задачу. Начало задачи — день срока, а без срока день занятия
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="item">Задание</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <param name="format">Формат, в котором готовить работу</param>
    /// <param name="projectId">Проект SingularityApp или <see langword="null"/></param>
    /// <param name="offset">Часовой пояс начала дня</param>
    /// <param name="drafts">Пути к готовым файлам по этому заданию. Singularity не принимает вложения, поэтому в заметку идут ссылки на файлы на диске</param>
    /// <returns>Запрос на создание задачи</returns>
    public static SingularityTaskRequest Build(Lesson lesson, HomeworkItem item, int number, HomeworkFormatEnum format, string? projectId, TimeSpan offset, IReadOnlyList<string>? drafts = null)
    {
        var day = item.Due ?? lesson.Date;
        var note = new QuillDelta().Text($"Занятие: {lesson.Title}, {lesson.Date:dd.MM.yyyy}\nФормат: {HomeworkFormats.Title(format)}\n");
        if (item.Due is { } due)
            note.Text($"Срок: {due:dd.MM.yyyy}\n");

        if (item.Requirements.Length > 0)
            note.Text($"\n{item.Requirements}\n");

        if (item.Link.Length > 0)
            note.Divider().Link("Задание", item.Link);

        if (drafts is { Count: > 0 })
        {
            note.Text("\nГотовая работа:\n");
            foreach (var draft in drafts)
                note.Link(Path.GetFileName(draft), new Uri(Path.GetFullPath(draft)).AbsoluteUri).Text("\n");
        }

        return new SingularityTaskRequest
        {
            Title = $"ДЗ: {item.Title}",
            Note = note.ToString(),
            Priority = 1,
            Start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), offset).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            ExternalId = ExternalId(lesson, number),
            ProjectId = projectId,
        };
    }
}