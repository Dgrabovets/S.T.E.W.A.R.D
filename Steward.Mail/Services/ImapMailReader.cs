using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Steward.Mail.Common;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Mail.Services;

/// <summary>
/// Чтение писем по IMAP через MailKit
/// Письма читаются только по заголовкам и превью, флаг «прочитано» не ставится
/// </summary>
public sealed class ImapMailReader(MailSettings settings) : IMailReader
{
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

        var summaries = await folder.FetchAsync(uids, MessageSummaryItems.Envelope | MessageSummaryItems.PreviewText, cancellationToken);

        return summaries
            .Select(summary =>
            {
                // Конверт всегда запрашивается выше через MessageSummaryItems.Envelope
                var from = summary.Envelope!.From.Mailboxes.FirstOrDefault();
                return new MailItem(
                    MessageId: summary.Envelope.MessageId ?? $"{folder.FullName}:{summary.UniqueId}",
                    Folder: folder.Name,
                    FromName: string.IsNullOrWhiteSpace(from?.Name) ? from?.Address ?? "?" : from!.Name,
                    FromAddress: from?.Address ?? string.Empty,
                    Subject: summary.Envelope.Subject ?? "(без темы)",
                    Date: summary.Envelope.Date ?? DateTimeOffset.Now,
                    Snippet: (summary.PreviewText ?? string.Empty).Trim());
            })
            .ToList();
    }

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
