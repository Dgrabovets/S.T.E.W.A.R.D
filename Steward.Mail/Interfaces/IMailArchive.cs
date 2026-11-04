using Steward.Mail.Models;

namespace Steward.Mail.Interfaces;

/// <summary>
/// Архив важных писем: хранит заголовок, отправителя и начало текста, чтобы письмо не пропадало из виду после разбора
/// </summary>
public interface IMailArchive
{
    /// <summary>
    /// Записывает письмо в архив. Письмо с тем же идентификатором второй раз не записывается
    /// </summary>
    /// <param name="letter">Письмо</param>
    void Add(ArchivedLetter letter);
}
