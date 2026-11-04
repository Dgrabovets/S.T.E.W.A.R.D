namespace Steward.Reels.Models;

/// <summary>
/// Входящее сообщение боту
/// </summary>
/// <param name="UpdateId">Номер обновления Telegram: по нему бот помнит, что уже прочитал</param>
/// <param name="ChatId">Чат, в который отвечать</param>
/// <param name="UserId">Кто написал</param>
/// <param name="Text">Текст сообщения или подпись к вложению. Пустая строка, если текста нет. У нажатия кнопки это данные кнопки</param>
/// <param name="MessageId">Номер сообщения в чате, нужен, чтобы удалить его в следующий проход. Ноль, если неизвестен</param>
/// <param name="CallbackId">Номер нажатия кнопки или <see langword="null"/>, если это обычное сообщение</param>
public sealed record TelegramMessage(long UpdateId, long ChatId, long UserId, string Text, string? CallbackId = null, long MessageId = 0);