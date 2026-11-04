using Steward.Mail.Models;

namespace Steward.Mail.Interfaces;

/// <summary>
/// Чтение непрочитанных писем и отметка «прочитано» у разобранных
/// </summary>
public interface IMailReader
{
    /// <summary>
    /// Возвращает непрочитанные письма из папок «Входящие» и «Преподаватели»
    /// </summary>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Письма за последние дни, которые указаны в настройках. При чтении статус «прочитано» у них не меняется</returns>
    Task<IReadOnlyList<MailItem>> GetUnreadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Ставит письмам отметку «прочитано». Вызывается после разбора, чтобы обработанные письма больше не приходили
    /// </summary>
    /// <param name="items">Разобранные письма</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда отметки поставлены</returns>
    Task MarkSeenAsync(IReadOnlyList<MailItem> items, CancellationToken cancellationToken);
}
