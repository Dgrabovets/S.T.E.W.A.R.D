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
    /// Браузер, из которого yt-dlp берёт вход в Instagram, например <c>firefox</c>, <c>chrome</c> или <c>edge</c>. Пусто: рилсы скачиваются без входа, и Instagram часто отказывает. Программа только читает cookies браузера, пароль ей не нужен
    /// </summary>
    public string CookiesFromBrowser { get; init; } = string.Empty;

    /// <summary>
    /// Команда Claude Code, которая разбирает идею и придумывает ей название. Пусто: разбора нет, название берётся из подписи
    /// </summary>
    public string ClaudeCommand { get; init; } = "claude";

    /// <summary>
    /// Сколько минут даётся Claude на разбор одной идеи
    /// </summary>
    public int AnalyzeTimeoutMinutes { get; init; } = 3;

    /// <summary>
    /// Сколько минут даётся на скачивание одного рилса
    /// </summary>
    public int DownloadTimeoutMinutes { get; init; } = 3;

    /// <summary>
    /// Сколько минут даётся на расшифровку одного рилса
    /// </summary>
    public int TranscribeTimeoutMinutes { get; init; } = 20;
}