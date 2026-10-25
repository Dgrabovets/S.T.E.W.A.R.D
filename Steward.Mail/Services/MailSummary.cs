using System.Text;
using Steward.Mail.Models;

namespace Steward.Mail.Services;

/// <summary>
/// Собирает текст уведомления и строку статуса
/// </summary>
public static class MailSummary
{
    /// <summary>
    /// Собирает текст: сколько писем требуют ответа, сколько отзывов и сколько прочих
    /// </summary>
    /// <param name="feedback">Письма с отзывами преподавателей</param>
    /// <param name="replies">Письма, на которые нужно ответить</param>
    /// <param name="info">Прочие письма</param>
    /// <returns>Строки по категориям или «Новых важных писем нет», если писем не было</returns>
    public static string Build(IReadOnlyList<MailItem> feedback, IReadOnlyList<MailItem> replies, IReadOnlyList<MailItem> info)
    {
        if (feedback.Count + replies.Count + info.Count == 0)
            return "Новых важных писем нет";

        var builder = new StringBuilder();
        if (replies.Count > 0)
            builder.AppendLine($"Ответить: {replies.Count} ({string.Join(", ", replies.Select(reply => MailText.ShortName(reply.FromName)).Distinct())})");

        if (feedback.Count > 0)
            builder.AppendLine($"Отзывы и оценки: {feedback.Count}");

        if (info.Count > 0)
            builder.AppendLine($"Прочее: {info.Count}");

        return builder.ToString().TrimEnd();
    }
}