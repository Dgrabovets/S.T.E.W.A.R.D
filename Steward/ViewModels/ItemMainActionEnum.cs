namespace Steward.ViewModels;

/// <summary>
/// Что делает главная кнопка карточки задания
/// </summary>
public enum ItemMainActionEnum
{
    /// <summary>
    /// Кнопки нет: например, задание сдано по данным LMS
    /// </summary>
    None,

    /// <summary>
    /// «Посмотреть работу»: открыть готовый файл
    /// </summary>
    ViewWork,

    /// <summary>
    /// «Отметить сданным»
    /// </summary>
    MarkSubmitted,

    /// <summary>
    /// «Вернуть в работу»: снять свою отметку «сдано»
    /// </summary>
    ReturnToWork,
}
