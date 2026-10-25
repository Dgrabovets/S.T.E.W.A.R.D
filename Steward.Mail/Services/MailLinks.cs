using System.Text.RegularExpressions;
using Steward.Mail.Common;
using Steward.Mail.Models;

namespace Steward.Mail.Services;

/// <summary>
/// Ссылки для заметки задачи: на само письмо и на задание в LMS
/// </summary>
public static partial class MailLinks
{
    /// <summary>
    /// Находит ссылку на задание в LMS: сначала страницу задания, затем любую ссылку на LMS
    /// </summary>
    /// <param name="mail">Письмо с отзывом</param>
    /// <returns>Адрес или <see langword="null"/>, если ссылки в письме нет</returns>
    public static string? Assignment(MailItem mail)
    {
        var pool = mail.Links.Concat(UrlPattern().Matches(mail.Body).Select(match => match.Value)).ToList();

        return pool.FirstOrDefault(link => AssignmentPattern().IsMatch(link)) ?? pool.FirstOrDefault(link => LmsPattern().IsMatch(link));
    }

    /// <summary>
    /// Собирает ссылку на письмо в веб-почте по шаблону из настроек
    /// </summary>
    /// <param name="mail">Письмо</param>
    /// <param name="settings">Настройки почты: шаблон и адрес веб-почты</param>
    /// <returns>Ссылка на поиск письма по теме или, если шаблона нет, на саму веб-почту</returns>
    public static string Letter(MailItem mail, MailSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.MessageLinkTemplate))
            return settings.WebMailUrl;

        var subject = ReplyPrefix().Replace(mail.Subject.Trim(), string.Empty);

        return settings.MessageLinkTemplate.Replace("{subject}", Uri.EscapeDataString(subject), StringComparison.Ordinal);
    }

    /// <summary>
    /// Приставки «Re:», «Fwd:», «Ответ:», «Пересл.:» в начале темы
    /// </summary>
    /// <returns>Регулярное выражение приставок</returns>
    [GeneratedRegex("^((re|fwd?|ответ|пересл)\\s*:\\s*)+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ReplyPrefix();

    /// <summary>
    /// Страница задания в LMS
    /// </summary>
    /// <returns>Регулярное выражение адреса задания</returns>
    [GeneratedRegex("lms\\.ranepa\\.ru/mod/assign/view\\.php\\?id=\\d+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex AssignmentPattern();

    /// <summary>
    /// Любой адрес на LMS
    /// </summary>
    /// <returns>Регулярное выражение адреса LMS</returns>
    [GeneratedRegex("^https?://lms\\.ranepa\\.ru/", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LmsPattern();

    /// <summary>
    /// Адрес в обычном тексте
    /// </summary>
    /// <returns>Регулярное выражение адреса</returns>
    [GeneratedRegex("https?://[^\\s<>\"')]+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();
}