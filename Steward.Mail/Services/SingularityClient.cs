using Steward.Mail.Common;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Steward.Mail.Services;

/// <summary>
/// Создание задач через REST API SingularityApp
/// </summary>
public sealed class SingularityClient : ISingularityClient, ISingularityNotes, IDisposable
{
    /// <summary>
    /// Клиент, у которого базовый адрес — API SingularityApp, а в заголовке токен
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Сколько задач просить за один запрос
    /// </summary>
    private const int PAGE_SIZE = 1000;

    /// <summary>
    /// Создаёт клиент, авторизованный токеном из настроек
    /// </summary>
    /// <param name="settings">Адрес API и токен доступа</param>
    public SingularityClient(SingularitySettings settings)
        : this(CreateHttp(settings))
    {
    }

    /// <summary>
    /// Создаёт клиент на готовом HTTP-клиенте: его адрес и заголовок задаёт вызывающий
    /// </summary>
    /// <param name="http">HTTP-клиент, которым владеет создаваемый объект</param>
    public SingularityClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Создаёт задачу запросом POST /v2/task
    /// </summary>
    /// <param name="request">Поля новой задачи</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда SingularityApp принимает запрос</returns>
    /// <exception cref="HttpRequestException">
    /// SingularityApp ответил ошибкой. В тексте код ответа и тело
    /// </exception>
    public async Task CreateTaskAsync(SingularityTaskRequest request, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync("v2/task", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"SingularityApp ответил {(int)response.StatusCode}: {body}");
        }
    }

    /// <summary>
    /// Читает внешние ключи задач, которые начинаются позже заданного момента. Удалённые, выполненные и архивные тоже учитываются: если вы удалили задачу из письма, она не вернётся
    /// </summary>
    /// <param name="startsAfter">Нижняя граница начала задач</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Внешние ключи без повторов. Задачи без ключа пропускаются</returns>
    /// <exception cref="HttpRequestException">
    /// SingularityApp ответил ошибкой. В тексте код ответа и тело
    /// </exception>
    public async Task<IReadOnlySet<string>> ListExternalIdsAsync(DateTimeOffset startsAfter, CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        // Фильтр принимает только время в UTC с буквой Z: смещение вида +01:00 даёт ответ 400 «Invalid date value». Создание задачи со смещением работает
        var after = Uri.EscapeDataString(startsAfter.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));

        for (var offset = 0; ; offset += PAGE_SIZE)
        {
            using var response = await _http.GetAsync($"v2/task?maxCount={PAGE_SIZE}&offset={offset}&includeRemoved=true&includeArchived=true&start.gt={after}", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"SingularityApp ответил {(int)response.StatusCode}: {body}");

            using var document = JsonDocument.Parse(body);
            var tasks = document.RootElement.GetProperty("tasks");
            foreach (var task in tasks.EnumerateArray())
            {
                if (task.TryGetProperty("externalId", out var key) && key.GetString() is { Length: > 0 } value)
                    keys.Add(value);
            }

            if (tasks.GetArrayLength() < PAGE_SIZE)
                return keys;
        }
    }

    /// <summary>
    /// Читает задачи с внешним ключом, которые начинаются позже заданного момента: номер задачи, ключ и отметка выполнения. Удалённые и архивные читаются тоже, с пометкой: если задачу удалили, заново её заводить не нужно
    /// </summary>
    /// <param name="startsAfter">Нижняя граница начала задач</param>
    /// <param name="keyPrefix">Берутся только задачи, ключ которых начинается с этого текста</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задачи с ключом</returns>
    /// <exception cref="HttpRequestException">
    /// SingularityApp ответил ошибкой. В тексте код ответа и тело
    /// </exception>
    public async Task<IReadOnlyList<SingularityTaskInfo>> ListTasksAsync(DateTimeOffset startsAfter, string keyPrefix, CancellationToken cancellationToken)
    {
        var found = new List<SingularityTaskInfo>();
        var after = Uri.EscapeDataString(startsAfter.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));

        for (var offset = 0; ; offset += PAGE_SIZE)
        {
            using var response = await _http.GetAsync($"v2/task?maxCount={PAGE_SIZE}&offset={offset}&includeRemoved=true&includeArchived=true&start.gt={after}", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"SingularityApp ответил {(int)response.StatusCode}: {body}");

            using var document = JsonDocument.Parse(body);
            var tasks = document.RootElement.GetProperty("tasks");
            foreach (var task in tasks.EnumerateArray())
            {
                if (task.TryGetProperty("externalId", out var key) && key.GetString() is { Length: > 0 } value && value.StartsWith(keyPrefix, StringComparison.Ordinal))
                {
                    found.Add(new SingularityTaskInfo(
                        task.GetProperty("id").GetString() ?? string.Empty,
                        value,
                        task.TryGetProperty("checked", out var state) && state.ValueKind == JsonValueKind.Number ? state.GetInt32() : 0,
                        task.TryGetProperty("removed", out var removed) && removed.ValueKind == JsonValueKind.True,
                        task.TryGetProperty("archive", out var archived) && archived.ValueKind == JsonValueKind.True));
                }
            }

            if (tasks.GetArrayLength() < PAGE_SIZE)
                return found;
        }
    }

    /// <summary>
    /// Отмечает задачу выполненной запросом PATCH /v2/task/{id}
    /// </summary>
    /// <param name="taskId">Номер задачи в SingularityApp</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда SingularityApp принимает запрос</returns>
    /// <exception cref="HttpRequestException">
    /// SingularityApp ответил ошибкой. В тексте код ответа и тело
    /// </exception>
    public async Task CompleteTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"v2/task/{Uri.EscapeDataString(taskId)}") { Content = JsonContent.Create(new { @checked = 1 }) };
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"SingularityApp ответил {(int)response.StatusCode}: {body}");
        }
    }

    /// <summary>
    /// Дописывает текст в конец заметки задачи: читает задачу, склеивает заметки и отправляет PATCH /v2/task/{id}. Написанное пользователем не теряется
    /// </summary>
    /// <param name="taskId">Номер задачи в SingularityApp</param>
    /// <param name="note">Дописываемый текст в формате Quill Delta</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда SingularityApp принимает запрос</returns>
    /// <exception cref="HttpRequestException">
    /// SingularityApp ответил ошибкой. В тексте код ответа и тело
    /// </exception>
    public async Task UpdateNoteAsync(string taskId, string note, CancellationToken cancellationToken)
    {
        string current = string.Empty;
        using (var read = await _http.GetAsync($"v2/task/{Uri.EscapeDataString(taskId)}", cancellationToken))
        {
            var text = await read.Content.ReadAsStringAsync(cancellationToken);
            if (!read.IsSuccessStatusCode)
                throw new HttpRequestException($"SingularityApp ответил {(int)read.StatusCode}: {text}");

            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("note", out var existing) && existing.ValueKind == JsonValueKind.String)
                current = existing.GetString() ?? string.Empty;
        }

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"v2/task/{Uri.EscapeDataString(taskId)}") { Content = JsonContent.Create(new { note = MergeNote(current, note) }) };
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"SingularityApp ответил {(int)response.StatusCode}: {body}");
        }
    }

    /// <summary>
    /// Склеивает две заметки в формате Quill Delta: старая остаётся как есть, новая идёт после пустой строки. Если старая пуста или не читается, берётся новая. Если все части новой уже есть в старой, заметка не меняется
    /// </summary>
    /// <param name="current">Заметка задачи сейчас</param>
    /// <param name="addition">Дописываемая заметка</param>
    /// <returns>Итоговая заметка в формате Quill Delta</returns>
    public static string MergeNote(string current, string addition)
    {
        if (string.IsNullOrWhiteSpace(current))
            return addition;

        try
        {
            var old = System.Text.Json.Nodes.JsonNode.Parse(current) as System.Text.Json.Nodes.JsonArray;
            var added = System.Text.Json.Nodes.JsonNode.Parse(addition) as System.Text.Json.Nodes.JsonArray;
            if (old is null || added is null)
                return addition;

            if (added.All(op => old.Any(existing => System.Text.Json.Nodes.JsonNode.DeepEquals(existing, op))))
                return current;

            var merged = new System.Text.Json.Nodes.JsonArray();
            foreach (var op in old)
                merged.Add(op?.DeepClone());

            merged.Add(new System.Text.Json.Nodes.JsonObject { ["insert"] = "\n" });
            foreach (var op in added)
                merged.Add(op?.DeepClone());

            return merged.ToJsonString(new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }
        catch (JsonException)
        {
            return addition;
        }
    }

    /// <summary>
    /// Освобождает HTTP-клиент
    /// </summary>
    public void Dispose() => _http.Dispose();

    /// <summary>
    /// Собирает HTTP-клиент с базовым адресом API и токеном в заголовке
    /// </summary>
    /// <param name="settings">Адрес API и токен доступа</param>
    /// <returns>Клиент, готовый к запросам</returns>
    private static HttpClient CreateHttp(SingularitySettings settings)
    {
        var http = new HttpClient { BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);

        return http;
    }
}