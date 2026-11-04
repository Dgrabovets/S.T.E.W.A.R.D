using System.Globalization;
using Steward.Homework.Models;
using Steward.Lms.Models;
using Steward.Mail.Models;
using Steward.Schedule.Common.Helpers;

namespace Steward.Lms.Services;

/// <summary>
/// Собирает заметку SingularityApp с расшифровкой занятия: резюме встречи, а полный текст остаётся файлом на диске
/// </summary>
public static class TranscriptNote
{
    /// <summary>
    /// Префикс внешнего ключа заметок с расшифровками
    /// </summary>
    public const string KEY_PREFIX = "lms:transcript:";

    /// <summary>
    /// Самая длинная часть резюме в заметке в знаках: хватает на тему, краткое содержание и ключевые моменты, а заметка остаётся читаемой
    /// </summary>
    private const int SUMMARY_LIMIT = 6000;

    /// <summary>
    /// Собирает заметку
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="transcript">Расшифровка из MTS Link</param>
    /// <param name="transcriptPath">Путь к файлу с полным текстом на диске</param>
    /// <param name="projectId">Проект SingularityApp или <see langword="null"/>, тогда заметка идёт во входящие</param>
    /// <param name="offset">Часовой пояс для времени начала</param>
    /// <returns>Запрос на создание заметки</returns>
    public static SingularityTaskRequest Build(Lesson lesson, MtsTranscript transcript, string transcriptPath, string? projectId, TimeSpan offset)
    {
        var summary = transcript.Summary.Length > SUMMARY_LIMIT ? transcript.Summary[..SUMMARY_LIMIT].TrimEnd() + "…" : transcript.Summary;
        var note = new QuillDelta().Text($"Занятие: {lesson.Title}{(lesson.Label.Length > 0 ? ", " + lesson.Label : string.Empty)}, {lesson.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}\n");
        note.Text(summary.Length > 0 ? $"\n{summary}\n" : "\nРезюме встречи сервис не подготовил.\n");
        note.Text($"\nРеплик в полной расшифровке: {transcript.Lines.Count}. Файл: {transcriptPath}\n");
        if (lesson.RecordUrl.Length > 0)
            note.Divider().Link("Запись занятия", lesson.RecordUrl);

        return new SingularityTaskRequest
        {
            Title = $"Расшифровка: {lesson.Title}, {lesson.Date.ToString("dd.MM", CultureInfo.InvariantCulture)}{(lesson.Label.Length > 0 ? " " + lesson.Label : string.Empty)}",
            Note = note.ToString(),
            IsNote = true,
            Start = new DateTimeOffset(lesson.Date.ToDateTime(TimeOnly.MinValue), offset).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            ExternalId = KEY_PREFIX + lesson.Id,
            ProjectId = projectId,
        };
    }
}
