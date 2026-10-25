namespace Steward.Mail.Models;

/// <summary>
/// Непрочитанное письмо, прочитанное из почтового ящика
/// </summary>
/// <param name="MessageId">Идентификатор из заголовка Message-ID. Если его нет, собирается из папки и номера письма</param>
/// <param name="Folder">Название папки, в которой лежит письмо</param>
/// <param name="FromName">Имя отправителя. Если его нет, адрес</param>
/// <param name="FromAddress">Адрес отправителя</param>
/// <param name="Subject">Тема письма</param>
/// <param name="Date">Когда письмо отправлено</param>
/// <param name="Body">Текст письма без разметки. Если тело не удалось прочитать, начало текста из превью</param>
/// <param name="Links">Адреса, найденные в тексте и в ссылках письма, без повторов</param>
public sealed record MailItem(
    string MessageId,
    string Folder,
    string FromName,
    string FromAddress,
    string Subject,
    DateTimeOffset Date,
    string Body,
    IReadOnlyList<string> Links);