using System.Net.Http.Json;
using System.Text.Json;
using Steward.Reels.Interfaces;
using Steward.Reels.Models;

namespace Steward.Reels.Services;

/// <summary>
/// Бот Telegram через Bot API. Адрес с токеном нигде не пишется: ни в лог, ни в тексты ошибок
/// </summary>
public sealed class TelegramClient : ITelegramClient, IDisposable
{
    /// <summary>
    /// Клиент, у которого базовый адрес содержит токен бота
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Создаёт клиент бота
    /// </summary>
    /// <param name="token">Токен из BotFather</param>
    public TelegramClient(string token)
        : this(new HttpClient { BaseAddress = new Uri($"https://api.telegram.org/bot{token}/") })
    {
    }

    /// <summary>
    /// Создаёт клиент на готовом HTTP-клиенте, которым владеет создаваемый объект
    /// </summary>
    /// <param name="http">HTTP-клиент с базовым адресом бота</param>
    public TelegramClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Забирает новые сообщения
    /// </summary>
    /// <param name="offset">Номер первого нужного обновления</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Сообщения в порядке появления. Обновления без сообщения пропускаются</returns>
    /// <exception cref="HttpRequestException">Telegram ответил ошибкой</exception>
    public async Task<IReadOnlyList<TelegramMessage>> GetMessagesAsync(long offset, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync($"getUpdates?offset={offset}&timeout=0&allowed_updates=%5B%22message%22%5D", cancellationToken);
        using var document = JsonDocument.Parse(await ReadAsync(response, cancellationToken));

        var messages = new List<TelegramMessage>();
        foreach (var update in document.RootElement.GetProperty("result").EnumerateArray())
        {
            if (!update.TryGetProperty("message", out var message) || !message.TryGetProperty("from", out var from))
                continue;

            messages.Add(new TelegramMessage(
                update.GetProperty("update_id").GetInt64(),
                message.GetProperty("chat").GetProperty("id").GetInt64(),
                from.GetProperty("id").GetInt64(),
                (message.TryGetProperty("text", out var text) ? text.GetString() : message.TryGetProperty("caption", out var caption) ? caption.GetString() : null) ?? string.Empty));
        }

        return messages;
    }

    /// <summary>
    /// Отправляет сообщение
    /// </summary>
    /// <param name="chatId">Чат</param>
    /// <param name="text">Текст</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда Telegram принял сообщение</returns>
    /// <exception cref="HttpRequestException">Telegram ответил ошибкой</exception>
    public async Task SendAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync("sendMessage", new { chat_id = chatId, text, disable_web_page_preview = true }, cancellationToken);

        await ReadAsync(response, cancellationToken);
    }

    /// <summary>
    /// Освобождает HTTP-клиент
    /// </summary>
    public void Dispose() => _http.Dispose();

    /// <summary>
    /// Читает ответ и бросает исключение с кодом и телом, если Telegram отказал
    /// </summary>
    /// <param name="response">Ответ Bot API</param>
    /// <param name="cancellationToken">Токен отмены чтения</param>
    /// <returns>Тело ответа</returns>
    private static async Task<string> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Telegram ответил {(int)response.StatusCode}: {body}");

        return body;
    }
}