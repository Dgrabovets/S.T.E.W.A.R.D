namespace Steward.Mail.Models;

/// <summary>
/// Важное письмо, сохранённое в архиве после разбора: заголовок, отправитель и начало текста. Приложение читает тот же файл и показывает письма на странице «Почта»
/// </summary>
/// <param name="MessageId">Идентификатор письма: по нему архив не пишет одно письмо дважды</param>
/// <param name="Kind">Тип: «Ответить» или «Отзыв»</param>
/// <param name="FromName">Имя отправителя</param>
/// <param name="FromAddress">Адрес отправителя</param>
/// <param name="Subject">Тема письма</param>
/// <param name="ReceivedAt">Когда письмо пришло</param>
/// <param name="HandledAt">Когда программа его разобрала</param>
/// <param name="Preview">Начало текста письма одной строкой</param>
/// <param name="Link">Ссылка на письмо в веб-почте. Пусто, если её нет</param>
/// <param name="TaskCreated">По письму заведена задача в Singularity</param>
public sealed record ArchivedLetter(
    string MessageId,
    string Kind,
    string FromName,
    string FromAddress,
    string Subject,
    DateTimeOffset ReceivedAt,
    DateTimeOffset HandledAt,
    string Preview,
    string Link,
    bool TaskCreated);
