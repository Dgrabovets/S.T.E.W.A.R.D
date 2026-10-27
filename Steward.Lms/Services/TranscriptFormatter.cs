using System.Globalization;
using System.Text;
using Steward.Homework.Models;
using Steward.Lms.Models;

namespace Steward.Lms.Services;

/// <summary>
/// Складывает расшифровку занятия в один текстовый файл, который читает агент при подготовке заданий
/// </summary>
public static class TranscriptFormatter
{
    /// <summary>
    /// Собирает текст файла: заголовок занятия, резюме и реплики со временем
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="transcript">Расшифровка из MTS Link</param>
    /// <returns>Текст для <c>transcripts/&lt;id&gt;.txt</c></returns>
    public static string Format(Lesson lesson, MtsTranscript transcript)
    {
        var text = new StringBuilder();
        text.AppendLine($"Занятие: {lesson.Title}{(lesson.Label.Length > 0 ? ", " + lesson.Label : string.Empty)}");
        text.AppendLine($"Дата: {lesson.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}");
        text.AppendLine();

        if (transcript.Summary.Length > 0)
        {
            text.AppendLine("== Резюме ==");
            text.AppendLine(transcript.Summary.Trim());
            text.AppendLine();
        }

        text.AppendLine("== Расшифровка ==");
        foreach (var line in transcript.Lines)
            text.AppendLine($"[{(int)line.Offset.TotalHours:00}:{line.Offset.Minutes:00}:{line.Offset.Seconds:00}] {line.Speaker}: {line.Text}");

        return text.ToString();
    }
}
