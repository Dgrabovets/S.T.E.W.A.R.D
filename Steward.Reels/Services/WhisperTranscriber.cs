using Steward.Reels.Common;
using Steward.Reels.Interfaces;

namespace Steward.Reels.Services;

/// <summary>
/// Расшифровывает речь локальным Whisper из папки <c>Whisper</c> репозитория
/// </summary>
/// <param name="python">Путь к python.exe окружения Whisper</param>
/// <param name="script">Путь к transcribe.py</param>
/// <param name="settings">Настройки: язык и сколько ждать</param>
public sealed class WhisperTranscriber(string python, string script, ReelsSettings settings) : ITranscriber
{
    /// <summary>
    /// Запускает transcribe.py и читает текстовый результат
    /// </summary>
    /// <param name="mediaPath">Путь к аудио или видео</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Текст речи</returns>
    /// <exception cref="ReelException">Скрипт завершился с ошибкой или не оставил результата</exception>
    public async Task<string> TranscribeAsync(string mediaPath, CancellationToken cancellationToken)
    {
        var result = await CommandRunner.RunAsync(
            python,
            [script, mediaPath, "--language", settings.Language],
            TimeSpan.FromMinutes(settings.TranscribeTimeoutMinutes),
            cancellationToken);

        if (result.ExitCode != 0)
            throw new ReelException("Whisper: " + (result.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "расшифровка не удалась"));

        var transcript = mediaPath + ".transcript.txt";

        return File.Exists(transcript) ? File.ReadAllText(transcript).Trim() : throw new ReelException("Whisper не оставил расшифровку");
    }
}