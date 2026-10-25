namespace Steward.Reels.Common;

/// <summary>
/// Настройки бота, которые читаются из <c>appsettings.json</c> рядом с программой
/// </summary>
public sealed class ReelsSettings
{
    /// <summary>
    /// Токен бота из BotFather. Секрет: файл с ним в репозиторий не попадает
    /// </summary>
    public string BotToken { get; init; } = string.Empty;

    /// <summary>
    /// Числовой идентификатор вашего аккаунта Telegram. Бот слушает только его. Ноль означает «ещё не задан»: бот тогда только отвечает, какой идентификатор у отправителя
    /// </summary>
    public long AllowedUserId { get; init; }

    /// <summary>
    /// Язык речи в рилсах для Whisper
    /// </summary>
    public string Language { get; init; } = "ru";

    /// <summary>
    /// Сколько минут даётся на скачивание одного рилса
    /// </summary>
    public int DownloadTimeoutMinutes { get; init; } = 3;

    /// <summary>
    /// Сколько минут даётся на расшифровку одного рилса
    /// </summary>
    public int TranscribeTimeoutMinutes { get; init; } = 20;
}