using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace UltimateParser.Common;

/// <summary>
/// Читает и записывает заметку Singularity: обычный текст, Markdown и дельту Quill
/// </summary>
public static class NoteText
{
    /// <summary>
    /// Видимый текст гиперссылки в описании задачи
    /// </summary>
    private const string LinkLabel = "Ссылка";

    /// <summary>
    /// Сериализатор дельты. Кириллица остаётся символами, а не кодами <c>\u</c>
    /// </summary>
    private static readonly JsonSerializerOptions DeltaJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    #region Reading

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
    /// Сообщает, описывают ли сохранённая и ожидаемая заметки один текст и одну ссылку
    /// </summary>
    /// <param name="stored">
    /// Заметка из API: обычный текст или дельта Quill
    /// </param>
    /// <param name="expected">
    /// Текст ячейки. Последняя строка с адресом <c>http</c> или <c>https</c> считается ссылкой
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если видимый текст и адрес ссылки совпадают
    /// </returns>
    public static bool Same(string? stored, string expected)
    {
        if (!TrySplitLink(expected, out var body, out var url))
            return ExtractLink(stored) is null
                && string.Equals(Plain(stored), expected, StringComparison.Ordinal);

        if (!IsOperationArray(stored))
            return false;

        var visible = body.Length == 0 ? LinkLabel : body + "\n" + LinkLabel;
        return string.Equals(Plain(stored), visible, StringComparison.Ordinal)
            && string.Equals(ExtractLink(stored), url, StringComparison.Ordinal);
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

    /// <summary>
    /// Возвращает адрес из атрибута <c>link</c> или из последней строки обычного текста
    /// </summary>
    /// <param name="note">
    /// Заметка из API
    /// </param>
    /// <returns>
    /// Адрес или <see langword="null"/>, если ссылки нет
    /// </returns>
    private static string? ExtractLink(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
            return null;

        var trimmed = note.Trim();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                JsonElement ops = default;
                var found = document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("ops", out ops);
                if (!found && document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    ops = document.RootElement;
                    found = true;
                }

                if (found && ops.ValueKind == JsonValueKind.Array)
                {
                    foreach (var operation in ops.EnumerateArray())
                    {
                        if (operation.ValueKind == JsonValueKind.Object
                            && operation.TryGetProperty("attributes", out var attributes)
                            && attributes.ValueKind == JsonValueKind.Object
                            && attributes.TryGetProperty("link", out var link)
                            && link.ValueKind == JsonValueKind.String)
                        {
                            return link.GetString();
                        }
                    }
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }

        return TrySplitLink(note, out _, out var url) ? url : null;
    }

    /// <summary>
    /// Сообщает, что заметка хранится массивом операций Quill, а не объектом <c>ops</c> и не обычным текстом
    /// </summary>
    /// <param name="note">
    /// Заметка из API
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если корень JSON — массив
    /// </returns>
    private static bool IsOperationArray(string? note)
    {
        if (string.IsNullOrWhiteSpace(note) || !note.TrimStart().StartsWith('['))
            return false;

        try
        {
            using var document = JsonDocument.Parse(note);
            return document.RootElement.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    #endregion Reading

    #region Writing

    /// <summary>
    /// Готовит заметку для API. Последняя строка-адрес становится словом <c>Ссылка</c> в массиве операций Quill
    /// </summary>
    /// <param name="note">
    /// Текст ячейки. Адрес, прикреплённый к ячейке, стоит последней строкой
    /// </param>
    /// <returns>
    /// Массив операций, если в заметке есть ссылка. Иначе исходный текст
    /// </returns>
    public static string Format(string note)
    {
        if (!TrySplitLink(note, out var body, out var url))
            return note;

        var ops = new List<object>();
        if (body.Length > 0)
            ops.Add(new { insert = body + "\n" });

        ops.Add(new { insert = LinkLabel, attributes = new { link = url } });
        ops.Add(new { insert = "\n" });
        return JsonSerializer.Serialize(ops, DeltaJson);
    }

    /// <summary>
    /// Отделяет последнюю строку, если это адрес <c>http</c> или <c>https</c>
    /// </summary>
    /// <param name="note">
    /// Текст заметки
    /// </param>
    /// <param name="body">
    /// Текст до адреса. Пустая строка, если адрес занимает всю заметку
    /// </param>
    /// <param name="url">
    /// Адрес последней строки, если метод вернул <see langword="true"/>
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если последняя непустая строка является адресом
    /// </returns>
    private static bool TrySplitLink(string note, out string body, out string url)
    {
        body = note;
        url = "";
        var normalized = note.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
        var lineBreak = normalized.LastIndexOf('\n');
        var last = lineBreak < 0 ? normalized : normalized[(lineBreak + 1)..];
        if (!last.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !last.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (last.Contains(' ') || last.Contains('\t'))
            return false;

        body = lineBreak < 0 ? "" : normalized[..lineBreak].TrimEnd('\n');
        url = last;
        return true;
    }

    #endregion Writing
}
