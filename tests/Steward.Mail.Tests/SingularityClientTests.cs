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
}