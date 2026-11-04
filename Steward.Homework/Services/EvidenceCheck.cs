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
    /// Находит в расшифровке строки, на которых сказана цитата, и показывает их вместе с несколькими строками вокруг
    /// </summary>
    /// <param name="transcript">Текст расшифровки по строкам</param>
    /// <param name="quote">Цитата агента</param>
    /// <param name="around">Сколько строк до и после цитаты показать</param>
    /// <returns>Строки отрывка, у строк с цитатой отмечено попадание. Пусто, если цитаты в расшифровке нет</returns>
    public static IReadOnlyList<(string Text, bool Hit)> Excerpt(string transcript, string quote, int around = 3)
    {
        var needle = Normalize(quote).Trim(' ', '.', ',', ';', ':', '!', '?', '-', '…');
        if (needle.Length == 0)
            return [];

        var lines = transcript.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        // Расшифровка склеивается в одну нормализованную строку, а для каждого её знака запоминается номер строки: так цитата находится, даже если занимает несколько строк
        var joined = new StringBuilder();
        var owner = new List<int>();
        for (var index = 0; index < lines.Length; index++)
        {
            var line = Normalize(lines[index]);
            if (line.Length == 0)
                continue;

            if (joined.Length > 0)
            {
                joined.Append(' ');
                owner.Add(index);
            }

            joined.Append(line);
            owner.AddRange(Enumerable.Repeat(index, line.Length));
        }

        var position = joined.ToString().IndexOf(needle, StringComparison.Ordinal);
        if (position < 0)
            return [];

        var first = owner[position];
        var last = owner[position + needle.Length - 1];
        var from = Math.Max(0, first - around);
        var to = Math.Min(lines.Length - 1, last + around);

        return [.. Enumerable.Range(from, to - from + 1).Where(index => lines[index].Trim().Length > 0).Select(index => (lines[index].Trim(), index >= first && index <= last))];
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
