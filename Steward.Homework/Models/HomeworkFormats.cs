namespace Steward.Homework.Models;

/// <summary>
/// Названия форматов для экрана
/// </summary>
public static class HomeworkFormats
{
    /// <summary>
    /// Формат, который берётся, если для занятия ничего не выбрано
    /// </summary>
    public const HomeworkFormatEnum DEFAULT = HomeworkFormatEnum.Word;

    /// <summary>
    /// Все форматы в порядке показа
    /// </summary>
    public static IReadOnlyList<HomeworkFormatEnum> All { get; } = Enum.GetValues<HomeworkFormatEnum>();

    /// <summary>
    /// Короткое название формата
    /// </summary>
    /// <param name="format">Формат</param>
    /// <returns>Название для кнопки</returns>
    public static string Title(HomeworkFormatEnum format) => format switch
    {
        HomeworkFormatEnum.Word => "Word",
        HomeworkFormatEnum.Presentation => "Презентация",
        HomeworkFormatEnum.Text => "Текст",
        HomeworkFormatEnum.Code => "Код",
        _ => "Не готовить",
    };
}