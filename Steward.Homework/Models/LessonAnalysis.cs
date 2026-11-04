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
                [.. Items(root, "tasks").Select(item => item.ValueKind == JsonValueKind.String ? new AnalysisTask(item.GetString() ?? string.Empty, string.Empty) : new AnalysisTask(Text(item, "title"), Text(item, "due"), Text(item, "quote"), Strings(item, "before"), Strings(item, "said"), Strings(item, "after"))).Where(item => item.Title.Length > 0)]);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Дописывает к каждой задаче место в расшифровке, где она сказана: реплики до слов преподавателя, сами слова и реплики после. Строки берутся из текста расшифровки, а не из ответа агента, поэтому они дословные. Если цитаты в расшифровке нет, у задачи остаётся только цитата агента
    /// </summary>
    /// <param name="transcript">Текст файла расшифровки</param>
    /// <param name="around">Сколько реплик до и после слов преподавателя сохранить</param>
    /// <returns>Разбор с отрывками</returns>
    public LessonAnalysis WithExcerpts(string transcript, int around = 3)
    {
        var bodyAt = transcript.IndexOf("== Расшифровка ==", StringComparison.Ordinal);
        var body = bodyAt >= 0 ? transcript[(bodyAt + "== Расшифровка ==".Length)..] : transcript;

        return this with
        {
            Tasks = [.. Tasks.Select(task =>
            {
                var lines = Services.EvidenceCheck.Excerpt(body, task.Quote, around).ToList();
                var first = lines.FindIndex(line => line.Hit);
                if (first < 0)
                    return task with { Before = [], Said = [], After = [] };

                var last = lines.FindLastIndex(line => line.Hit);

                return task with
                {
                    Before = [.. lines.Take(first).Select(line => line.Text)],
                    Said = [.. lines.Skip(first).Take(last - first + 1).Select(line => line.Text)],
                    After = [.. lines.Skip(last + 1).Select(line => line.Text)],
                };
            })],
        };
    }

    /// <summary>
    /// Записывает разбор в JSON того же вида, который читает <see cref="Parse"/>
    /// </summary>
    /// <returns>Текст JSON</returns>
    public string ToJson() => JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["topic"] = Topic,
        ["overview"] = Overview,
        ["covered"] = Covered,
        ["topics"] = Topics.Select(topic => new { title = topic.Title, details = topic.Details }),
        ["formulations"] = Formulations,
        ["decisions"] = Decisions,
        ["tasks"] = Tasks.Select(task => new { title = task.Title, due = task.Due, quote = task.Quote, before = task.Before ?? [], said = task.Said ?? [], after = task.After ?? [] }),
    }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

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
/// <param name="Quote">Слова преподавателя, которыми дано задание, дословно. Пусто у разборов, записанных до появления отрывков</param>
/// <param name="Before">Реплики перед заданием: что происходило на занятии. <see langword="null"/> или пусто, если цитату не нашли в расшифровке</param>
/// <param name="Said">Реплики, в которых сказано задание</param>
/// <param name="After">Реплики после задания</param>
public sealed record AnalysisTask(string Title, string Due, string Quote = "", IReadOnlyList<string>? Before = null, IReadOnlyList<string>? Said = null, IReadOnlyList<string>? After = null)
{
    /// <summary>
    /// Место в расшифровке найдено: есть реплики, в которых сказано задание
    /// </summary>
    public bool HasExcerpt => Said is { Count: > 0 };
}
