using Steward.Reels.Models;

namespace Steward.Reels.Interfaces;

/// <summary>
/// Бот Telegram: читает сообщения и отвечает
/// </summary>
public interface ITelegramClient
{
    /// <summary>
    /// Забирает новые сообщения, начиная с заданного обновления
    /// </summary>
    /// <param name="offset">Номер первого нужного обновления. Всё, что раньше, Telegram больше не отдаёт</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Сообщения в порядке появления</returns>
    Task<IReadOnlyList<TelegramMessage>> GetMessagesAsync(long offset, CancellationToken cancellationToken);

    /// <summary>
    /// Отправляет сообщение в чат
    /// </summary>
    /// <param name="chatId">Чат</param>
    /// <param name="text">Текст ответа</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда Telegram принимает сообщение</returns>
    Task SendAsync(long chatId, string text, CancellationToken cancellationToken);
}