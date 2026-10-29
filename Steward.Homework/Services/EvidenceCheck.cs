using System.Text;

namespace Steward.Homework.Services;

/// <summary>
/// Проверяет, что цитата, которую привёл агент, действительно есть в расшифровке занятия
/// </summary>
public static class EvidenceCheck
{
    /// <summary>
    /// Ищет цитату в расшифровке без учёта регистра, лишних пробелов и вида кавычек
    /// </summary>
    /// <param name="transcript">Текст расшифровки</param>
    /// <param name="quote">Цитата агента</param>
    /// <returns><see langword="true"/>, если цитата непустая и входит в расшифровку</returns>
    public static bool Verify(string transcript, string quote)
    {
        var needle = Normalize(quote).Trim(' ', '.', ',', ';', ':', '!', '?', '-', '…');
        return needle.Length > 0 && Normalize(transcript).Contains(needle, StringComparison.Ordinal);
    }

    /// <summary>
    /// Приводит текст к сравнимому виду: нижний регистр, один пробел вместо любых пробельных знаков, без кавычек любого вида
    /// </summary>
    /// <param name="text">Исходный текст</param>
    /// <returns>Нормализованный текст без пробелов по краям</returns>
    private static string Normalize(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var symbol in text.ToLowerInvariant())
        {
            if (char.IsWhiteSpace(symbol) || symbol == ' ')
            {
                if (result.Length > 0 && result[^1] != ' ')
                    result.Append(' ');
            }
            else if (symbol is not ('"' or '«' or '»' or '„' or '“' or '”' or '‟'))
                result.Append(symbol);
        }

        return result.ToString().Trim();
    }
}
