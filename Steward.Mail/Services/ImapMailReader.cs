using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using System.Net;
using System.Text.RegularExpressions;
using Steward.Mail.Common;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Mail.Services;

/// <summary>
/// Чтение писем по IMAP через MailKit
/// Письма читаются только по заголовкам и превью, флаг «прочитано» не ставится
/// </summary>
public sealed partial class ImapMailReader(MailSettings settings) : IMailReader
{
    /// <summary>
    /// Наибольшая длина текста письма в символах: дальше письмо обрезается
    /// </summary>
    private const int BODY_LIMIT = 20000;

    /// <summary>
    /// Подключается к ящику и собирает непрочитанные письма из «Входящих» и из папки преподавателей
    /// </summary>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Письма за последние дни, которые указаны в настройках. Папки открываются только для чтения, флаг «прочитано» не ставится</returns>
    public async Task<IReadOnlyList<MailItem>> GetUnreadAsync(CancellationToken cancellationToken)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.SslOnConnect, cancellationToken);
        await client.AuthenticateAsync(settings.Login, settings.AppPassword, cancellationToken);

        var result = new List<MailItem>();
        var since = DateTime.Today.AddDays(-settings.LookbackDays);

        result.AddRange(await ReadFolderAsync(client.Inbox, since, cancellationToken));

        var teachers = await FindFolderByNameAsync(client, settings.TeachersFolder, cancellationToken);
        if (teachers is not null)
            result.AddRange(await ReadFolderAsync(teachers, since, cancellationToken));

        await client.DisconnectAsync(true, cancellationToken);
        return result;
    }

    /// <summary>
    /// Читает из папки непрочитанные письма, полученные не раньше указанной даты. Берутся только заголовки и превью текста
    /// </summary>
    /// <param name="folder">Папка ящика</param>
    /// <param name="since">Дата, с которой письма считаются свежими</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Письма папки. Пустой список, если свежих непрочитанных нет</returns>
    private static async Task<IReadOnlyList<MailItem>> ReadFolderAsync(IMailFolder folder, DateTime since, CancellationToken cancellationToken)
    {
        await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        var uids = await folder.SearchAsync(SearchQuery.NotSeen.And(SearchQuery.DeliveredAfter(since)), cancellationToken);
        if (uids.Count == 0)
            return [];

        var summaries = await folder.FetchAsync(uids, MessageSummaryItems.Envelope | MessageSummaryItems.PreviewText | MessageSummaryItems.BodyStructure, cancellationToken);

        var result = new List<MailItem>();
        foreach (var summary in summaries)
            result.Add(await ToItemAsync(folder, summary, cancellationToken));

        return result;
    }

    /// <summary>
    /// Собирает письмо из конверта и тела. Тело читается без отметки «прочитано»: папка открыта только для чтения
    /// </summary>
    /// <param name="folder">Папка письма</param>
    /// <param name="summary">Конверт, превью и устройство тела письма</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Письмо с текстом и найденными адресами</returns>
    private static async Task<MailItem> ToItemAsync(IMailFolder folder, IMessageSummary summary, CancellationToken cancellationToken)
    {
        // Конверт всегда запрашивается выше через MessageSummaryItems.Envelope
        var from = summary.Envelope!.From.Mailboxes.FirstOrDefault();
        var (body, links) = await ReadBodyAsync(folder, summary, cancellationToken);

        return new MailItem(
            MessageId: summary.Envelope.MessageId ?? $"{folder.FullName}:{summary.UniqueId}",
            Folder: folder.Name,
            FromName: string.IsNullOrWhiteSpace(from?.Name) ? from?.Address ?? "?" : from!.Name,
            FromAddress: from?.Address ?? string.Empty,
            Subject: summary.Envelope.Subject ?? "(без темы)",
            Date: summary.Envelope.Date ?? DateTimeOffset.Now,
            Body: body.Length > 0 ? body : (summary.PreviewText ?? string.Empty).Trim(),
            Links: links);
    }

    /// <summary>
    /// Читает текст письма и адреса в нём. Если есть простой текст, берётся он, иначе текст из HTML. Ссылки из HTML нужны, когда в тексте адреса нет
    /// </summary>
    /// <param name="folder">Папка письма</param>
    /// <param name="summary">Устройство тела письма</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Текст без разметки (не длиннее лимита) и адреса без повторов. Пусто, если тело не удалось прочитать</returns>
    private static async Task<(string Body, IReadOnlyList<string> Links)> ReadBodyAsync(IMailFolder folder, IMessageSummary summary, CancellationToken cancellationToken)
    {
        try
        {
            var text = await ReadPartAsync(folder, summary, summary.TextBody, cancellationToken);
            var html = summary.HtmlBody is not null && (text.Length == 0 || !UrlPattern().IsMatch(text))
                ? await ReadPartAsync(folder, summary, summary.HtmlBody, cancellationToken)
                : string.Empty;

            var body = text.Length > 0 ? text : MailHtml.ToText(html);
            var links = UrlPattern().Matches(text).Select(match => match.Value)
                .Concat(HrefPattern().Matches(html).Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var trimmed = body.Trim();

            return (trimmed.Length > BODY_LIMIT ? trimmed[..BODY_LIMIT] : trimmed, links);
        }
        catch (ImapCommandException)
        {
            return (string.Empty, []);
        }
    }

    /// <summary>
    /// Скачивает одну текстовую часть письма
    /// </summary>
    /// <param name="folder">Папка письма</param>
    /// <param name="summary">Письмо</param>
    /// <param name="part">Часть из устройства тела или <see langword="null"/>, если такой нет</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Текст части или пустая строка</returns>
    private static async Task<string> ReadPartAsync(IMailFolder folder, IMessageSummary summary, BodyPartText? part, CancellationToken cancellationToken)
    {
        if (part is null)
            return string.Empty;

        return await folder.GetBodyPartAsync(summary.UniqueId, part, cancellationToken) is TextPart text ? text.Text : string.Empty;
    }

    /// <summary>
    /// Адрес в обычном тексте
    /// </summary>
    /// <returns>Регулярное выражение адреса</returns>
    [GeneratedRegex("https?://[^\\s<>\"')]+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    /// <summary>
    /// Адрес в атрибуте <c>href</c> HTML-письма
    /// </summary>
    /// <returns>Регулярное выражение, группа 1 это адрес</returns>
    [GeneratedRegex("href\\s*=\\s*[\"'](https?://[^\"']+)[\"']", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex HrefPattern();
    /// <summary>
    /// Ищет папку по отображаемому имени
    /// В Яндексе пользовательские папки могут лежать внутри «Входящих»
    /// </summary>
    /// <param name="client">Подключённый IMAP-клиент</param>
    /// <param name="name">Название папки без учёта регистра</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>
    /// Первая найденная папка или <see langword="null"/>, если такой нет
    /// </returns>
    private static async Task<IMailFolder?> FindFolderByNameAsync(ImapClient client, string name, CancellationToken cancellationToken)
    {
        var all = await client.GetFolder(client.PersonalNamespaces[0]).GetSubfoldersAsync(true, cancellationToken);
        var queue = new Queue<IMailFolder>(all);

        while (queue.Count > 0)
        {
            var folder = queue.Dequeue();
            if (string.Equals(folder.Name, name, StringComparison.OrdinalIgnoreCase))
                return folder;

            foreach (var child in await folder.GetSubfoldersAsync(false, cancellationToken))
                queue.Enqueue(child);
        }

        return null;
    }
}
