namespace Steward.Lms.Models;

/// <summary>
/// Вид учебной работы в LMS
/// </summary>
public enum ActivityKindEnum
{
    /// <summary>
    /// Задание с ответом, который сдают преподавателю
    /// </summary>
    Assignment,

    /// <summary>
    /// Тест с автоматической оценкой
    /// </summary>
    Quiz,
}
