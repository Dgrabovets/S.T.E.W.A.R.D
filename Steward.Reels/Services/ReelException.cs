namespace Steward.Reels.Services;

/// <summary>
/// Ожидаемый сбой одного рилса: он не скачался или не расшифровался. Текст исключения безопасно показать в Telegram
/// </summary>
/// <param name="message">Короткая причина без путей и секретов</param>
/// <param name="detail">Первоисточник причины для журнала, например последняя строка yt-dlp. В Telegram не уходит</param>
public sealed class ReelException(string message, string? detail = null) : Exception(message)
{
    /// <summary>
    /// Первоисточник причины для журнала или <see langword="null"/>
    /// </summary>
    public string? Detail { get; } = detail;
}
