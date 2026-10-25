namespace Steward.Mail.Common;

/// <summary>
/// Корневые настройки приложения
/// Загружаются из appsettings.json рядом с exe
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Подключение к почтовому ящику
    /// </summary>
    public MailSettings Mail { get; init; } = new();

    /// <summary>
    /// Подключение к SingularityApp
    /// </summary>
    public SingularitySettings Singularity { get; init; } = new();

    /// <summary>
    /// Правила разбора писем
    /// </summary>
    public ClassifierSettings Classifier { get; init; } = new();
}

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
}

/// <summary>
/// Параметры REST API SingularityApp
/// </summary>
public sealed class SingularitySettings
{
    /// <summary>
    /// Адрес API без завершающего слэша
    /// </summary>
    public string BaseUrl { get; init; } = "https://api.singularity-app.com";

    /// <summary>
    /// Токен доступа к API. Создаётся в личном кабинете SingularityApp
    /// </summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// Идентификатор проекта для задач
    /// Если не задан, задачи попадают во «Входящие»
    /// </summary>
    public string? ProjectId { get; init; }
}

/// <summary>
/// Правила разбора писем
/// </summary>
public sealed class ClassifierSettings
{
    /// <summary>
    /// Системные адреса, письма с которых во «Входящих» не превращаются в задачи
    /// </summary>
    public List<string> IgnoredSenders { get; init; } = [];
}
