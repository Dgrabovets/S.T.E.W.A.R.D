using Steward.Reels.Interfaces;
using Steward.Reels.Models;
using Steward.Reels.Services;

namespace Steward.Reels.Tests.Fakes;

/// <summary>
/// Загрузчик, который отдаёт заготовленный рилс или падает с заданной ошибкой
/// </summary>
public sealed class FakeDownloader : IReelDownloader, ITranscriber
{
    /// <summary>
    /// Что вернуть вместо рилса
    /// </summary>
    public DownloadedReel Reel { get; init; } = new("reel.m4a", "Название", "@author", "Подпись рилса");

    /// <summary>
    /// Расшифровка, которую вернёт «Whisper»
    /// </summary>
    public string Transcript { get; init; } = "Первая мысль. Вторая мысль.";

    /// <summary>
    /// Причина отказа загрузки. Если пусто, рилс скачивается
    /// </summary>
    public string? FailWith { get; init; }

    /// <summary>
    /// Имена, для которых вызвали очистку
    /// </summary>
    public List<string> Cleaned { get; } = [];

    /// <summary>
    /// Сколько раз рилс скачивали
    /// </summary>
    public int Downloads { get; private set; }

    /// <summary>
    /// «Скачивает» рилс
    /// </summary>
    /// <param name="url">Ссылка</param>
    /// <param name="name">Имя файлов</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Заготовленный рилс</returns>
    /// <exception cref="ReelException">Задана причина отказа</exception>
    public Task<DownloadedReel> DownloadAsync(string url, string name, CancellationToken cancellationToken)
    {
        Downloads++;

        return FailWith is null ? Task.FromResult(Reel) : throw new ReelException(FailWith);
    }

    /// <summary>
    /// Запоминает очистку
    /// </summary>
    /// <param name="name">Имя файлов</param>
    public void Clean(string name) => Cleaned.Add(name);

    /// <summary>
    /// «Расшифровывает» рилс
    /// </summary>
    /// <param name="mediaPath">Путь к файлу</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Заготовленная расшифровка</returns>
    public Task<string> TranscribeAsync(string mediaPath, CancellationToken cancellationToken) => Task.FromResult(Transcript);
}