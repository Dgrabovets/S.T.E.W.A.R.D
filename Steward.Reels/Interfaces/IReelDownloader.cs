using Steward.Reels.Models;

namespace Steward.Reels.Interfaces;

/// <summary>
/// Скачивает рилс по ссылке
/// </summary>
public interface IReelDownloader
{
    /// <summary>
    /// Скачивает звук рилса в рабочую папку
    /// </summary>
    /// <param name="url">Ссылка на рилс</param>
    /// <param name="name">Имя файлов в рабочей папке без расширения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Скачанный файл и сведения о ролике</returns>
    /// <exception cref="Services.ReelException">Рилс не скачался: нужен вход, ролик закрыт или удалён</exception>
    Task<DownloadedReel> DownloadAsync(string url, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Удаляет файлы, оставшиеся от рилса
    /// </summary>
    /// <param name="name">Имя файлов в рабочей папке без расширения</param>
    void Clean(string name);
}