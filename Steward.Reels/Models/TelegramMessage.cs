namespace Steward.Reels.Models;

/// <summary>
/// Входящее сообщение боту
/// </summary>
/// <param name="UpdateId">Номер обновления Telegram: по нему бот помнит, что уже прочитал</param>
/// <param name="ChatId">Чат, в который отвечать</param>
/// <param name="UserId">Кто написал</param>
/// <param name="Text">Текст сообщения или подпись к вложению. Пустая строка, если текста нет</param>
public sealed record TelegramMessage(long UpdateId, long ChatId, long UserId, string Text);