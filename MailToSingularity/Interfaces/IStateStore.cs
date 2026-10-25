namespace MailToSingularity.Interfaces;

/// <summary>
/// Хранилище уже обработанных писем, чтобы не дублировать задачи
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// Проверяет, обработано ли письмо раньше
    /// </summary>
    /// <param name="messageId">
    /// Идентификатор письма из заголовка Message-ID
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если по письму уже создана задача или оно попало в сводку
    /// </returns>
    bool IsProcessed(string messageId);

    /// <summary>
    /// Запоминает письмо как обработанное. На диск запись попадает после <see cref="Save"/>
    /// </summary>
    /// <param name="messageId">
    /// Идентификатор письма из заголовка Message-ID
    /// </param>
    void MarkProcessed(string messageId);

    /// <summary>
    /// Записывает список обработанных писем на диск
    /// </summary>
    void Save();
}
