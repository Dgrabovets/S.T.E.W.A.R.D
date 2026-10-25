namespace Steward.Reels.Interfaces;

/// <summary>
/// Превращает речь из файла в текст
/// </summary>
public interface ITranscriber
{
    /// <summary>
    /// Расшифровывает файл
    /// </summary>
    /// <param name="mediaPath">Путь к аудио или видео</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Текст речи. Пустая строка, если в ролике нет слов</returns>
    /// <exception cref="Services.ReelException">Расшифровка не удалась</exception>
    Task<string> TranscribeAsync(string mediaPath, CancellationToken cancellationToken);
}