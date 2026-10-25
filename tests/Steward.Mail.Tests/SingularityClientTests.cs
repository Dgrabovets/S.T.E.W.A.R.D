using System.Net;
using System.Text.Json;
using Steward.Mail.Models;
using Steward.Mail.Services;
using Steward.Mail.Tests.Fakes;

namespace Steward.Mail.Tests;

/// <summary>
/// Проверяет запрос на создание задачи и разбор ошибки
/// </summary>
public class SingularityClientTests
{
    /// <summary>
    /// Задача уходит в POST /v2/task как JSON с полями API, пустой проект не отправляется
    /// </summary>
    [Fact]
    public async Task CreateTaskAsync_posts_the_task_as_json()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        using var client = new SingularityClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });

        await client.CreateTaskAsync(new SingularityTaskRequest { Title = "Ответить", Priority = 0, Start = "2026-10-25" }, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/v2/task", handler.Request.RequestUri!.AbsolutePath);
        using var body = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("Ответить", body.RootElement.GetProperty("title").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("priority").GetInt32());
        Assert.Equal("2026-10-25", body.RootElement.GetProperty("start").GetString());
        Assert.False(body.RootElement.GetProperty("useTime").GetBoolean());
        Assert.False(body.RootElement.TryGetProperty("projectId", out _));
    }

    /// <summary>
    /// Ошибка API превращается в исключение с кодом ответа и телом
    /// </summary>
    [Fact]
    public async Task CreateTaskAsync_throws_with_the_status_and_the_body()
    {
        using var client = new SingularityClient(new HttpClient(new StubHandler(HttpStatusCode.Unauthorized, "bad token")) { BaseAddress = new Uri("https://api.example.test/") });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CreateTaskAsync(new SingularityTaskRequest { Title = "x" }, CancellationToken.None));

        Assert.Contains("401", exception.Message, StringComparison.Ordinal);
        Assert.Contains("bad token", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ключи берутся у всех задач страницы, включая удалённые, задачи без ключа пропускаются, запрос просит и удалённые, и архивные
    /// </summary>
    [Fact]
    public async Task ListExternalIdsAsync_reads_keys_and_asks_for_removed_and_archived_tasks()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"tasks":[{"id":"T-1","externalId":"mail:<a@b>"},{"id":"T-2"},{"id":"T-3","externalId":"up:rki:2026-10-01:2010","removed":true}]}""");
        using var client = new SingularityClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });

        var keys = await client.ListExternalIdsAsync(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.FromHours(1)), CancellationToken.None);

        Assert.Equal(["mail:<a@b>", "up:rki:2026-10-01:2010"], keys.Order().ToArray());
        var query = handler.Request!.RequestUri!.Query;
        Assert.Contains("includeRemoved=true", query, StringComparison.Ordinal);
        Assert.Contains("includeArchived=true", query, StringComparison.Ordinal);
        // 00:00 по +01:00 это 23:00 предыдущих суток по UTC, и фильтр принимает только такую запись
        Assert.Contains("start.gt=2026-10-20T23%3A00%3A00.000Z", query, StringComparison.Ordinal);
        Assert.DoesNotContain("%2B", query, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ошибка при чтении списка задач превращается в исключение с кодом и телом
    /// </summary>
    [Fact]
    public async Task ListExternalIdsAsync_throws_with_the_status_and_the_body()
    {
        using var client = new SingularityClient(new HttpClient(new StubHandler(HttpStatusCode.BadRequest, "bad filter")) { BaseAddress = new Uri("https://api.example.test/") });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.ListExternalIdsAsync(DateTimeOffset.UnixEpoch, CancellationToken.None));

        Assert.Contains("400", exception.Message, StringComparison.Ordinal);
        Assert.Contains("bad filter", exception.Message, StringComparison.Ordinal);
    }
}