using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Steward.Lms.Models;

namespace Steward.Lms.Services;

/// <summary>
/// Разбирает страницы «Задания» и «Тесты» курса в LMS: там таблица с названием, сроком, состоянием и оценкой каждой работы
/// </summary>
public static partial class ActivityParser
{
    /// <summary>
    /// Месяцы в родительном падеже, как они написаны на странице
    /// </summary>
    private static readonly string[] Months =
    [
        "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря",
    ];

    /// <summary>
    /// Достаёт задания со страницы <c>/mod/assign/index.php</c>: столбцы «Секция, Задания, Последний срок сдачи, Ответ, Оценка»
    /// </summary>
    /// <param name="html">Код страницы</param>
    /// <param name="courseId">Номер курса</param>
    /// <param name="subject">Название предмета</param>
    /// <returns>Задания в порядке страницы. Пусто, если таблицы нет</returns>
    public static IReadOnlyList<LmsActivity> ParseAssignments(string html, int courseId, string subject)
    {
        var result = new List<LmsActivity>();
        foreach (var row in Rows(html))
        {
            if (row.Cells.Count < 4 || Link().Match(row.Html) is not { Success: true } link || link.Groups[1].Value != "assign")
                continue;

            var status = row.Cells[3];
            var grade = row.Cells.Count > 4 ? CleanGrade(row.Cells[4]) : string.Empty;
            result.Add(new LmsActivity(link.Groups[2].Value, ActivityKindEnum.Assignment, courseId, subject, row.Cells[0], row.Cells[1], ParseDue(row.Cells[2]), status, grade, IsSubmitted(status), link.Value));
        }

        return result;
    }

    /// <summary>
    /// Достаёт тесты со страницы <c>/mod/quiz/index.php</c>: столбцы «Секция, Имя, Тест закрывается, Оценка»
    /// </summary>
    /// <param name="html">Код страницы</param>
    /// <param name="courseId">Номер курса</param>
    /// <param name="subject">Название предмета</param>
    /// <returns>Тесты в порядке страницы. Пусто, если таблицы нет</returns>
    public static IReadOnlyList<LmsActivity> ParseQuizzes(string html, int courseId, string subject)
    {
        var result = new List<LmsActivity>();
        foreach (var row in Rows(html))
        {
            if (row.Cells.Count < 4 || Link().Match(row.Html) is not { Success: true } link || link.Groups[1].Value != "quiz")
                continue;

            var grade = CleanGrade(row.Cells[3]);
            result.Add(new LmsActivity(link.Groups[2].Value, ActivityKindEnum.Quiz, courseId, subject, row.Cells[0], row.Cells[1], ParseDue(row.Cells[2]), grade.Length > 0 ? $"Оценка {grade}" : "Не пройден", grade, grade.Length > 0, link.Value));
        }

        return result;
    }

    /// <summary>
    /// Разбирает срок вида «пятница, 23 октября 2026, 00:00»
    /// </summary>
    /// <param name="text">Текст ячейки со сроком</param>
    /// <returns>Срок или <see langword="null"/>, если в ячейке прочерк или «Нет даты закрытия»</returns>
    public static DateTime? ParseDue(string text)
    {
        var match = DuePattern().Match(text);
        if (!match.Success)
            return null;

        var month = Array.IndexOf(Months, match.Groups[2].Value.ToLowerInvariant()) + 1;

        return month == 0
            ? null
            : new DateTime(int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), month, int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture), 0);
    }

    /// <summary>
    /// Определяет по состоянию, отправлено ли задание: «Отправлено для оценивания», «Оценено» и подобное считаются сдачей, «Черновик», «Нет ответа» и «Ответ не представлен» нет
    /// </summary>
    /// <param name="status">Состояние как его пишет LMS</param>
    /// <returns>
    /// <see langword="true"/>, если задание сдано
    /// </returns>
    public static bool IsSubmitted(string status)
    {
        var text = status.ToLowerInvariant();
        if (text.Contains("не отправлен", StringComparison.Ordinal) || text.Contains("черновик", StringComparison.Ordinal) || text.Contains("нет ответа", StringComparison.Ordinal) || text.Contains("не представлен", StringComparison.Ordinal))
            return false;

        return text.Contains("отправлено", StringComparison.Ordinal) || text.Contains("оценено", StringComparison.Ordinal) || text.Contains("сдано", StringComparison.Ordinal);
    }

    /// <summary>
    /// Убирает из оценки прочерк: «-» значит, что оценки нет
    /// </summary>
    /// <param name="text">Текст ячейки с оценкой</param>
    /// <returns>Оценка или пустая строка</returns>
    private static string CleanGrade(string text) => text.Trim() is "-" or "—" or "–" ? string.Empty : text.Trim();

    /// <summary>
    /// Делит таблицу на строки, а строки на очищенные от разметки ячейки
    /// </summary>
    /// <param name="html">Код страницы</param>
    /// <returns>Код строки и текст её ячеек</returns>
    private static IEnumerable<(string Html, List<string> Cells)> Rows(string html)
    {
        foreach (Match row in Row().Matches(html))
        {
            var cells = Cell().Matches(row.Value).Select(cell => Text(cell.Groups[1].Value)).ToList();

            yield return (row.Value, cells);
        }
    }

    /// <summary>
    /// Превращает кусок кода в чистый текст одной строкой
    /// </summary>
    /// <param name="html">Код ячейки</param>
    /// <returns>Текст без тегов и лишних пробелов</returns>
    private static string Text(string html) => Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(html, " ")), " ").Trim();

    /// <summary>
    /// Строка таблицы
    /// </summary>
    /// <returns>Регулярное выражение строки</returns>
    [GeneratedRegex("<tr[^>]*>.*?</tr>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Row();

    /// <summary>
    /// Ячейка таблицы
    /// </summary>
    /// <returns>Регулярное выражение ячейки с одной группой: её содержимое</returns>
    [GeneratedRegex("<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Cell();

    /// <summary>
    /// Ссылка на работу: вид (assign или quiz) и номер
    /// </summary>
    /// <returns>Регулярное выражение, группы: вид, номер</returns>
    [GeneratedRegex("https?://[^\"'\\s]+/mod/(assign|quiz)/view\\.php\\?id=(\\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex Link();

    /// <summary>
    /// Срок: день, месяц словом, год, часы и минуты
    /// </summary>
    /// <returns>Регулярное выражение с пятью группами</returns>
    [GeneratedRegex("(\\d{1,2})\\s+([А-Яа-я]+)\\s+(\\d{4}),\\s*(\\d{1,2}):(\\d{2})", RegexOptions.CultureInvariant)]
    private static partial Regex DuePattern();

    /// <summary>
    /// Любой тег
    /// </summary>
    /// <returns>Регулярное выражение тега</returns>
    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex Tags();

    /// <summary>
    /// Подряд идущие пробелы и переносы
    /// </summary>
    /// <returns>Регулярное выражение пробелов</returns>
    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Spaces();
}
