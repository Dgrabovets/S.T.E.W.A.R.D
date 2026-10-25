using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Steward.Schedule.Common.Helpers;
using Steward.Schedule.Common.Models;
using Steward.Schedule.Scheduling.Implementations;
using Steward.Schedule.Singularity.Dtos;
using Steward.Schedule.Singularity.Interfaces;
using Steward.Schedule.Singularity.Models;

namespace Steward.Schedule.Singularity.Implementations;

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
    /// Напоминание за 15 минут до начала пары
    /// </summary>
    private const int ReminderMinutes = 15;

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
    /// <param name="http">Клиент, у которого базовый адрес — API Singularity</param>
    /// <param name="options">Даёт токен доступа</param>
    public SingularityClient(HttpClient http, SyncOptions options)
    {
        _http = http;
        _options = options;
    }

    #region Projects

    /// <summary>
    /// Ищет активный проект группы по устойчивому ключу, затем по точному названию, и создаёт его, если такого нет
    /// </summary>
    /// <param name="title">Название проекта. По нему проект находится, пока на нём нет ключа, и с ним создаётся новый</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Идентификатор проекта. Проект, найденный по названию без ключа, получает ключ, и дальше переименование ему не мешает</returns>
    /// <exception cref="InvalidOperationException">
    /// Singularity не вернула идентификатор созданного проекта
    /// </exception>
    public async Task<string> GetOrCreateProjectIdAsync(string title, CancellationToken cancellationToken)
    {
        var key = ScheduleParser.ProjectExternalId(_options.GroupColumn);
        var projects = await ListProjectsAsync(cancellationToken);

        var keyed = projects.FirstOrDefault(project => string.Equals(project.ExternalId, key, StringComparison.Ordinal));
        if (keyed?.Id is { Length: > 0 } keyedId)
            return keyedId;

        var titled = FindByTitle(projects, title);
        if (titled?.Id is { Length: > 0 } titledId)
        {
            if (string.IsNullOrEmpty(titled.ExternalId))
            {
                await SendAsync<ProjectResponse>(
                    HttpMethod.Patch,
                    $"/v2/project/{Uri.EscapeDataString(titledId)}",
                    new { externalId = key },
                    cancellationToken);
            }

            return titledId;
        }

        var created = await SendAsync<ProjectCreateResponse>(
            HttpMethod.Post,
            "/v2/project",
            new { title, externalId = key, parent = _options.ProjectParentId },
            cancellationToken);

        if (created.Project?.Id is not { Length: > 0 } projectId)
            throw new InvalidOperationException($"Singularity did not return an id for project '{title}'.");

        return projectId;
    }

    /// <summary>
    /// Ищет активный проект по точному названию и не создаёт его
    /// </summary>
    /// <param name="title">Название проекта</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>
    /// Идентификатор проекта или <see langword="null"/>, если проекта нет
    /// </returns>
    public async Task<string?> FindProjectIdAsync(string title, CancellationToken cancellationToken)
    {
        var projects = await ListProjectsAsync(cancellationToken);
        return FindByTitle(projects, title)?.Id is { Length: > 0 } id ? id : null;
    }

    /// <summary>
    /// Загружает все активные проекты: в API нет фильтра по <c>externalId</c>, поэтому поиск идёт на стороне клиента
    /// </summary>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Проекты всех страниц</returns>
    private async Task<IReadOnlyList<ProjectResponse>> ListProjectsAsync(CancellationToken cancellationToken)
    {
        var projects = new List<ProjectResponse>();
        var offset = 0;
        while (true)
        {
            var page = await SendAsync<ProjectListResponse>(
                HttpMethod.Get,
                $"/v2/project?maxCount={PageSize}&offset={offset}&includeArchived=false&includeRemoved=false",
                null,
                cancellationToken);

            var batch = page.Projects ?? [];
            projects.AddRange(batch);
            if (batch.Count < PageSize)
                return projects;

            offset += batch.Count;
        }
    }

    /// <summary>
    /// Находит проект по точному названию без крайних пробелов
    /// </summary>
    /// <param name="projects">Загруженные проекты</param>
    /// <param name="title">Название проекта</param>
    /// <returns>
    /// Первый подходящий проект или <see langword="null"/>
    /// </returns>
    private static ProjectResponse? FindByTitle(IReadOnlyList<ProjectResponse> projects, string title) =>
        projects.FirstOrDefault(project => string.Equals(project.Title?.Trim(), title, StringComparison.Ordinal));

    #endregion Projects

    #region Tags

    /// <summary>
    /// Находит активный тег по названию без учёта регистра или создаёт его
    /// </summary>
    /// <param name="title">Название тега</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Идентификатор тега</returns>
    /// <exception cref="InvalidOperationException">
    /// Токену не хватает доступа к тегам, либо Singularity не вернула идентификатор созданного тега
    /// </exception>
    public async Task<string> GetOrCreateTagIdAsync(string title, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (true)
        {
            var page = await SendAsync<TagListResponse>(
                HttpMethod.Get,
                $"/v2/tag?maxCount={PageSize}&offset={offset}&includeRemoved=false",
                null,
                cancellationToken);

            var tags = page.Tags ?? [];
            var match = tags.FirstOrDefault(tag =>
                tag.Removed != true && string.Equals(tag.Title?.Trim(), title, StringComparison.OrdinalIgnoreCase));
            if (match?.Id is { Length: > 0 } existingId)
                return existingId;

            if (tags.Count < PageSize)
                break;

            offset += tags.Count;
        }

        var created = await SendAsync<TagResponse>(HttpMethod.Post, "/v2/tag", new { title }, cancellationToken);
        if (created.Id is not { Length: > 0 } tagId)
            throw new InvalidOperationException($"Singularity did not return an id for tag '{title}'.");

        return tagId;
    }

    #endregion Tags

    #region Tasks

    /// <summary>
    /// Загружает все задачи проекта, включая архивные
    /// </summary>
    /// <param name="projectId">Идентификатор проекта</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Сопоставленные задачи. Ответы без идентификатора отбрасываются</returns>
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
    /// <param name="projectId">Проект назначения</param>
    /// <param name="lesson">Занятие, которое нужно создать</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Идентификатор созданной задачи</returns>
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
    /// <param name="taskId">Идентификатор задачи</param>
    /// <param name="projectId">Проект, в котором задача должна остаться</param>
    /// <param name="lesson">Желаемые значения</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда API принимает тело</returns>
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
    /// <param name="taskId">Идентификатор задачи</param>
    /// <param name="journalDate">
    /// Момент архивации. API принимает его только с явным смещением, например <c>2026-10-16T18:00:00+03:00</c>
    /// </param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда API архивирует задачу</returns>
    public Task ArchiveTaskAsync(string taskId, DateTimeOffset journalDate, CancellationToken cancellationToken) =>
        SendAsync<TaskResponse>(
            HttpMethod.Post,
            $"/v2/task/{Uri.EscapeDataString(taskId)}/archive",
            new { journalDate = FormatStart(journalDate) },
            cancellationToken);

    /// <summary>
    /// Ставит у задачи отметку выполнения
    /// </summary>
    /// <param name="taskId">Идентификатор задачи</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда API принимает отметку</returns>
    public Task CompleteTaskAsync(string taskId, CancellationToken cancellationToken) =>
        SendAsync<TaskResponse>(
            HttpMethod.Patch,
            $"/v2/task/{Uri.EscapeDataString(taskId)}",
            new { @checked = 1 },
            cancellationToken);

    /// <summary>
    /// Возвращает задачу из архива
    /// </summary>
    /// <param name="taskId">Идентификатор задачи</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда API возвращает задачу из архива</returns>
    public Task UnarchiveTaskAsync(string taskId, CancellationToken cancellationToken) =>
        SendAsync<TaskResponse>(
            HttpMethod.Post,
            $"/v2/task/{Uri.EscapeDataString(taskId)}/unarchive",
            null,
            cancellationToken);

    /// <summary>
    /// Удаляет задачу. Ответ API пустой
    /// </summary>
    /// <param name="taskId">Идентификатор задачи</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>
    /// Задача, которая завершается после ответа <c>204</c>
    /// </returns>
    public async Task DeleteTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/v2/task/{Uri.EscapeDataString(taskId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SingularityToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var detail = responseBody.Length <= 500 ? responseBody : responseBody[..500];
        throw new InvalidOperationException(
            $"Singularity {(int)response.StatusCode} DELETE /v2/task/{taskId}: {detail}");
    }

    #endregion Tasks

    #region Comparison

    /// <summary>
    /// Сообщает, отличается ли сохранённая задача от занятия названием, заметкой, временем, длительностью или напоминанием
    /// </summary>
    /// <param name="task">Задача, которая сейчас хранится в Singularity</param>
    /// <param name="lesson">Занятие, разобранное из таблицы</param>
    /// <returns>
    /// <see langword="true"/>, если нужно частичное обновление
    /// </returns>
    internal static bool NeedsUpdate(SingularityTask task, Lesson lesson) =>
        !string.Equals(task.Title, lesson.Title, StringComparison.Ordinal)
        || !NoteTextHelper.Same(task.Note, lesson.Note)
        || !task.UseTime
        || task.TimeLength != lesson.TimeLengthMinutes
        || !SameStart(task.Start, lesson.Start)
        || task.Notifies is null
        || !task.Notifies.Contains(ReminderMinutes)
        || lesson.TagIds.Any(tagId => task.Tags is null || !task.Tags.Contains(tagId));

    /// <summary>
    /// Форматирует момент начала так, как в примерах API, с числовым смещением
    /// </summary>
    /// <param name="start">Местное начало занятия</param>
    /// <returns>
    /// Значение вроде <c>2026-10-01T17:20:00+03:00</c>
    /// </returns>
    public static string FormatStart(DateTimeOffset start) =>
        start.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    /// <summary>
    /// Собирает JSON-тело, общее для создания и обновления
    /// </summary>
    /// <param name="projectId">Проект назначения</param>
    /// <param name="lesson">Значения занятия</param>
    /// <param name="includeExternalId">
    /// <see langword="true"/> при создании, чтобы сохранить устойчивый ключ
    /// </param>
    /// <returns>Анонимное тело. Null во внешнем идентификаторе сериализатор пропускает</returns>
    private static object WriteBody(string projectId, Lesson lesson, bool includeExternalId) => new
    {
        title = lesson.Title,
        note = NoteTextHelper.Format(lesson.Note),
        projectId,
        start = FormatStart(lesson.Start),
        useTime = true,
        timeLength = lesson.TimeLengthMinutes,
        notifies = new[] { ReminderMinutes },
        tags = lesson.TagIds.Length == 0 ? null : lesson.TagIds,
        externalId = includeExternalId ? lesson.ExternalId : null,
    };

    /// <summary>
    /// Сравнивает сохранённую строку начала с ожидаемым моментом, не обращая внимания на формат
    /// </summary>
    /// <param name="stored">Значение начала из API</param>
    /// <param name="expected">Начало, разобранное из таблицы</param>
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
    /// <returns>Снимок</returns>
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
        task.Removed == true,
        task.Notifies,
        task.Tags);

    #endregion Comparison

    #region Transport

    /// <summary>
    /// Отправляет один авторизованный JSON-запрос и читает тело ответа
    /// </summary>
    /// <typeparam name="T">
    /// Контракт ответа
    /// </typeparam>
    /// <param name="method">Метод HTTP</param>
    /// <param name="path">Путь относительно хоста API</param>
    /// <param name="body">
    /// Тело JSON или <see langword="null"/>, если у вызова его нет
    /// </param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Десериализованное тело</returns>
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
