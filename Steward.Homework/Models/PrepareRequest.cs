namespace Steward.Homework.Models;

/// <summary>
/// Что выбрано в окне «Подготовить задание»: формат работы, дополнительное указание и материалы, которые нужно учесть
/// </summary>
/// <param name="Format">Формат работы</param>
/// <param name="Extra">Дополнительное указание агенту. Пусто, если не нужно</param>
/// <param name="GuideIds">Ключи методических указаний и образцов, которые нужно учесть. <see langword="null"/>: все включённые</param>
public sealed record PrepareRequest(HomeworkFormatEnum Format, string Extra, IReadOnlySet<string>? GuideIds)
{
    /// <summary>
    /// Запрос без дополнительного указания: берутся все включённые материалы
    /// </summary>
    /// <param name="format">Формат работы</param>
    /// <returns>Запрос</returns>
    public static PrepareRequest Default(HomeworkFormatEnum format) => new(format, string.Empty, null);
}
