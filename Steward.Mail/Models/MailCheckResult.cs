namespace Steward.Mail.Models;

/// <summary>
/// Итог одной проверки почты
/// </summary>
/// <param name="Feedback">Письма с отзывами преподавателей</param>
/// <param name="Replies">Письма, на которые нужно ответить</param>
/// <param name="Info">Прочие письма</param>
/// <param name="Summary">Сводка по категориям</param>
public sealed record MailCheckResult(
    IReadOnlyList<MailItem> Feedback,
    IReadOnlyList<MailItem> Replies,
    IReadOnlyList<MailItem> Info,
    string Summary);