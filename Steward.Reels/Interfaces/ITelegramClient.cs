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
    /// <returns>Номер отправленного сообщения в чате</returns>
    Task<long> SendAsync(long chatId, string text, CancellationToken cancellationToken);

    /// <summary>
    /// Отправляет сообщение с кнопками под ним. Нажатие приходит как сообщение с <see cref="TelegramMessage.CallbackId"/>, а в тексте лежит данные кнопки
    /// </summary>
    /// <param name="chatId">Чат</param>
    /// <param name="text">Текст вопроса</param>
    /// <param name="buttons">Кнопки в одну строку: подпись и данные</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Номер отправленного сообщения в чате</returns>
    Task<long> SendWithButtonsAsync(long chatId, string text, IReadOnlyList<(string Label, string Data)> buttons, CancellationToken cancellationToken);

    /// <summary>
    /// Удаляет сообщение из чата. Telegram разрешает это в течение двух суток: ответы бота и сообщения владельца в личном чате
    /// </summary>
    /// <param name="chatId">Чат</param>
    /// <param name="messageId">Номер сообщения в чате</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда Telegram отвечает</returns>
    /// <exception cref="HttpRequestException">Telegram отказал, например сообщение уже удалено или старше двух суток</exception>
    Task DeleteAsync(long chatId, long messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Подтверждает нажатие кнопки, чтобы у неё перестали крутиться часики
    /// </summary>
    /// <param name="callbackId">Номер нажатия</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда Telegram принимает подтверждение</returns>
    Task AnswerCallbackAsync(string callbackId, CancellationToken cancellationToken);
}