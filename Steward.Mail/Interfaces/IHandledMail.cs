namespace Steward.Mail.Interfaces;

/// <summary>
/// Память о письмах, которые уже разобраны. Нужна, чтобы информационное письмо, для которого задача не создаётся, не всплывало при каждой проверке
/// </summary>
public interface IHandledMail
{
    /// <summary>
    /// Проверяет, что письмо уже разбирали
    /// </summary>
    /// <param name="messageId">Идентификатор письма</param>
    /// <returns>
    /// <see langword="true"/>, если письмо уже разобрано
    /// </returns>
    bool Contains(string messageId);

    /// <summary>
    /// Запоминает письмо как разобранное
    /// </summary>
    /// <param name="messageId">Идентификатор письма</param>
    void Add(string messageId);
}
