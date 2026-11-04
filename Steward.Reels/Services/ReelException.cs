namespace Steward.Reels.Services;

/// <summary>
/// Ожидаемый сбой одного рилса: он не скачался или не расшифровался. Текст исключения безопасно показать в Telegram
/// </summary>
/// <param name="message">Короткая причина без путей и секретов</param>
/// <param name="detail">Первоисточник причины для журнала, например последняя строка yt-dlp. В Telegram не уходит</param>
/// <param name="isSkip">Это не сбой, а обычный случай, например пост без видео: бот отвечает объяснением без слов об ошибке</param>
public sealed class ReelException(string message, string? detail = null, bool isSkip = false) : Exception(message)
{
    /// <summary>
    /// Первоисточник причины для журнала или <see langword="null"/>
    /// </summary>
    public string? Detail { get; } = detail;

    /// <summary>
    /// Пост пропущен по понятной причине, а не из-за сбоя
    /// </summary>
    public bool IsSkip { get; } = isSkip;
}
