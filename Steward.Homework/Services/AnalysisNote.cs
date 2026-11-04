using System.Globalization;
using Steward.Homework.Models;
using Steward.Mail.Models;
using Steward.Schedule.Common.Helpers;

namespace Steward.Homework.Services;

/// <summary>
/// Собирает заметку SingularityApp с подробным разбором занятия. Полный текст по ролям в заметку не попадает
/// </summary>
public static class AnalysisNote
{
    /// <summary>
    /// Префикс внешнего ключа заметок с расшифровками: тот же, что у первой заметки с резюме, поэтому разбор обновляет её, а не создаёт вторую
    /// </summary>
    public const string KEY_PREFIX = "lms:transcript:";

    /// <summary>
    /// Собирает текст заметки в формате Quill Delta
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="analysis">Разбор</param>
    /// <returns>JSON заметки</returns>
    public static string Text(Lesson lesson, LessonAnalysis analysis)
    {
        var note = new QuillDelta().Text($"Занятие: {lesson.Title}{(lesson.Label.Length > 0 ? ", " + lesson.Label : string.Empty)}, {lesson.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}\n");
        if (analysis.Topic.Length > 0)
            note.Text($"Тема: {analysis.Topic}\n");

        if (analysis.Overview.Length > 0)
            note.Text($"\nЧто было на занятии\n{analysis.Overview}\n");

        Section(note, "Пройдено", analysis.Covered);
        if (analysis.Topics.Count > 0)
        {
            note.Text("\nТемы\n");
            foreach (var topic in analysis.Topics)
                note.Text($"{topic.Title}\n{(topic.Details.Length > 0 ? topic.Details + "\n" : string.Empty)}");
        }

        Section(note, "Формулировки", analysis.Formulations);
        Section(note, "Решения", analysis.Decisions);
        Section(note, "Задачи", [.. analysis.Tasks.Select(task => task.Due.Length > 0 ? $"{task.Title} (срок: {task.Due})" : task.Title)]);
        if (lesson.RecordUrl.Length > 0)
            note.Divider().Link("Запись занятия", lesson.RecordUrl);

        return note.ToString();
    }

    /// <summary>
    /// Собирает запрос на создание заметки, если первой заметки с резюме в SingularityApp ещё нет
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="analysis">Разбор</param>
    /// <param name="projectId">Проект SingularityApp или <see langword="null"/></param>
    /// <param name="offset">Часовой пояс начала дня</param>
    /// <returns>Запрос на создание заметки</returns>
    public static SingularityTaskRequest Build(Lesson lesson, LessonAnalysis analysis, string? projectId, TimeSpan offset) => new()
    {
        Title = $"Расшифровка: {lesson.Title}, {lesson.Date.ToString("dd.MM", CultureInfo.InvariantCulture)}{(lesson.Label.Length > 0 ? " " + lesson.Label : string.Empty)}",
        Note = Text(lesson, analysis),
        IsNote = true,
        Start = new DateTimeOffset(lesson.Date.ToDateTime(TimeOnly.MinValue), offset).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
        ExternalId = KEY_PREFIX + lesson.Id,
        ProjectId = projectId,
    };

    /// <summary>
    /// Добавляет раздел со списком, если в нём есть пункты
    /// </summary>
    /// <param name="note">Заметка</param>
    /// <param name="title">Заголовок раздела</param>
    /// <param name="items">Пункты</param>
    private static void Section(QuillDelta note, string title, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
            return;

        note.Text($"\n{title}\n");
        foreach (var item in items)
            note.Text($"• {item}\n");
    }
}
