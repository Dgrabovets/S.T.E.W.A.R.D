namespace Steward.Homework.Models;

/// <summary>
/// Что выбрано в окне «Подготовить задание»: формат работы, дополнительное указание и материалы, которые нужно учесть
/// </summary>
/// <param name="Format">Формат работы</param>
/// <param name="Extra">Дополнительное указание агенту. Пусто, если не нужно</param>
/// <param name="GuideIds">Ключи методических указаний и образцов, которые нужно учесть. <see langword="null"/>: все включённые</param>
/// <param name="Redo">Работу делают заново: прежняя версия остаётся как образец того, что не понравилось, а в указании записаны замечания</param>
/// <param name="RemarkFiles">Файлы с замечаниями преподавателя (Word, презентация, PDF, текст, снимок) для переделки или <see langword="null"/></param>
public sealed record PrepareRequest(HomeworkFormatEnum Format, string Extra, IReadOnlySet<string>? GuideIds, bool Redo = false, IReadOnlyList<string>? RemarkFiles = null)
{
    /// <summary>
    /// Запрос без дополнительного указания: берутся все включённые материалы
    /// </summary>
    /// <param name="format">Формат работы</param>
    /// <returns>Запрос</returns>
    public static PrepareRequest Default(HomeworkFormatEnum format) => new(format, string.Empty, null);
}
