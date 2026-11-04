using Steward.Reels.Interfaces;
using Steward.Reels.Models;

namespace Steward.Reels.Tests.Fakes;

/// <summary>
/// Разбор идеи, который отдаёт заготовленный ответ
/// </summary>
/// <param name="analysis">Что вернуть. <see langword="null"/>: разобрать не удалось</param>
public sealed class FakeAnalyzer(IdeaAnalysis? analysis) : IIdeaAnalyzer
{
    /// <summary>
    /// Отдаёт заготовленный разбор
    /// </summary>
    /// <param name="caption">Подпись</param>
    /// <param name="transcript">Расшифровка</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Заготовленный разбор</returns>
    public Task<IdeaAnalysis?> AnalyzeAsync(string caption, string transcript, CancellationToken cancellationToken) => Task.FromResult(analysis);
}
