using System.Text;
using System.Text.RegularExpressions;

namespace Steward.Mail.Services;

/// <summary>
/// Достаёт из текста письма то, что написал собеседник: без цитат прошлой переписки и подписи
/// </summary>
public static partial class MailReply
{
    /// <summary>
    /// Наибольшая длина текста ответа в символах
    /// </summary>
    private const int MAX_LENGTH = 1500;

    /// <summary>
    /// Берёт новый текст письма и обрезает его по границе слова
    /// </summary>
    /// <param name="body">Весь текст письма</param>
    /// <returns>Текст собеседника или пустая строка, если в письме нет текста</returns>
    public static string Extract(string body)
    {
        var kept = new List<string>();
        foreach (var raw in body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.TrimEnd();
            if (IsQuoteStart(line, hasText: kept.Any(item => item.Length > 0)))
                break;

            kept.Add(line);
        }

        var text = BlankRuns().Replace(string.Join('\n', kept).Trim(), "\n\n");

        return Cut(text);
    }

    /// <summary>
    /// Проверяет, что с этой строки начинается цитата, пересланное сообщение или подпись
    /// </summary>
    /// <param name="line">Строка письма без пробелов справа</param>
    /// <param name="hasText">Был ли уже собственный текст: заголовок «От:» в самом начале письма цитатой не считается</param>
    /// <returns>
    /// <see langword="true"/>, если дальше идёт чужой текст
    /// </returns>
    private static bool IsQuoteStart(string line, bool hasText)
    {
        var trimmed = line.TrimStart();

        return trimmed.StartsWith('>')
            || SignatureDash().IsMatch(trimmed)
            || Separator().IsMatch(trimmed)
            || Attribution().IsMatch(trimmed)
            || (hasText && HeaderBlock().IsMatch(trimmed));
    }

    /// <summary>
    /// Обрезает текст по границе слова и ставит многоточие
    /// </summary>
    /// <param name="text">Текст ответа</param>
    /// <returns>Тот же текст или его начало</returns>
    private static string Cut(string text)
    {
        if (text.Length <= MAX_LENGTH)
            return text;

        var cut = text[..MAX_LENGTH];
        var space = cut.LastIndexOfAny([' ', '\n']);

        return (space > MAX_LENGTH / 2 ? cut[..space] : cut).TrimEnd() + "…";
    }

    /// <summary>
    /// Строка «-- » перед подписью
    /// </summary>
    /// <returns>Регулярное выражение строки подписи</returns>
    [GeneratedRegex("^--\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex SignatureDash();

    /// <summary>
    /// Линия-разделитель с пометкой о пересланном или исходном сообщении
    /// </summary>
    /// <returns>Регулярное выражение разделителя</returns>
    [GeneratedRegex("^[-_=]{3,}\\s*(Original Message|Исходное сообщение|Пересылаемое сообщение|Forwarded message|Переслано)?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Separator();

    /// <summary>
    /// Строка вида «25.10.2026, 11:19, Имя &lt;a@b.ru&gt;:» или «Имя написал(а):», с которой начинается цитата
    /// </summary>
    /// <returns>Регулярное выражение строки-представления</returns>
    [GeneratedRegex("^(\\d{1,2}\\.\\d{1,2}\\.\\d{2,4},?\\s+\\d{1,2}:\\d{2}.*:|.{0,200}(написал(\\(а\\)|а)?|wrote|пишет):)\\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Attribution();

    /// <summary>
    /// Строка заголовка пересланного письма: «От:», «From:», «Отправлено:», «Тема:»
    /// </summary>
    /// <returns>Регулярное выражение заголовка</returns>
    [GeneratedRegex("^(От|From|Отправлено|Sent|Кому|To|Тема|Subject|Дата|Date):\\s", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex HeaderBlock();

    /// <summary>
    /// Три пустые строки и больше подряд
    /// </summary>
    /// <returns>Регулярное выражение пустых строк</returns>
    [GeneratedRegex("\\n{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex BlankRuns();
}