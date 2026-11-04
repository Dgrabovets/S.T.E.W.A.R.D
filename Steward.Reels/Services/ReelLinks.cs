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
    /// Ключ рилса для сравнения: код из ссылки. Автор в адресе, <c>reels</c> вместо <c>reel</c> и слэш в конце на ключ не влияют. Регистр кода сохраняется: в Instagram он значим
    /// </summary>
    /// <param name="url">Ссылка на рилс</param>
    /// <returns>Например «reel/AbC123». Для чужой ссылки её саму без слэша в конце</returns>
    public static string Key(string url)
    {
        var match = KeyPattern().Match(url);

        return match.Success ? $"{(match.Groups[1].Value.StartsWith("reel", StringComparison.OrdinalIgnoreCase) ? "reel" : match.Groups[1].Value.ToLowerInvariant())}/{match.Groups[2].Value}" : url.Trim().TrimEnd('/');
    }

    /// <summary>
    /// Ссылка вида <c>instagram.com/reel/XXXX</c>, а также <c>reels</c>, <c>p</c> и <c>tv</c>, с именем автора перед типом или без него
    /// </summary>
    /// <returns>Регулярное выражение ссылки без строки запроса</returns>
    [GeneratedRegex("https?://(?:www\\.)?instagram\\.com/(?:[\\w.]+/)?(?:reels?|p|tv)/[\\w-]+/?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ReelPattern();

    /// <summary>
    /// Тип и код внутри ссылки
    /// </summary>
    /// <returns>Регулярное выражение с двумя группами: тип и код</returns>
    [GeneratedRegex("/(reels?|p|tv)/([\\w-]+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex KeyPattern();
}
