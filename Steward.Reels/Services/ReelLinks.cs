using System.Text.RegularExpressions;

namespace Steward.Reels.Services;

/// <summary>
/// Находит в сообщении ссылки на рилсы Instagram
/// </summary>
public static partial class ReelLinks
{
    /// <summary>
    /// Достаёт ссылки на рилсы, посты и видео без параметров отслеживания
    /// </summary>
    /// <param name="text">Текст сообщения</param>
    /// <returns>Ссылки без повторов в порядке появления. Пусто, если ссылок нет</returns>
    public static IReadOnlyList<string> Find(string text) =>
        [.. ReelPattern().Matches(text).Select(match => match.Value).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Ссылка вида <c>instagram.com/reel/XXXX</c>, а также <c>reels</c>, <c>p</c> и <c>tv</c>, с именем автора перед типом или без него
    /// </summary>
    /// <returns>Регулярное выражение ссылки без строки запроса</returns>
    [GeneratedRegex("https?://(?:www\\.)?instagram\\.com/(?:[\\w.]+/)?(?:reels?|p|tv)/[\\w-]+/?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ReelPattern();
}