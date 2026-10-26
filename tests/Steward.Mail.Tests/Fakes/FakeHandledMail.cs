using Steward.Mail.Interfaces;

namespace Steward.Mail.Tests.Fakes;

/// <summary>
/// Помнит разобранные письма в памяти
/// </summary>
public sealed class FakeHandledMail : IHandledMail
{
    /// <summary>
    /// Запомненные идентификаторы
    /// </summary>
    public HashSet<string> Ids { get; } = [];

    /// <summary>
    /// Проверяет, что письмо запомнено
    /// </summary>
    /// <param name="messageId">Идентификатор письма</param>
    /// <returns>
    /// <see langword="true"/>, если письмо уже разобрано
    /// </returns>
    public bool Contains(string messageId) => Ids.Contains(messageId);

    /// <summary>
    /// Запоминает письмо
    /// </summary>
    /// <param name="messageId">Идентификатор письма</param>
    public void Add(string messageId) => Ids.Add(messageId);
}
