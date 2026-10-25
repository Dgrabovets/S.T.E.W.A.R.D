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
            [
                "-m", "yt_dlp", "--no-playlist", "--no-progress", "--no-warnings", "--write-info-json", "--restrict-filenames",
                "-f", "bestaudio[ext=m4a]/bestaudio/best",
                "-o", Path.Combine(workDirectory, name + ".%(ext)s"),
                url,
            ],
            TimeSpan.FromMinutes(settings.DownloadTimeoutMinutes),
            cancellationToken);

        if (result.ExitCode != 0)
            throw new ReelException("yt-dlp: " + LastLine(result.Error, "скачать не удалось"));

        var media = Directory.GetFiles(workDirectory, name + ".*").FirstOrDefault(file => !file.EndsWith(".info.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new ReelException("yt-dlp ничего не скачал");

        return ReadInfo(media, Path.Combine(workDirectory, name + ".info.json"));
    }

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