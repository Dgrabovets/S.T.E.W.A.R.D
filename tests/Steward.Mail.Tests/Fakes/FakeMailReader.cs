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

    /// <summary>
    /// Письма, которые проверка пометила прочитанными
    /// </summary>
    public List<MailItem> Marked { get; } = [];

    /// <summary>
    /// Запоминает письма, которые проверка пометила прочитанными
    /// </summary>
    /// <param name="items">Разобранные письма</param>
    /// <param name="cancellationToken">Не используется</param>
    /// <returns>Завершённая задача</returns>
    public Task MarkSeenAsync(IReadOnlyList<MailItem> items, CancellationToken cancellationToken)
    {
        Marked.AddRange(items);

        return Task.CompletedTask;
    }
}