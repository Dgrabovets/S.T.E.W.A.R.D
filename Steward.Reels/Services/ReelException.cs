namespace Steward.Reels.Services;

/// <summary>
/// Ожидаемый сбой одного рилса: он не скачался или не расшифровался. Текст исключения безопасно показать в Telegram
/// </summary>
/// <param name="message">Короткая причина без путей и секретов</param>
public sealed class ReelException(string message) : Exception(message);