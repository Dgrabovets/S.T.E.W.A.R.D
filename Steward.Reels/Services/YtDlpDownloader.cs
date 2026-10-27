using System.Text.Json;
using Steward.Reels.Common;
using Steward.Reels.Interfaces;
using Steward.Reels.Models;

namespace Steward.Reels.Services;

/// <summary>
/// Скачивает рилс программой yt-dlp, которая стоит в окружении Whisper
/// </summary>
/// <param name="python">Путь к python.exe окружения</param>
/// <param name="workDirectory">Папка для скачанных файлов</param>
/// <param name="settings">Настройки: сколько ждать скачивание</param>
public sealed class YtDlpDownloader(string python, string workDirectory, ReelsSettings settings) : IReelDownloader
{
    /// <summary>
    /// Скачивает звук рилса и читает подпись и автора из файла сведений
    /// </summary>
    /// <param name="url">Ссылка на рилс</param>
    /// <param name="name">Имя файлов без расширения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Файл со звуком и сведения о ролике</returns>
    /// <exception cref="ReelException">yt-dlp завершился с ошибкой или ничего не скачал</exception>
    public async Task<DownloadedReel> DownloadAsync(string url, string name, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(workDirectory);

        var result = await CommandRunner.RunAsync(
            python,
            Arguments(url, name, workDirectory, settings.CookiesFromBrowser, settings.CookiesFile),
            TimeSpan.FromMinutes(settings.DownloadTimeoutMinutes),
            cancellationToken);

        if (result.ExitCode != 0)
        {
            var line = LastLine(result.Error, "скачать не удалось");

            throw new ReelException(Explain(line), "yt-dlp: " + line, IsSkip(line));
        }

        var media = Directory.GetFiles(workDirectory, name + ".*").FirstOrDefault(file => !file.EndsWith(".info.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new ReelException("yt-dlp ничего не скачал");

        return ReadInfo(media, Path.Combine(workDirectory, name + ".info.json"));
    }

    /// <summary>
    /// Собирает аргументы yt-dlp: только звук, подпись в файл сведений и, если задан файл или браузер, вход в Instagram из cookies
    /// </summary>
    /// <param name="url">Ссылка на рилс</param>
    /// <param name="name">Имя файлов без расширения</param>
    /// <param name="workDirectory">Папка для скачанных файлов</param>
    /// <param name="cookiesFromBrowser">Название браузера или пусто</param>
    /// <param name="cookiesFile">Путь к файлу cookies в формате Netscape или пусто. Если задан, браузер не читается</param>
    /// <returns>Аргументы запуска python</returns>
    public static IReadOnlyList<string> Arguments(string url, string name, string workDirectory, string cookiesFromBrowser, string cookiesFile = "")
    {
        var arguments = new List<string>
        {
            "-m", "yt_dlp", "--no-playlist", "--no-progress", "--no-warnings", "--write-info-json", "--restrict-filenames",
            "-f", "bestaudio[ext=m4a]/bestaudio/best",
            "-o", Path.Combine(workDirectory, name + ".%(ext)s"),
        };

        if (!string.IsNullOrWhiteSpace(cookiesFile))
        {
            arguments.Add("--cookies");
            arguments.Add(cookiesFile.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(cookiesFromBrowser))
        {
            arguments.Add("--cookies-from-browser");
            arguments.Add(cookiesFromBrowser.Trim());
        }

        arguments.Add(url);

        return arguments;
    }

    /// <summary>
    /// Переводит ошибки входа в Instagram в подсказку, что сделать, а остальные оставляет как есть
    /// </summary>
    /// <param name="line">Последняя строка ошибки yt-dlp</param>
    /// <returns>Короткий текст для Telegram без путей к файлам</returns>
    public static string Explain(string line)
    {
        if (line.Contains("cookies database", StringComparison.OrdinalIgnoreCase) || line.Contains("cookies-from-browser", StringComparison.OrdinalIgnoreCase) && line.Contains("could not", StringComparison.OrdinalIgnoreCase))
            return "Бот не нашёл cookies браузера. Открой Firefox, зайди в аккаунт Instagram и пришли ссылку ещё раз.";

        if (line.Contains("No video formats found", StringComparison.OrdinalIgnoreCase))
            return "Пропустил этот пост: в нём нет видео, только фото. Бот разбирает рилсы с речью, а в картинках расшифровывать нечего.";

        string[] loginMarks = ["empty media response", "login required", "login_required", "log in", "sign in", "checkpoint", "rate-limit", "rate limit", "requested content is not available"];

        return loginMarks.Any(mark => line.Contains(mark, StringComparison.OrdinalIgnoreCase))
            ? "Instagram не пустил без входа. Открой Firefox, зайди в аккаунт Instagram и пришли ссылку ещё раз."
            : "yt-dlp: " + line;
    }

    /// <summary>
    /// Пост без видео пропускается как обычный случай, а не как сбой
    /// </summary>
    /// <param name="line">Последняя строка ошибки yt-dlp</param>
    /// <returns>
    /// <see langword="true"/>, если причина не сбой
    /// </returns>
    public static bool IsSkip(string line) => line.Contains("No video formats found", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Удаляет скачанный файл, сведения и расшифровки рилса
    /// </summary>
    /// <param name="name">Имя файлов без расширения</param>
    public void Clean(string name)
    {
        if (!Directory.Exists(workDirectory))
            return;

        foreach (var file in Directory.GetFiles(workDirectory, name + ".*"))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // ignore
            }
        }
    }

    /// <summary>
    /// Читает подпись, название и автора из файла сведений yt-dlp
    /// </summary>
    /// <param name="media">Скачанный файл</param>
    /// <param name="infoPath">Путь к файлу сведений</param>
    /// <returns>Рилс со сведениями. Если файла сведений нет, поля пустые</returns>
    private static DownloadedReel ReadInfo(string media, string infoPath)
    {
        if (!File.Exists(infoPath))
            return new DownloadedReel(media, string.Empty, string.Empty, string.Empty);

        using var document = JsonDocument.Parse(File.ReadAllText(infoPath));
        string Text(string key) => document.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : string.Empty;

        var author = Text("uploader");

        return new DownloadedReel(media, Text("title"), author.Length > 0 ? author : Text("channel"), Text("description"));
    }

    /// <summary>
    /// Берёт последнюю непустую строку вывода и обрезает её
    /// </summary>
    /// <param name="text">Вывод программы</param>
    /// <param name="fallback">Что вернуть, если вывод пуст</param>
    /// <returns>Строка не длиннее 200 символов</returns>
    private static string LastLine(string text, string fallback)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();

        return line is null ? fallback : line.Length <= 200 ? line : line[..200] + "…";
    }
}