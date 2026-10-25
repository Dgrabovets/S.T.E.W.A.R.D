using System.Text;
using System.Text.Json;

namespace Steward.Mail.Tests;

/// <summary>
/// Читает заметку задачи, записанную дельтой Quill: видимый текст, ссылки и горизонтальные линии
/// </summary>
public static class NoteOps
{
    /// <summary>
    /// Склеивает видимый текст заметки
    /// </summary>
    /// <param name="note">Заметка: JSON-массив операций</param>
    /// <returns>Текст без разметки</returns>
    public static string Visible(string note)
    {
        var text = new StringBuilder();
        foreach (var operation in Operations(note))
        {
            if (operation.GetProperty("insert").ValueKind == JsonValueKind.String)
                text.Append(operation.GetProperty("insert").GetString());
        }

        return text.ToString();
    }

    /// <summary>
    /// Находит гиперссылки: подпись и адрес
    /// </summary>
    /// <param name="note">Заметка: JSON-массив операций</param>
    /// <returns>Пары «подпись и адрес» в порядке записи</returns>
    public static IReadOnlyList<(string Label, string Url)> Links(string note) =>
    [
        .. Operations(note)
            .Where(operation => operation.TryGetProperty("attributes", out var attributes) && attributes.TryGetProperty("link", out _))
            .Select(operation => (operation.GetProperty("insert").GetString()!, operation.GetProperty("attributes").GetProperty("link").GetString()!)),
    ];

    /// <summary>
    /// Считает горизонтальные линии
    /// </summary>
    /// <param name="note">Заметка: JSON-массив операций</param>
    /// <returns>Число вставок разделителя</returns>
    public static int Dividers(string note) =>
        Operations(note).Count(operation => operation.GetProperty("insert").ValueKind == JsonValueKind.Object && operation.GetProperty("insert").TryGetProperty("divider", out _));

    /// <summary>
    /// Разбирает заметку в операции
    /// </summary>
    /// <param name="note">Заметка: JSON-массив операций</param>
    /// <returns>Операции в порядке записи</returns>
    private static List<JsonElement> Operations(string note)
    {
        using var document = JsonDocument.Parse(note);

        return [.. document.RootElement.EnumerateArray().Select(operation => operation.Clone())];
    }
}