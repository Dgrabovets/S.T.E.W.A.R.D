using System.Text.Json;

namespace Steward.Homework.Models;

/// <summary>
/// Подробный разбор занятия, который агент записывает по расшифровке: тема, что было, что пройдено, формулировки, решения и задачи. Полный текст по ролям сюда не попадает
/// </summary>
/// <param name="Topic">Тема встречи</param>
/// <param name="Overview">Что было на занятии, связным текстом</param>
/// <param name="Covered">Что пройдено: короткие пункты</param>
/// <param name="Topics">Разобранные темы с подробностями</param>
/// <param name="Formulations">Важные формулировки и определения, которые прозвучали</param>
/// <param name="Decisions">Принятые решения</param>
/// <param name="Tasks">Задачи, которые прозвучали на занятии</param>
public sealed record LessonAnalysis(
    string Topic,
    string Overview,
    IReadOnlyList<string> Covered,
    IReadOnlyList<AnalysisTopic> Topics,
    IReadOnlyList<string> Formulations,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<AnalysisTask> Tasks)
{
    /// <summary>
    /// Разбирает JSON, который записал агент. Лишние ключи игнорируются, недостающие остаются пустыми, обёртка из тройных кавычек снимается
    /// </summary>
    /// <param name="json">Содержимое файла</param>
    /// <returns>Разбор или <see langword="null"/>, если это не JSON-объект</returns>
    public static LessonAnalysis? Parse(string json)
    {
        var text = json.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var start = text.IndexOf('\n', StringComparison.Ordinal);
            var end = text.LastIndexOf("```", StringComparison.Ordinal);
            if (start >= 0 && end > start)
                text = text[(start + 1)..end].Trim();
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            return new LessonAnalysis(
                Text(root, "topic"),
                Text(root, "overview"),
                Strings(root, "covered"),
                [.. Items(root, "topics").Select(item => new AnalysisTopic(Text(item, "title"), Text(item, "details"))).Where(item => item.Title.Length > 0)],
                Strings(root, "formulations"),
                Strings(root, "decisions"),
                [.. Items(root, "tasks").Select(item => item.ValueKind == JsonValueKind.String ? new AnalysisTask(item.GetString() ?? string.Empty, string.Empty) : new AnalysisTask(Text(item, "title"), Text(item, "due"))).Where(item => item.Title.Length > 0)]);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Читает строку свойства
    /// </summary>
    /// <param name="element">Объект</param>
    /// <param name="name">Имя свойства</param>
    /// <returns>Строка или пустая, если свойства нет</returns>
    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? (value.GetString() ?? string.Empty).Trim() : string.Empty;

    /// <summary>
    /// Читает элементы массива-свойства
    /// </summary>
    /// <param name="element">Объект</param>
    /// <param name="name">Имя свойства</param>
    /// <returns>Элементы или пустой список</returns>
    private static IEnumerable<JsonElement> Items(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];

    /// <summary>
    /// Читает массив строк
    /// </summary>
    /// <param name="element">Объект</param>
    /// <param name="name">Имя свойства</param>
    /// <returns>Непустые строки</returns>
    private static IReadOnlyList<string> Strings(JsonElement element, string name) =>
        [.. Items(element, name).Where(item => item.ValueKind == JsonValueKind.String).Select(item => (item.GetString() ?? string.Empty).Trim()).Where(item => item.Length > 0)];
}

/// <summary>
/// Тема занятия с подробностями
/// </summary>
/// <param name="Title">Название темы</param>
/// <param name="Details">Что именно было сказано по теме</param>
public sealed record AnalysisTopic(string Title, string Details);

/// <summary>
/// Задача, которая прозвучала на занятии
/// </summary>
/// <param name="Title">Что нужно сделать</param>
/// <param name="Due">Срок словами или датой. Пусто, если не назван</param>
public sealed record AnalysisTask(string Title, string Due);
