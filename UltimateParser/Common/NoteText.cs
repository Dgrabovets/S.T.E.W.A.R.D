using System.Text;
using System.Text.Json;

namespace UltimateParser.Common;

/// <summary>
/// Читает заметку Singularity и из обычного текста, и из дельты Quill
/// </summary>
public static class NoteText
{
    /// <summary>
    /// Возвращает видимый текст заметки
    /// </summary>
    /// <param name="note">
    /// Обычный текст, объект дельты Quill или массив операций. <see langword="null"/> считается пустой заметкой
    /// </param>
    /// <returns>
    /// Обычный текст. Завершающий перевод строки, который добавляет Quill, отбрасывается. Нечитаемый JSON возвращается как есть
    /// </returns>
    public static string Plain(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
            return "";

        var trimmed = note.Trim();
        if (!trimmed.StartsWith('{') && !trimmed.StartsWith('['))
            return note;

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var text = new StringBuilder();
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("ops", out var ops))
            {
                AppendInserts(ops, text);
            }
            else if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                AppendInserts(document.RootElement, text);
            }
            else
            {
                return note;
            }

            return text.ToString().TrimEnd('\n');
        }
        catch (JsonException)
        {
            return note;
        }
    }

    /// <summary>
    /// Дописывает строковые операции <c>insert</c> из массива операций Quill
    /// </summary>
    /// <param name="ops">
    /// JSON-массив операций. Значения другого вида пропускаются
    /// </param>
    /// <param name="text">
    /// Буфер, в который попадают вставленные строки
    /// </param>
    private static void AppendInserts(JsonElement ops, StringBuilder text)
    {
        if (ops.ValueKind != JsonValueKind.Array)
            return;

        foreach (var operation in ops.EnumerateArray())
        {
            if (operation.ValueKind == JsonValueKind.Object
                && operation.TryGetProperty("insert", out var insert)
                && insert.ValueKind == JsonValueKind.String)
            {
                text.Append(insert.GetString());
            }
        }
    }
}
