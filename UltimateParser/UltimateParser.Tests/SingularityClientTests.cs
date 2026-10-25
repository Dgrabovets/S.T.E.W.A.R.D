using System.Net;
using System.Text;
using System.Text.Json;
using UltimateParser.Common.Models;
using UltimateParser.Singularity.Implementations;

namespace UltimateParser.Tests;

/// <summary>
/// Проверяет запросы клиента Singularity к проектам, тегам и задачам на подмене HTTP
/// </summary>
public class SingularityClientTests
{
    /// <summary>
    /// Ключ проекта для колонки <c>РКИ-2-26</c>
    /// </summary>
    private const string ProjectKey = "up-project:rki-2-26";

    #region Facts

    /// <summary>
    /// Проект с нужным ключом находится даже после переименования, и ничего не пишется
    /// </summary>
    [Fact]
    public async Task GetOrCreateProject_finds_the_project_by_key_after_a_rename()
    {
        var http = new StubHttp(request => request.Path.StartsWith("/v2/project?", StringComparison.Ordinal)
            ? $$"""{"projects":[{"id":"P-1","title":"Другое"},{"id":"P-2","title":"Новое имя","externalId":"{{ProjectKey}}"}]}"""
            : "{}");

        var id = await Client(http).GetOrCreateProjectIdAsync("Расписание", CancellationToken.None);

        Assert.Equal("P-2", id);
        Assert.Single(http.Requests);
    }

    /// <summary>
    /// Проект, найденный по названию, получает ключ отдельным PATCH
    /// </summary>
    [Fact]
    public async Task GetOrCreateProject_tags_a_project_found_by_title()
    {
        var http = new StubHttp(request => request.Method == HttpMethod.Get
            ? """{"projects":[{"id":"P-1","title":"Расписание"}]}"""
            : """{"id":"P-1"}""");

        var id = await Client(http).GetOrCreateProjectIdAsync("Расписание", CancellationToken.None);

        Assert.Equal("P-1", id);
        var patch = http.Requests.Last();
        Assert.Equal(HttpMethod.Patch, patch.Method);
        Assert.Equal("/v2/project/P-1", patch.Path);
        Assert.Equal(ProjectKey, patch.Json.GetProperty("externalId").GetString());
        Assert.Equal(2, http.Requests.Count);
    }

    /// <summary>
    /// Чужой ключ на проекте с нужным названием не перезаписывается
    /// </summary>
    [Fact]
    public async Task GetOrCreateProject_keeps_a_foreign_key_on_a_project_found_by_title()
    {
        var http = new StubHttp(_ => """{"projects":[{"id":"P-1","title":"Расписание","externalId":"manual"}]}""");

        var id = await Client(http).GetOrCreateProjectIdAsync("Расписание", CancellationToken.None);

        Assert.Equal("P-1", id);
        Assert.Single(http.Requests);
    }

    /// <summary>
    /// Новый проект создаётся с ключом внутри родителя из настроек
    /// </summary>
    [Fact]
    public async Task GetOrCreateProject_creates_the_project_under_the_configured_parent()
    {
        var http = new StubHttp(request => request.Method == HttpMethod.Get
            ? """{"projects":[]}"""
            : """{"project":{"id":"P-new","title":"Расписание"}}""");

        var id = await Client(http, parentId: "P-7").GetOrCreateProjectIdAsync("Расписание", CancellationToken.None);

        Assert.Equal("P-new", id);
        var post = http.Requests.Last();
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("/v2/project", post.Path);
        Assert.Equal("Расписание", post.Json.GetProperty("title").GetString());
        Assert.Equal(ProjectKey, post.Json.GetProperty("externalId").GetString());
        Assert.Equal("P-7", post.Json.GetProperty("parent").GetString());
    }

    /// <summary>
    /// Без родителя поле не отправляется: API отклоняет пустую строку
    /// </summary>
    [Fact]
    public async Task GetOrCreateProject_sends_no_parent_when_none_is_configured()
    {
        var http = new StubHttp(request => request.Method == HttpMethod.Get
            ? """{"projects":[]}"""
            : """{"project":{"id":"P-new"}}""");

        await Client(http).GetOrCreateProjectIdAsync("Расписание", CancellationToken.None);

        Assert.False(http.Requests.Last().Json.TryGetProperty("parent", out _));
    }

    /// <summary>
    /// Существующий тег находится без учёта регистра и не создаётся заново
    /// </summary>
    [Fact]
    public async Task GetOrCreateTag_reuses_an_existing_tag_regardless_of_case()
    {
        var http = new StubHttp(_ => """{"tags":[{"id":"A-1","title":"Гейм-дизайн"},{"id":"A-2","title":"Unity","removed":true}]}""");

        var id = await Client(http).GetOrCreateTagIdAsync("гейм-дизайн", CancellationToken.None);

        Assert.Equal("A-1", id);
        Assert.Single(http.Requests);
    }

    /// <summary>
    /// Отсутствующий тег создаётся, а тег из корзины не считается
    /// </summary>
    [Fact]
    public async Task GetOrCreateTag_creates_a_missing_tag()
    {
        var http = new StubHttp(request => request.Method == HttpMethod.Get
            ? """{"tags":[{"id":"A-2","title":"Unity","removed":true}]}"""
            : """{"id":"A-new","title":"Unity"}""");

        var id = await Client(http).GetOrCreateTagIdAsync("Unity", CancellationToken.None);

        Assert.Equal("A-new", id);
        var post = http.Requests.Last();
        Assert.Equal("/v2/tag", post.Path);
        Assert.Equal("Unity", post.Json.GetProperty("title").GetString());
    }

    /// <summary>
    /// Теги занятия попадают в тело задачи, а без тегов поле не отправляется
    /// </summary>
    [Fact]
    public async Task CreateTask_sends_tags_only_when_the_lesson_has_them()
    {
        var http = new StubHttp(_ => """{"id":"T-1"}""");
        var start = new DateTimeOffset(2026, 10, 1, 17, 20, 0, TimeSpan.FromHours(3));
        var tagged = new Lesson("up:rki-2-26:2026-10-01:1720", "Предмет", "Иванов", start, 80) { TagIds = ["A-1", "A-2"] };
        var plain = tagged with { TagIds = [] };
        var client = Client(http);

        await client.CreateTaskAsync("P-1", tagged, CancellationToken.None);
        await client.CreateTaskAsync("P-1", plain, CancellationToken.None);

        Assert.Equal(["A-1", "A-2"], http.Requests[0].Json.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()!).ToArray());
        Assert.False(http.Requests[1].Json.TryGetProperty("tags", out _));
    }

    /// <summary>
    /// Из названия с типом занятия берётся только предмет
    /// </summary>
    [Fact]
    public void Lesson_subject_drops_the_trailing_lesson_type()
    {
        var start = new DateTimeOffset(2026, 10, 1, 17, 20, 0, TimeSpan.FromHours(3));

        Assert.Equal("Гейм-дизайн", new Lesson("k", "Гейм-дизайн (Практика)", string.Empty, start, 80).Subject);
        Assert.Equal("Гейм-дизайн", new Lesson("k", "Гейм-дизайн", string.Empty, start, 80).Subject);
        Assert.Equal("Основы (теория) права", new Lesson("k", "Основы (теория) права (Лекция)", string.Empty, start, 80).Subject);
    }

    #endregion Facts

    #region Helpers

    /// <summary>
    /// Собирает клиент, который отправляет запросы подмене
    /// </summary>
    /// <param name="http">
    /// Подмена HTTP
    /// </param>
    /// <param name="parentId">
    /// Идентификатор родительского проекта или <see langword="null"/>
    /// </param>
    /// <returns>
    /// Клиент для колонки <c>РКИ-2-26</c>
    /// </returns>
    private static SingularityClient Client(StubHttp http, string? parentId = null)
    {
        var options = new SyncOptions
        {
            SingularityToken = "test",
            SpreadsheetId = "sheet",
            SheetGid = "0",
            GroupColumn = "РКИ-2-26",
            ProjectTitle = "Расписание",
            ProjectParentId = parentId,
            TimeZoneId = "Europe/Moscow",
            IntervalMinutes = 30,
            RunOnce = true,
        };
        return new SingularityClient(new HttpClient(http) { BaseAddress = new Uri("https://api.example.test") }, options);
    }

    #endregion Helpers

    #region Fakes

    /// <summary>
    /// Один запрос, который получила подмена
    /// </summary>
    /// <param name="Method">
    /// Метод HTTP
    /// </param>
    /// <param name="Path">
    /// Путь и строка запроса
    /// </param>
    /// <param name="Body">
    /// Тело запроса. Пусто, если его нет
    /// </param>
    private sealed record RecordedRequest(HttpMethod Method, string Path, string Body)
    {
        /// <summary>
        /// Тело запроса как JSON. Для запроса без тела — пустой объект
        /// </summary>
        public JsonElement Json => JsonDocument.Parse(Body.Length == 0 ? "{}" : Body).RootElement;
    }

    /// <summary>
    /// Подмена HTTP, которая записывает запросы и отвечает заранее заданным JSON
    /// </summary>
    /// <param name="respond">
    /// Строит тело ответа по запросу
    /// </param>
    private sealed class StubHttp(Func<RecordedRequest, string> respond) : HttpMessageHandler
    {
        /// <summary>
        /// Запросы в порядке отправки
        /// </summary>
        public List<RecordedRequest> Requests { get; } = [];

        /// <summary>
        /// Записывает запрос и возвращает успешный ответ
        /// </summary>
        /// <param name="request">
        /// Запрос клиента
        /// </param>
        /// <param name="cancellationToken">
        /// Токен отмены
        /// </param>
        /// <returns>
        /// Ответ <c>200</c> с JSON
        /// </returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var recorded = new RecordedRequest(request.Method, request.RequestUri!.PathAndQuery, body);
            Requests.Add(recorded);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(respond(recorded), Encoding.UTF8, "application/json"),
            };
        }
    }

    #endregion Fakes
}
