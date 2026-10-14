using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UltimateParser.Common;
using UltimateParser.Common.Models;
using UltimateParser.Singularity.Dtos;
using UltimateParser.Singularity.Interfaces;
using UltimateParser.Singularity.Models;

namespace UltimateParser.Singularity.Implementations;

/// <summary>
/// HTTP-клиент REST API Singularity версии 2
/// </summary>
public sealed class SingularityClient : ISingularityClient
{
    /// <summary>
    /// Максимальный размер страницы, который принимают методы списка
    /// </summary>
    private const int PageSize = 1000;

    /// <summary>
    /// Сериализатор, который пишет JSON в camelCase и пропускает null
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Клиент, у которого базовый адрес — API Singularity
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Даёт токен доступа
    /// </summary>
    private readonly SyncOptions _options;

    /// <summary>
    /// Создаёт клиент, авторизованный токеном из <paramref name="options"/>
    /// </summary>
    /// <param name="http">
    /// Клиент, у которого базовый адрес — API Singularity
    /// </param>
    /// <param name="options">
    /// Даёт токен доступа
    /// </param>
    public SingularityClient(HttpClient http, SyncOptions options)
    {
        _http = http;
        _options = options;
    }

    #region Projects

    /// <summary>
    /// Ищет активный проект по точному названию или создаёт его
    /// </summary>
    /// <param name="title">
    /// Название проекта
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Идентификатор проекта
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Singularity не вернула идентификатор созданного проекта
    /// </exception>
    public async Task<string> GetOrCreateProjectIdAsync(string title, CancellationToken cancellationToken)
    {
        var existing = await FindProjectIdAsync(title, cancellationToken);
        if (existing is not null)
            return existing;

        var created = await SendAsync<ProjectCreateResponse>(
            HttpMethod.Post,
            "/v2/project",
            new { title },
            cancellationToken);

        if (created.Project?.Id is not { Length: > 0 } projectId)
            throw new InvalidOperationException($"Singularity did not return an id for project '{title}'.");

        return projectId;
    }

    /// <summary>
    /// Ищет активный проект по точному названию и не создаёт его
    /// </summary>
    /// <param name="title">
    /// Название проекта
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Идентификатор проекта или <see langword="null"/>, если проекта нет
    /// </returns>
    public async Task<string?> FindProjectIdAsync(string title, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (true)
        {
            var page = await SendAsync<ProjectListResponse>(
                HttpMethod.Get,
                $"/v2/project?maxCount={PageSize}&offset={offset}&includeArchived=false&includeRemoved=false",
                null,
                cancellationToken);

            var projects = page.Projects ?? [];
            var match = projects.FirstOrDefault(project =>
                string.Equals(project.Title?.Trim(), title, StringComparison.Ordinal));
            if (match?.Id is { Length: > 0 } id)
                return id;

            if (projects.Count < PageSize)
                return null;

            offset += projects.Count;
        }
    }

    #endregion Projects

    #region Tasks

    /// <summary>
    /// Загружает все задачи проекта, включая архивные
    /// </summary>
    /// <param name="projectId">
    /// Идентификатор проекта
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Сопоставленные задачи. Ответы без идентификатора отбрасываются
    /// </returns>
    public async Task<IReadOnlyList<SingularityTask>> ListProjectTasksAsync(
        string projectId,
        CancellationToken cancellationToken)
    {
        var tasks = new List<SingularityTask>();
        var offset = 0;
        while (true)
        {
            var page = await SendAsync<TaskListResponse>(
                HttpMethod.Get,
                $"/v2/task?projectId={Uri.EscapeDataString(projectId)}&maxCount={PageSize}&offset={offset}&includeArchived=true&includeRemoved=false&includeAllRecurrenceInstances=true",
                null,
                cancellationToken);

            var batch = page.Tasks ?? [];
            tasks.AddRange(batch.Where(task => !string.IsNullOrWhiteSpace(task.Id)).Select(Map));
            if (batch.Count < PageSize)
                break;

            offset += batch.Count;
        }

        return tasks;
    }

    /// <summary>
    /// Создаёт задачу со временем и сохраняет внешний идентификатор занятия
    /// </summary>
    /// <param name="projectId">
    /// Проект назначения
    /// </param>
    /// <param name="lesson">
    /// Занятие, которое нужно создать
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Идентификатор созданной задачи
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Singularity не вернула идентификатор созданной задачи
    /// </exception>
    public async Task<string> CreateTaskAsync(string projectId, Lesson lesson, CancellationToken cancellationToken)
    {
        var created = await SendAsync<TaskResponse>(
            HttpMethod.Post,
            "/v2/task",
            WriteBody(projectId, lesson, includeExternalId: true),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(created.Id))
            throw new InvalidOperationException("Singularity did not return a task id.");

        return created.Id;
    }

    /// <summary>
    /// Меняет поля, которыми владеет расписание
    /// </summary>
    /// <param name="taskId">
    /// Идентификатор задачи
    /// </param>
    /// <param name="projectId">
    /// Проект, в котором задача должна остаться
    /// </param>
    /// <param name="lesson">
    /// Желаемые значения
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Задача, которая завершается, когда API принимает тело
    /// </returns>
    public Task UpdateTaskAsync(
        string taskId,
        string projectId,
        Lesson lesson,
        CancellationToken cancellationToken) =>
        SendAsync<TaskResponse>(
            HttpMethod.Patch,
            $"/v2/task/{Uri.EscapeDataString(taskId)}",
            WriteBody(projectId, lesson, includeExternalId: false),
            cancellationToken);

    /// <summary>
    /// Архивирует задачу датой <paramref name="journalDate"/>
    /// </summary>
    /// <param name="taskId">
    /// Идентификатор задачи
    /// </param>
    /// <param name="journalDate">
    /// Дата в виде <c>yyyy-MM-dd</c>
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Задача, которая завершается, когда API архивирует задачу
    /// </returns>
    public Task ArchiveTaskAsync(string taskId, DateOnly journalDate, CancellationToken cancellationToken) =>
        SendAsync<TaskResponse>(
            HttpMethod.Post,
            $"/v2/task/{Uri.EscapeDataString(taskId)}/archive",
            new { journalDate = journalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
            cancellationToken);

    /// <summary>
    /// Ставит у задачи отметку выполнения
    /// </summary>
    /// <param name="taskId">
    /// Идентификатор задачи
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Задача, которая завершается, когда API принимает отметку
    /// </returns>
    public Task CompleteTaskAsync(string taskId, CancellationToken cancellationToken) =>
        SendAsync<TaskResponse>(
            HttpMethod.Patch,
            $"/v2/task/{Uri.EscapeDataString(taskId)}",
            new { @checked = 1 },
            cancellationToken);

    /// <summary>
    /// Возвращает задачу из архива
    /// </summary>
    /// <param name="taskId">
    /// Идентификатор задачи
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Задача, которая завершается, когда API возвращает задачу из архива
    /// </returns>
    public Task UnarchiveTaskAsync(string taskId, CancellationToken cancellationToken) =>
        SendAsync<TaskResponse>(
            HttpMethod.Post,
            $"/v2/task/{Uri.EscapeDataString(taskId)}/unarchive",
            null,
            cancellationToken);

    #endregion Tasks

    #region Comparison

    /// <summary>
    /// Сообщает, отличается ли сохранённая задача от занятия названием, заметкой, временем или длительностью
    /// </summary>
    /// <param name="task">
    /// Задача, которая сейчас хранится в Singularity
    /// </param>
    /// <param name="lesson">
    /// Занятие, разобранное из таблицы
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если нужно частичное обновление
    /// </returns>
    internal static bool NeedsUpdate(SingularityTask task, Lesson lesson) =>
        !string.Equals(task.Title, lesson.Title, StringComparison.Ordinal)
        || !string.Equals(NoteText.Plain(task.Note), lesson.Note, StringComparison.Ordinal)
        || !task.UseTime
        || task.TimeLength != lesson.TimeLengthMinutes
        || !SameStart(task.Start, lesson.Start);

    /// <summary>
    /// Форматирует момент начала так, как в примерах API, с числовым смещением
    /// </summary>
    /// <param name="start">
    /// Местное начало занятия
    /// </param>
    /// <returns>
    /// Значение вроде <c>2026-10-01T17:20:00+03:00</c>
    /// </returns>
    public static string FormatStart(DateTimeOffset start) =>
        start.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    /// <summary>
    /// Собирает JSON-тело, общее для создания и обновления
    /// </summary>
    /// <param name="projectId">
    /// Проект назначения
    /// </param>
    /// <param name="lesson">
    /// Значения занятия
    /// </param>
    /// <param name="includeExternalId">
    /// <see langword="true"/> при создании, чтобы сохранить устойчивый ключ
    /// </param>
    /// <returns>
    /// Анонимное тело. Null во внешнем идентификаторе сериализатор пропускает
    /// </returns>
    private static object WriteBody(string projectId, Lesson lesson, bool includeExternalId) => new
    {
        title = lesson.Title,
        note = lesson.Note,
        projectId,
        start = FormatStart(lesson.Start),
        useTime = true,
        timeLength = lesson.TimeLengthMinutes,
        externalId = includeExternalId ? lesson.ExternalId : null,
    };

    /// <summary>
    /// Сравнивает сохранённую строку начала с ожидаемым моментом, не обращая внимания на формат
    /// </summary>
    /// <param name="stored">
    /// Значение начала из API
    /// </param>
    /// <param name="expected">
    /// Начало, разобранное из таблицы
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если оба значения описывают один и тот же момент UTC
    /// </returns>
    private static bool SameStart(string? stored, DateTimeOffset expected)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return false;

        return DateTimeOffset.TryParse(
                stored,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed)
            && parsed.UtcDateTime == expected.UtcDateTime;
    }

    /// <summary>
    /// Переносит задачу API в снимок, которым пользуется синхронизация
    /// </summary>
    /// <param name="task">
    /// Десериализованная задача. <see cref="TaskResponse.Id"/> должен быть задан
    /// </param>
    /// <returns>
    /// Снимок
    /// </returns>
    private static SingularityTask Map(TaskResponse task) => new(
        task.Id!,
        task.ExternalId,
        task.Title,
        task.Note,
        task.Start,
        task.UseTime == true,
        task.TimeLength is null ? null : (int)Math.Round(task.TimeLength.Value),
        task.Checked,
        task.JournalDate,
        task.Removed == true);

    #endregion Comparison

    #region Transport

    /// <summary>
    /// Отправляет один авторизованный JSON-запрос и читает тело ответа
    /// </summary>
    /// <typeparam name="T">
    /// Контракт ответа
    /// </typeparam>
    /// <param name="method">
    /// Метод HTTP
    /// </param>
    /// <param name="path">
    /// Путь относительно хоста API
    /// </param>
    /// <param name="body">
    /// Тело JSON или <see langword="null"/>, если у вызова его нет
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Десериализованное тело
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Статус неуспешный, либо тело пустое или нечитаемое
    /// </exception>
    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SingularityToken);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);

        using var response = await _http.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = responseBody.Length <= 500 ? responseBody : responseBody[..500];
            throw new InvalidOperationException(
                $"Singularity {(int)response.StatusCode} {method} {path}: {detail}");
        }

        if (string.IsNullOrWhiteSpace(responseBody))
            throw new InvalidOperationException($"Singularity returned an empty body for {method} {path}.");

        return JsonSerializer.Deserialize<T>(responseBody, Json)
            ?? throw new InvalidOperationException($"Singularity returned an unreadable body for {method} {path}.");
    }

    #endregion Transport
}
