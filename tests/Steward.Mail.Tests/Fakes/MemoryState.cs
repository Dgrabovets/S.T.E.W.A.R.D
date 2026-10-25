using Steward.Mail.Interfaces;

namespace Steward.Mail.Tests.Fakes;

/// <summary>
/// Список обработанных писем в памяти, который помнит, что из него «записано на диск»
/// </summary>
public sealed class MemoryState : IStateStore
{
    /// <summary>
    /// Все отмеченные идентификаторы
    /// </summary>
    private readonly HashSet<string> _marked = new(StringComparer.Ordinal);

    /// <summary>
    /// Идентификаторы, которые успели сохранить
    /// </summary>
    public HashSet<string> Saved { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Проверяет, отмечено ли письмо
    /// </summary>
    /// <param name="messageId">Идентификатор письма</param>
    /// <returns>
    /// <see langword="true"/>, если письмо отмечено
    /// </returns>
    public bool IsProcessed(string messageId) => _marked.Contains(messageId);

    /// <summary>
    /// Отмечает письмо
    /// </summary>
    /// <param name="messageId">Идентификатор письма</param>
    public void MarkProcessed(string messageId) => _marked.Add(messageId);

    /// <summary>
    /// Переносит отмеченные письма в «сохранённые»
    /// </summary>
    public void Save() => Saved.UnionWith(_marked);
}