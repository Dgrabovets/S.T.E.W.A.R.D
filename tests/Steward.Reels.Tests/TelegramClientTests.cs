using System.Net;
using Steward.Reels.Services;
using Steward.Reels.Tests.Fakes;

namespace Steward.Reels.Tests;

/// <summary>
/// Проверяет разбор ответов Telegram и запросы к нему
/// </summary>
public class TelegramClientTests
{
    /// <summary>
    /// Из обновлений берутся сообщения с текстом или подписью, остальное пропускается
    /// </summary>
    [Fact]
    public async Task GetMessages_reads_text_and_captions_and_skips_other_updates()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            {"ok":true,"result":[
              {"update_id":10,"message":{"chat":{"id":5},"from":{"id":7},"text":"привет"}},
              {"update_id":11,"edited_message":{"chat":{"id":5}}},
              {"update_id":12,"message":{"chat":{"id":5},"from":{"id":7},"caption":"подпись"}}
            ]}
            """);
        using var client = new TelegramClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/bot/") });

        var messages = await client.GetMessagesAsync(10, CancellationToken.None);

        Assert.Equal([10L, 12L], messages.Select(message => message.UpdateId).ToArray());
        Assert.Equal(["привет", "подпись"], messages.Select(message => message.Text).ToArray());
        Assert.All(messages, message => Assert.Equal((5L, 7L), (message.ChatId, message.UserId)));
        Assert.Contains("offset=10", handler.RequestUrl, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ответ уходит в нужный чат вместе с текстом
    /// </summary>
    [Fact]
    public async Task Send_posts_the_chat_and_text()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"ok":true,"result":{}}""");
        using var client = new TelegramClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/bot/") });

        await client.SendAsync(5, "Готово", CancellationToken.None);

        using var body = System.Text.Json.JsonDocument.Parse(handler.RequestBody);
        Assert.Equal(5, body.RootElement.GetProperty("chat_id").GetInt64());
        Assert.Equal("Готово", body.RootElement.GetProperty("text").GetString());
    }

    /// <summary>
    /// Ошибка Telegram становится исключением, а адрес с токеном в сообщение не попадает
    /// </summary>
    [Fact]
    public async Task An_error_response_throws_without_leaking_the_address()
    {
        var handler = new StubHandler(HttpStatusCode.Unauthorized, """{"ok":false,"description":"Unauthorized"}""");
        using var client = new TelegramClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/botSECRET/") });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetMessagesAsync(0, CancellationToken.None));

        Assert.DoesNotContain("SECRET", exception.Message, StringComparison.Ordinal);
    }
}