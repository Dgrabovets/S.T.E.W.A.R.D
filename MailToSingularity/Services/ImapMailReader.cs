using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MailToSingularity.Common;
using MailToSingularity.Interfaces;
using MailToSingularity.Models;

namespace MailToSingularity.Services;

/// <summary>
/// Чтение писем по IMAP через MailKit
/// Письма читаются только по заголовкам и превью, флаг «прочитано» не ставится
/// </summary>
public sealed class ImapMailReader(MailSettings settings) : IMailReader
{
    #region Public methods

    /// <summary>
    /// Подключается к ящику и собирает непрочитанные письма из «Входящих» и из папки преподавателей
    /// </summary>
    /// <param name="ct">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Письма за последние дни, которые указаны в настройках. Папки открываются только для чтения, флаг «прочитано» не ставится
    /// </returns>
    public async Task<IReadOnlyList<MailItem>> GetUnreadAsync(CancellationToken ct)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.SslOnConnect, ct);
        await client.AuthenticateAsync(settings.Login, settings.AppPassword, ct);

        var result = new List<MailItem>();
        var since = DateTime.Today.AddDays(-settings.LookbackDays);

        result.AddRange(await ReadFolderAsync(client.Inbox, since, ct));

        var teachers = await FindFolderByNameAsync(client, settings.TeachersFolder, ct);
        if (teachers is not null)
        {
            result.AddRange(await ReadFolderAsync(teachers, since, ct));
        }

        await client.DisconnectAsync(true, ct);
        return result;
    }

    #endregion

    #region Private methods

    /// <summary>
    /// Читает из папки непрочитанные письма, полученные не раньше указанной даты. Берутся только заголовки и превью текста
    /// </summary>
    /// <param name="folder">
    /// Папка ящика
    /// </param>
    /// <param name="since">
    /// Дата, с которой письма считаются свежими
    /// </param>
    /// <param name="ct">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Письма папки. Пустой список, если свежих непрочитанных нет
    /// </returns>
    private static async Task<IReadOnlyList<MailItem>> ReadFolderAsync(IMailFolder folder, DateTime since, CancellationToken ct)
    {
        await folder.OpenAsync(FolderAccess.ReadOnly, ct);

        var uids = await folder.SearchAsync(SearchQuery.NotSeen.And(SearchQuery.DeliveredAfter(since)), ct);
        if (uids.Count == 0)
        {
            return [];
        }

        var summaries = await folder.FetchAsync(uids, MessageSummaryItems.Envelope | MessageSummaryItems.PreviewText, ct);

        return summaries
            .Select(s =>
            {
                // Конверт всегда запрашивается выше через MessageSummaryItems.Envelope
                var from = s.Envelope!.From.Mailboxes.FirstOrDefault();
                return new MailItem(
                    MessageId: s.Envelope.MessageId ?? $"{folder.FullName}:{s.UniqueId}",
                    Folder: folder.Name,
                    FromName: string.IsNullOrWhiteSpace(from?.Name) ? from?.Address ?? "?" : from!.Name,
                    FromAddress: from?.Address ?? string.Empty,
                    Subject: s.Envelope.Subject ?? "(без темы)",
                    Date: s.Envelope.Date ?? DateTimeOffset.Now,
                    Snippet: (s.PreviewText ?? string.Empty).Trim());
            })
            .ToList();
    }

    /// <summary>
    /// Ищет папку по отображаемому имени
    /// В Яндексе пользовательские папки могут лежать внутри «Входящих»
    /// </summary>
    /// <param name="client">
    /// Подключённый IMAP-клиент
    /// </param>
    /// <param name="name">
    /// Название папки без учёта регистра
    /// </param>
    /// <param name="ct">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Первая найденная папка или <see langword="null"/>, если такой нет
    /// </returns>
    private static async Task<IMailFolder?> FindFolderByNameAsync(ImapClient client, string name, CancellationToken ct)
    {
        var root = client.GetFolder(client.PersonalNamespaces[0]);
        var all = await root.GetSubfoldersAsync(true, ct);
        var queue = new Queue<IMailFolder>(all);

        while (queue.Count > 0)
        {
            var folder = queue.Dequeue();
            if (string.Equals(folder.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return folder;
            }

            foreach (var child in await folder.GetSubfoldersAsync(false, ct))
            {
                queue.Enqueue(child);
            }
        }

        return null;
    }

    #endregion
}
