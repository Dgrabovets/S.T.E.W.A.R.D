using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Steward.Lms.Models;

namespace Steward.Lms.Services;

/// <summary>
/// Разбирает страницу «Ссылка на занятия МТС Линк»: достаёт из неё прошедшие вебинары с записью
/// </summary>
public static partial class WebinarParser
{
    /// <summary>
    /// Месяцы в родительном падеже, как они написаны на странице
    /// </summary>
    private static readonly string[] Months =
    [
        "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря",
    ];

    /// <summary>
    /// Находит на странице все вебинары, у которых есть запись. Текущие вебинары без записи пропускаются
    /// </summary>
    /// <param name="html">Код страницы</param>
    /// <returns>Вебинары в порядке страницы. Пусто, если записей нет</returns>
    public static IReadOnlyList<Webinar> Parse(string html)
    {
        var webinars = new List<Webinar>();
        foreach (Match block in Block().Matches(html))
        {
            var record = Record().Match(block.Value);
            if (!record.Success)
                continue;

            var title = WebUtility.HtmlDecode(Title().Match(block.Value).Groups[1].Value).Trim();
            var subtitle = WebUtility.HtmlDecode(Subtitle().Match(block.Value).Groups[1].Value);
            var start = ParseStart(subtitle);
            if (title.Length == 0 || start is null)
                continue;

            webinars.Add(new Webinar(title, start.Value, ParseMinutes(subtitle), record.Groups[1].Value, record.Groups[2].Value, record.Groups[3].Value));
        }

        return webinars;
    }

    /// <summary>
    /// Разбирает дату и время начала из строки «07 сентября 2026, 19:25 (длительность 01:59)»
    /// </summary>
    /// <param name="subtitle">Подпись под названием вебинара</param>
    /// <returns>Время начала или <see langword="null"/>, если строка не похожа на дату</returns>
    public static DateTime? ParseStart(string subtitle)
    {
        var match = StartPattern().Match(subtitle);
        if (!match.Success)
            return null;

        var month = Array.IndexOf(Months, match.Groups[2].Value.ToLowerInvariant()) + 1;
        if (month == 0)
            return null;

        return new DateTime(
            int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
            month,
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture),
            0);
    }

    /// <summary>
    /// Разбирает длительность из строки «(длительность 01:59)»
    /// </summary>
    /// <param name="subtitle">Подпись под названием вебинара</param>
    /// <returns>Минуты. Ноль, если длительности в строке нет</returns>
    public static int ParseMinutes(string subtitle)
    {
        var match = DurationPattern().Match(subtitle);

        return match.Success ? (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 60) + int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
    }

    /// <summary>
    /// Один пункт списка вебинаров
    /// </summary>
    /// <returns>Регулярное выражение блока</returns>
    [GeneratedRegex("<li class=\"moodle__content\">.*?</li>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Block();

    /// <summary>
    /// Ссылка на запись: адрес, номер мероприятия и номер записи
    /// </summary>
    /// <returns>Регулярное выражение с тремя группами</returns>
    [GeneratedRegex("href=\"(https://my\\.mts-link\\.ru/j/[^/\"]+/(\\d+)/record-new/(\\d+))\"", RegexOptions.CultureInvariant)]
    private static partial Regex Record();

    /// <summary>
    /// Название вебинара
    /// </summary>
    /// <returns>Регулярное выражение с одной группой</returns>
    [GeneratedRegex("<h3[^>]*>(.*?)</h3>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Title();

    /// <summary>
    /// Подпись с датой и длительностью
    /// </summary>
    /// <returns>Регулярное выражение с одной группой</returns>
    [GeneratedRegex("<p class=\"moodle__subtitle\">(.*?)</p>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Subtitle();

    /// <summary>
    /// Дата и время начала
    /// </summary>
    /// <returns>Регулярное выражение: день, месяц словом, год, часы, минуты</returns>
    [GeneratedRegex("(\\d{1,2})\\s+([А-Яа-я]+)\\s+(\\d{4}),\\s*(\\d{1,2}):(\\d{2})", RegexOptions.CultureInvariant)]
    private static partial Regex StartPattern();

    /// <summary>
    /// Длительность в часах и минутах
    /// </summary>
    /// <returns>Регулярное выражение с двумя группами</returns>
    [GeneratedRegex("(\\d{1,2}):(\\d{2})\\)", RegexOptions.CultureInvariant)]
    private static partial Regex DurationPattern();
}
