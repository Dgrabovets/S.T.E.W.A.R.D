namespace Steward.Homework.Services;

/// <summary>
/// Разбирает простой Markdown на блоки, из которых собираются Word и презентация: заголовки, пункты списка и абзацы
/// </summary>
public static partial class MarkdownBlocks
{
    /// <summary>
    /// Вид блока
    /// </summary>
    public enum BlockKind
    {
        /// <summary>
        /// Заголовок любого уровня
        /// </summary>
        Heading,

        /// <summary>
        /// Пункт списка
        /// </summary>
        Bullet,

        /// <summary>
        /// Абзац
        /// </summary>
        Paragraph,
    }

    /// <summary>
    /// Блок текста
    /// </summary>
    /// <param name="Kind">Вид блока</param>
    /// <param name="Level">Уровень заголовка, от 1. Для остальных блоков ноль</param>
    /// <param name="Text">Текст без знаков разметки в начале строки. Жирный текст остаётся в виде <c>**…**</c></param>
    public sealed record Block(BlockKind Kind, int Level, string Text);

    /// <summary>
    /// Делит текст на блоки. Пустые строки и тройные кавычки кода пропускаются
    /// </summary>
    /// <param name="markdown">Текст Markdown</param>
    /// <returns>Блоки в порядке текста</returns>
    public static IReadOnlyList<Block> Parse(string markdown)
    {
        var blocks = new List<Block>();
        foreach (var raw in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("```", StringComparison.Ordinal))
                continue;

            var level = line.TakeWhile(symbol => symbol == '#').Count();
            if (level is > 0 and <= 6 && line.Length > level && line[level] == ' ')
            {
                blocks.Add(new Block(BlockKind.Heading, level, line[(level + 1)..].Trim()));
                continue;
            }

            if (line.Length > 2 && line[0] is '-' or '*' or '•' && line[1] == ' ')
            {
                blocks.Add(new Block(BlockKind.Bullet, 0, line[2..].Trim()));
                continue;
            }

            var dot = line.IndexOfAny(['.', ')']);
            if (dot is > 0 and <= 3 && line[..dot].All(char.IsDigit) && line.Length > dot + 1 && line[dot + 1] == ' ')
            {
                blocks.Add(new Block(BlockKind.Bullet, 0, line[(dot + 2)..].Trim()));
                continue;
            }

            blocks.Add(new Block(BlockKind.Paragraph, 0, line));
        }

        return blocks;
    }

    /// <summary>
    /// Делит строку на куски: обычный текст, жирный (<c>**…**</c>) и курсив (<c>*…*</c>)
    /// </summary>
    /// <param name="text">Строка с разметкой</param>
    /// <returns>Куски текста с признаками «жирный» и «курсив»</returns>
    public static IReadOnlyList<(string Text, bool Bold, bool Italic)> Runs(string text)
    {
        var runs = new List<(string, bool, bool)>();
        foreach (System.Text.RegularExpressions.Match match in Emphasis().Matches(text))
        {
            if (match.Groups[1].Success)
                runs.Add((match.Groups[1].Value, true, false));
            else if (match.Groups[2].Success)
                runs.Add((match.Groups[2].Value, false, true));
            else if (runs.Count > 0 && runs[^1] is { Item2: false, Item3: false } previous)
                runs[^1] = (previous.Item1 + match.Value, false, false);
            else
                runs.Add((match.Value, false, false));
        }

        return runs;
    }

    /// <summary>
    /// Убирает знаки выделения из строки
    /// </summary>
    /// <param name="text">Строка с разметкой</param>
    /// <returns>Чистый текст</returns>
    public static string Plain(string text) => string.Concat(Runs(text).Select(run => run.Text));

    [System.Text.RegularExpressions.GeneratedRegex(@"\*\*(.+?)\*\*|\*([^*\s][^*]*?)\*|[^*]+|\*+")]
    private static partial System.Text.RegularExpressions.Regex Emphasis();
}
