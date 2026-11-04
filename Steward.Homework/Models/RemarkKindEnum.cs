namespace Steward.Homework.Models;

/// <summary>
/// К чему относится замечание владельца
/// </summary>
public enum RemarkKindEnum
{
    /// <summary>
    /// Общее замечание без привязки к части работы
    /// </summary>
    General,

    /// <summary>
    /// Условие задания: как правильно понимать, что именно задано
    /// </summary>
    Condition,

    /// <summary>
    /// Формат и оформление работы
    /// </summary>
    Format,

    /// <summary>
    /// Содержание работы: полнота, глубина, точность
    /// </summary>
    Content,

    /// <summary>
    /// Стиль изложения
    /// </summary>
    Style,
}
