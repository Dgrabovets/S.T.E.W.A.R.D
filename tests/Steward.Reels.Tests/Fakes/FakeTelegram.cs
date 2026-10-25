using Steward.Reels.Interfaces;
using Steward.Reels.Models;

namespace Steward.Reels.Tests.Fakes;

/// <summary>
/// Бот Telegram, который отдаёт заданные сообщения и запоминает ответы
/// </summary>
/// <param name="messages">Сообщения, которые придут боту</param>
public sealed class FakeTelegram(params TelegramMessage[] messages) : ITelegramClient
{
    /// <summary>
    /// Тексты отправленных ответов
    /// </summary>
    public List<string> Sent { get; } = [];

    /// <summary>
    /// Номер, с которого бот запросил сообщения в последний раз
    /// </summary>
    public long LastOffset { get; private set; }

    /// <summary>
    /// Отдаёт сообщения, номер которых не меньше запрошенного
    /// </summary>
    /// <param name="offset">Номер первого нужного обновления</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Подходящие сообщения</returns>
    public Task<IReadOnlyList<TelegramMessage>> GetMessagesAsync(long offset, CancellationToken cancellationToken)
    {
        LastOffset = offset;

        return Task.FromResult<IReadOnlyList<TelegramMessage>>([.. messages.Where(message => message.UpdateId >= offset)]);
    }

    /// <summary>
    /// Запоминает ответ
    /// </summary>
    /// <param name="chatId">Чат</param>
    /// <param name="text">Текст</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Завершённая задача</returns>
    public Task SendAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        Sent.Add(text);

        return Task.CompletedTask;
    }
}