using System.Globalization;
using System.Text;

namespace Steward.Preview;

/// <summary>
/// Конспект к практике: что повторить и что знать по занятию. Собирается из разбора занятия без обращения к агенту
/// </summary>
public static class PracticeBrief
{
    /// <summary>
    /// Сколько формулировок берётся в конспект: самые важные идут в разборе первыми
    /// </summary>
    private const int FORMULATIONS_LIMIT = 6;

    /// <summary>
    /// Собирает текст конспекта
    /// </summary>
    /// <param name="subject">Предмет</param>
    /// <param name="date">Дата занятия</param>
    /// <param name="reading">Разбор занятия</param>
    /// <returns>Текст для буфера обмена или пустая строка, если подробного разбора ещё нет</returns>
    public static string Build(string subject, string date, TranscriptText reading)
    {
        if (reading.Topics.Count == 0 && reading.Formulations.Count == 0 && reading.Tasks.Count == 0)
            return string.Empty;

        var text = new StringBuilder();
        text.Append("К практике: ").Append(subject).Append(", ").AppendLine(date);
        if (reading.Topic.Length > 0)
            text.Append("Тема: ").AppendLine(reading.Topic);

        if (reading.Topics.Count > 0)
        {
            text.AppendLine().AppendLine("Что повторить:");
            foreach (var topic in reading.Topics)
                text.Append("- ").Append(topic.Title).Append(topic.Details.Length > 0 ? ": " + topic.Details : string.Empty).AppendLine();
        }

        if (reading.Formulations.Count > 0)
        {
            text.AppendLine().AppendLine("Формулировки преподавателя:");
            foreach (var formulation in reading.Formulations.Take(FORMULATIONS_LIMIT))
                text.Append("- ").AppendLine(formulation);
        }

        if (reading.Decisions.Count > 0)
        {
            text.AppendLine().AppendLine("Что решили:");
            foreach (var decision in reading.Decisions)
                text.Append("- ").AppendLine(decision);
        }

        if (reading.Tasks.Count > 0)
        {
            text.AppendLine().AppendLine("Что сделать до следующего раза:");
            foreach (var task in reading.Tasks)
                text.Append("- ").AppendLine(task);
        }

        return text.ToString().Trim().ToString(CultureInfo.InvariantCulture);
    }
}
