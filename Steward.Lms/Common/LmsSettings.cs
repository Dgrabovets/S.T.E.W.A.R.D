namespace Steward.Lms.Common;

/// <summary>
/// Настройки входа в LMS, которые читаются из <c>lmssettings.json</c> рядом с программой
/// </summary>
public sealed class LmsSettings
{
    /// <summary>
    /// Адрес сайта LMS без слэша в конце
    /// </summary>
    public string BaseUrl { get; init; } = "https://lms.ranepa.ru";

    /// <summary>
    /// Логин или почта для входа в LMS. Секрет: файл с ним в репозиторий не попадает
    /// </summary>
    public string Login { get; init; } = string.Empty;

    /// <summary>
    /// Пароль от LMS. Секрет: файл с ним в репозиторий не попадает
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Папка, куда установлены браузеры Playwright. Пусто: рядом с репозиторием, в <c>..\Playwright</c>
    /// </summary>
    public string BrowsersPath { get; init; } = string.Empty;

    /// <summary>
    /// Файл с сохранённым входом в MTS Link. Пусто: <c>data\lms\mts-state.json</c>
    /// </summary>
    public string MtsStatePath { get; init; } = string.Empty;

    /// <summary>
    /// Сколько расшифровок забирать за один запуск. Остальные подтягиваются в следующих запусках, чтобы не грузить сервис и лимиты разом
    /// </summary>
    public int MaxFetchPerRun { get; init; } = 6;

    /// <summary>
    /// Подключение к SingularityApp: токен и проект для задач по работам. Пусто: задачи не заводятся
    /// </summary>
    public Steward.Mail.Common.SingularitySettings? Singularity { get; init; }

    /// <summary>
    /// Заводить ли задачи в SingularityApp и закрывать ли их по сдаче. Пока выключено, программа только читает LMS и показывает, что сделала бы (команда <c>tasks</c>)
    /// </summary>
    public bool CreateTasks { get; init; }

    /// <summary>
    /// Записи короче этого числа минут считаются пробными запусками и сразу получают отметку «не забирать»
    /// </summary>
    public int MinLessonMinutes { get; init; } = 10;
}
