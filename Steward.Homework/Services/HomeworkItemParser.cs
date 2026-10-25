using System.Globalization;
using System.Text.Json;
using Steward.Homework.Models;

namespace Steward.Homework.Services;

/// <summary>
/// Читает JSON, который записал агент. Мусор и неполные элементы пропускаются, а не роняют разбор
/// </summary>
public static class HomeworkItemParser
{
    /// <summary>
    /// Разбирает ответ агента
    /// </summary>
    /// <param name="json">Текст файла: массив заданий, возможно в ограде из тройных кавычек</param>
    /// <returns>Найденные задания. Пустой список, если разобрать нечего</returns>
    public static IReadOnlyList<HomeworkItem> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(StripFence(json));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            return [.. document.RootElement.EnumerateArray().Select(ReadItem).OfType<HomeworkItem>()];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Убирает ограду <c>```json … ```</c>, в которую агенты любят заворачивать ответ
    /// </summary>
    /// <param name="text">Текст файла</param>
    /// <returns>Текст без ограды</returns>
    private static string StripFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;

        var start = trimmed.IndexOf('\n');
        var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);

        return start >= 0 && end > start ? trimmed[(start + 1)..end].Trim() : trimmed;
    }

    /// <summary>
    /// Читает один элемент массива
    /// </summary>
    /// <param name="element">Элемент</param>
    /// <returns>Задание или <see langword="null"/>, если это не объект или в нём нет названия</returns>
    private static HomeworkItem? ReadItem(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        var title = Text(element, "title");
        if (title.Length == 0)
            return null;

        return new HomeworkItem(
            title,
            DateOnly.TryParseExact(Text(element, "due"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var due) ? due : null,
            Text(element, "requirements"),
            Text(element, "link"));
    }

    /// <summary>
    /// Читает строковое поле
    /// </summary>
    /// <param name="element">Объект</param>
    /// <param name="name">Имя поля</param>
    /// <returns>Текст без пробелов по краям или пустая строка, если поля нет или в нём не строка</returns>
    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : string.Empty;
}