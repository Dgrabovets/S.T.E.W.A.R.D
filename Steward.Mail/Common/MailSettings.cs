namespace Steward.Mail.Common;

/// <summary>
/// Параметры подключения к почте по IMAP
/// </summary>
public sealed class MailSettings
{
    /// <summary>
    /// Адрес IMAP-сервера
    /// </summary>
    public string Host { get; init; } = "imap.yandex.ru";

    /// <summary>
    /// Порт IMAP-сервера. Подключение идёт по SSL
    /// </summary>
    public int Port { get; init; } = 993;

    /// <summary>
    /// Адрес ящика, он же логин
    /// </summary>
    public string Login { get; init; } = string.Empty;

    /// <summary>
    /// Пароль приложения для почты. Обычный пароль аккаунта сюда не подходит
    /// </summary>
    public string AppPassword { get; init; } = string.Empty;

    /// <summary>
    /// Папка входящих писем
    /// </summary>
    public string InboxFolder { get; init; } = "INBOX";

    /// <summary>
    /// Папка, в которую правило почты складывает отзывы преподавателей
    /// </summary>
    public string TeachersFolder { get; init; } = "Преподаватели";

    /// <summary>
    /// За сколько последних дней смотреть непрочитанные письма
    /// </summary>
    public int LookbackDays { get; init; } = 3;

    /// <summary>
    /// Адрес веб-версии почты. Попадает в заметку задачи, чтобы письмо открывалось одним щелчком
    /// </summary>
    public string WebMailUrl { get; init; } = "https://mail.360.yandex.ru/";

    /// <summary>
    /// Шаблон ссылки на письмо в веб-почте. <c>{subject}</c> заменяется темой без «Re:» и «Fwd:», закодированной для адреса. Пустой шаблон даёт ссылку на саму веб-почту
    /// </summary>
    public string MessageLinkTemplate { get; init; } = "https://mail.360.yandex.ru/#search?request={subject}";
}
