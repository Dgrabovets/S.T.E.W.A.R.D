using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Mail.Tests.Fakes;

/// <summary>
/// Отдаёт заранее заданные письма
/// </summary>
/// <param name="mails">Письма ящика</param>
public sealed class FakeMailReader(params MailItem[] mails) : IMailReader
{
    /// <summary>
    /// Возвращает письма, заданные в конструкторе
    /// </summary>
    /// <param name="cancellationToken">Не используется</param>
    /// <returns>Письма ящика</returns>
    public Task<IReadOnlyList<MailItem>> GetUnreadAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MailItem>>(mails);
}