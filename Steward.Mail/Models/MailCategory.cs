namespace Steward.Mail.Models;

/// <summary>
/// Категория письма после разбора
/// </summary>
public enum MailCategory
{
    /// <summary>
    /// Отзыв или оценка преподавателя в LMS
    /// </summary>
    TeacherFeedback,

    /// <summary>
    /// Живое письмо от человека, на которое, скорее всего, нужно ответить
    /// </summary>
    PersonalReply,

    /// <summary>
    /// Прочее письмо: попадает только в сводку
    /// </summary>
    Info
}
