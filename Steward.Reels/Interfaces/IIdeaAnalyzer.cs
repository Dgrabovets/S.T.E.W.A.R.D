using Steward.Reels.Models;

namespace Steward.Reels.Interfaces;

/// <summary>
/// Разбирает рилс: придумывает название идеи и пишет краткий разбор
/// </summary>
public interface IIdeaAnalyzer
{
    /// <summary>
    /// Разбирает идею по подписи и расшифровке
    /// </summary>
    /// <param name="caption">Подпись под рилсом</param>
    /// <param name="transcript">Расшифровка речи</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Разбор или <see langword="null"/>, если разобрать не удалось: тогда название берётся из подписи</returns>
    Task<IdeaAnalysis?> AnalyzeAsync(string caption, string transcript, CancellationToken cancellationToken);
}
