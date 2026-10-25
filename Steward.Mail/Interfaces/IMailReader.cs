using Steward.Mail.Models;

namespace Steward.Mail.Interfaces;

/// <summary>
/// Чтение непрочитанных писем без изменения их статуса
/// </summary>
public interface IMailReader
{
    /// <summary>
    /// Возвращает непрочитанные письма из папок «Входящие» и «Преподаватели»
    /// </summary>
    /// <param name="ct">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Письма за последние дни, которые указаны в настройках. Статус «прочитано» у них не меняется
    /// </returns>
    Task<IReadOnlyList<MailItem>> GetUnreadAsync(CancellationToken ct);
}
