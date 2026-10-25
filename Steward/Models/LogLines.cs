using System.Text.RegularExpressions;

namespace Steward.Models;

/// <summary>
/// Разбирает вывод программы на записи, чтобы каждая показывалась отдельной плашкой
/// </summary>
public static partial class LogLines
{
    /// <summary>
    /// Делит вывод на записи. Строка с временем в начале открывает новую запись, строки без времени (стек ошибки, продолжение) дописываются к предыдущей
    /// </summary>
    /// <param name="output">Вывод программы, строки разделены переводом строки</param>
    /// <returns>Записи в порядке вывода. Пустой список, если вывода нет</returns>
    public static IReadOnlyList<LogLine> Parse(string output)
    {
        var lines = new List<(string Time, string Text)>();
        foreach (var raw in output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0)
                continue;

            if (Stamped().Match(line) is { Success: true } match)
                lines.Add((match.Groups[1].Value, match.Groups[2].Value));
            else if (lines.Count > 0)
                lines[^1] = (lines[^1].Time, lines[^1].Text + "\n" + line);
            else
                lines.Add((string.Empty, line));
        }

        return [.. lines.Select(line => new LogLine(line.Time, line.Text, ToneOf(line.Text)))];
    }

    /// <summary>
    /// Выбирает цвет плашки по началу текста
    /// </summary>
    /// <param name="text">Текст записи</param>
    /// <returns>Ошибка это <see cref="PanelToneEnum.Problem"/>, письма к действию <see cref="PanelToneEnum.Attention"/>, прочее <see cref="PanelToneEnum.Idle"/></returns>
    private static PanelToneEnum ToneOf(string text) => text switch
    {
        _ when text.StartsWith("Ошибка", StringComparison.Ordinal) || text.StartsWith("[тест] Ошибка", StringComparison.Ordinal) => PanelToneEnum.Problem,
        _ when text.StartsWith("Ответить", StringComparison.Ordinal) || text.StartsWith("Отзыв", StringComparison.Ordinal) || text.StartsWith("[тест] Ответить", StringComparison.Ordinal) || text.StartsWith("[тест] Отзыв", StringComparison.Ordinal) => PanelToneEnum.Attention,
        _ => PanelToneEnum.Idle,
    };

    /// <summary>
    /// Строка, которая начинается с времени <c>дд.мм.гггг чч:мм:сс</c>
    /// </summary>
    /// <returns>Регулярное выражение: группа 1 время, группа 2 текст</returns>
    [GeneratedRegex("^(\\d{2}\\.\\d{2}\\.\\d{4} \\d{2}:\\d{2}:\\d{2})\\s+(.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex Stamped();
}