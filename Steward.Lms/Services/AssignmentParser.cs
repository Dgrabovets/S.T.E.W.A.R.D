using System.Net;
using System.Text.RegularExpressions;

namespace Steward.Lms.Services;

/// <summary>
/// Файл, прикреплённый к заданию в LMS
/// </summary>
/// <param name="Name">Имя файла</param>
/// <param name="Url">Адрес для скачивания</param>
public sealed record AssignmentFile(string Name, string Url);

/// <summary>
/// Что написано на странице задания в LMS: текст условия, прикреплённые файлы и даты
/// </summary>
/// <param name="Text">Текст условия</param>
/// <param name="Files">Прикреплённые файлы</param>
/// <param name="Dates">Даты со страницы: «Открыто с», «Срок сдачи» и им подобные, строкой на каждую</param>
public sealed record AssignmentDetails(string Text, IReadOnlyList<AssignmentFile> Files, IReadOnlyList<string> Dates);

/// <summary>
/// Разбирает страницу задания Moodle: описание в блоке <c>intro</c>, файлы со ссылками <c>pluginfile.php</c> и даты
/// </summary>
public static partial class AssignmentParser
{
    /// <summary>
    /// Разбирает страницу задания
    /// </summary>
    /// <param name="html">HTML страницы задания</param>
    /// <returns>Условие, файлы и даты. Пустые, если блока описания на странице нет</returns>
    public static AssignmentDetails Parse(string html)
    {
        var dates = DateRow().Matches(html).Select(match => $"{match.Groups[1].Value.Trim()}: {Clean(match.Groups[2].Value)}").ToArray();
        var start = html.IndexOf("id=\"intro\"", StringComparison.Ordinal);
        if (start < 0)
            return new AssignmentDetails(string.Empty, [], dates);

        var end = html.IndexOf("role=\"main\"", start, StringComparison.Ordinal);
        var block = html[start..(end > start ? end : Math.Min(html.Length, start + 60000))];
        var textStart = block.IndexOf('>', StringComparison.Ordinal) + 1;
        var filesAt = block.IndexOf("<div id=\"assign_files_tree", StringComparison.Ordinal);
        var text = Text(block[textStart..(filesAt > textStart ? filesAt : block.Length)]);
        var files = FileLink().Matches(block)
            .Select(match => new AssignmentFile(Clean(match.Groups[2].Value), WebUtility.HtmlDecode(match.Groups[1].Value)))
            .Where(file => file.Name.Length > 0)
            .DistinctBy(file => file.Url)
            .ToArray();

        return new AssignmentDetails(text, files, dates);
    }

    /// <summary>
    /// Превращает фрагмент HTML в текст: абзацы и пункты списка идут с новой строки, теги и сущности убираются
    /// </summary>
    /// <param name="html">Фрагмент HTML</param>
    /// <returns>Текст с переводами строк</returns>
    public static string Text(string html)
    {
        var broken = BlockEnd().Replace(html, "\n");
        var plain = WebUtility.HtmlDecode(Tags().Replace(broken, string.Empty));
        var lines = plain.Replace(' ', ' ').Split('\n').Select(line => Spaces().Replace(line, " ").Trim()).Where(line => line.Length > 0);

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Убирает теги и лишние пробелы из короткой строки
    /// </summary>
    /// <param name="html">Строка HTML</param>
    /// <returns>Чистый текст в одну строку</returns>
    private static string Clean(string html) => Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(html, " ")), " ").Trim();

    [GeneratedRegex(@"<strong>([^<:]+):</strong>\s*([^<]+)")]
    private static partial Regex DateRow();

    [GeneratedRegex(@"<a[^>]+href=""([^""]*pluginfile\.php[^""]*)""[^>]*>(.*?)</a>", RegexOptions.Singleline)]
    private static partial Regex FileLink();

    [GeneratedRegex(@"</(p|div|li|h[1-6]|tr)>|<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnd();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex Spaces();
}
