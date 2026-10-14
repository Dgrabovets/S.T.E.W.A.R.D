namespace UltimateParser.Common.Models;

/// <summary>
/// Настройки листа, проекта назначения и частоты синхронизации
/// </summary>
public sealed record SyncOptions
{
    #region Defaults

    /// <summary>
    /// Таблица с расписанием, если не задана переменная <c>SPREADSHEET_ID</c>
    /// </summary>
    public const string DefaultSpreadsheetId = "1NKd10Q4Ld17T2c_g_71cqAHBAuPaVKNN5rPpWsL20ic";

    /// <summary>
    /// Gid листа расписания, если не задана переменная <c>SHEET_GID</c>
    /// </summary>
    public const string DefaultSheetGid = "197520568";

    /// <summary>
    /// Колонка группы, если не задана переменная <c>GROUP_COLUMN</c>
    /// </summary>
    public const string DefaultGroupColumn = "РКИ-2-26";

    /// <summary>
    /// Название проекта Singularity, если не задана переменная <c>PROJECT_TITLE</c>
    /// </summary>
    public const string DefaultProjectTitle = "Расписание";

    /// <summary>
    /// Часовой пояс IANA, если не задана переменная <c>TIME_ZONE</c>
    /// </summary>
    public const string DefaultTimeZoneId = "Europe/Moscow";

    /// <summary>
    /// Минуты между прогонами, если не задана переменная <c>SYNC_INTERVAL_MINUTES</c>
    /// </summary>
    public const int DefaultIntervalMinutes = 30;

    #endregion Defaults

    #region Properties

    /// <summary>
    /// Токен доступа к REST API Singularity, обязателен
    /// </summary>
    public required string SingularityToken { get; init; }

    /// <summary>
    /// Идентификатор таблицы Google из её адреса
    /// </summary>
    public string SpreadsheetId { get; init; } = DefaultSpreadsheetId;

    /// <summary>
    /// Gid листа из параметра <c>gid</c>
    /// </summary>
    public string SheetGid { get; init; } = DefaultSheetGid;

    /// <summary>
    /// Заголовок колонки группы, из которой создаются задачи
    /// </summary>
    public string GroupColumn { get; init; } = DefaultGroupColumn;

    /// <summary>
    /// Название проекта Singularity, в который попадают задачи
    /// </summary>
    public string ProjectTitle { get; init; } = DefaultProjectTitle;

    /// <summary>
    /// Пояс, в котором выбирается год даты без года и ставится время начала
    /// </summary>
    public string TimeZoneId { get; init; } = DefaultTimeZoneId;

    /// <summary>
    /// Пауза между прогонами в минутах, пока процесс работает
    /// </summary>
    public int IntervalMinutes { get; init; } = DefaultIntervalMinutes;

    /// <summary>
    /// <see langword="true"/>, если процесс должен завершиться после одного прогона
    /// </summary>
    public bool RunOnce { get; init; }

    #endregion Properties

    #region Environment

    /// <summary>
    /// Читает настройки из переменных окружения процесса
    /// </summary>
    /// <param name="runOnce">
    /// <see langword="true"/>, если в командной строке передан <c>--once</c>
    /// </param>
    /// <returns>
    /// Настройки из окружения: у всех полей есть значения по умолчанию, кроме токена
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Токен отсутствует, либо интервал не является положительным целым числом
    /// </exception>
    public static SyncOptions FromEnvironment(bool runOnce)
    {
        var interval = ReadIntervalMinutes();

        return new SyncOptions
        {
            SingularityToken = Required("SINGULARITY_TOKEN"),
            SpreadsheetId = Optional("SPREADSHEET_ID", DefaultSpreadsheetId),
            SheetGid = Optional("SHEET_GID", DefaultSheetGid),
            GroupColumn = Optional("GROUP_COLUMN", DefaultGroupColumn),
            ProjectTitle = Optional("PROJECT_TITLE", DefaultProjectTitle),
            TimeZoneId = Optional("TIME_ZONE", DefaultTimeZoneId),
            IntervalMinutes = interval,
            RunOnce = runOnce,
        };
    }

    /// <summary>
    /// Читает паузу в минутах из <c>SYNC_INTERVAL_MINUTES</c> или из <c>SYNC_INTERVAL_HOURS</c>
    /// </summary>
    /// <returns>
    /// Положительное число минут
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Интервал задан не положительным целым числом
    /// </exception>
    private static int ReadIntervalMinutes()
    {
        var minutesText = Environment.GetEnvironmentVariable("SYNC_INTERVAL_MINUTES");
        if (!string.IsNullOrWhiteSpace(minutesText))
            return ParsePositive(minutesText, "SYNC_INTERVAL_MINUTES");

        var hoursText = Environment.GetEnvironmentVariable("SYNC_INTERVAL_HOURS");
        if (!string.IsNullOrWhiteSpace(hoursText))
            return ParsePositive(hoursText, "SYNC_INTERVAL_HOURS") * 60;

        return DefaultIntervalMinutes;
    }

    /// <summary>
    /// Разбирает положительное целое число из текста настройки
    /// </summary>
    /// <param name="text">
    /// Текст переменной окружения
    /// </param>
    /// <param name="name">
    /// Имя переменной для текста ошибки
    /// </param>
    /// <returns>
    /// Число больше нуля
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Текст не является положительным целым числом
    /// </exception>
    private static int ParsePositive(string text, string name)
    {
        if (!int.TryParse(text, out var value) || value < 1)
            throw new InvalidOperationException($"{name} must be a positive integer.");

        return value;
    }

    /// <summary>
    /// Возвращает обязательную переменную окружения без крайних пробелов
    /// </summary>
    /// <param name="name">
    /// Имя переменной
    /// </param>
    /// <returns>
    /// Обрезанное значение
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Переменная отсутствует или пустая
    /// </exception>
    private static string Required(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Environment variable {name} is required.");

        return value.Trim();
    }

    /// <summary>
    /// Возвращает переменную окружения или <paramref name="fallback"/>, если она пустая
    /// </summary>
    /// <param name="name">
    /// Имя переменной
    /// </param>
    /// <param name="fallback">
    /// Значение, если переменная отсутствует или пустая
    /// </param>
    /// <returns>
    /// Обрезанное значение переменной или запасное значение
    /// </returns>
    private static string Optional(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    #endregion Environment

    #region Time zone

    /// <summary>
    /// Находит часовой пояс по идентификатору IANA или Windows
    /// </summary>
    /// <param name="timeZoneId">
    /// Идентификатор, например <c>Europe/Moscow</c>
    /// </param>
    /// <returns>
    /// Найденный пояс. Для <c>Europe/Moscow</c> также принимается <c>Russian Standard Time</c>
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Идентификатор неизвестен на этой машине
    /// </exception>
    public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var timeZone))
            return timeZone;

        if (timeZoneId.Equals("Europe/Moscow", StringComparison.OrdinalIgnoreCase)
            && TimeZoneInfo.TryFindSystemTimeZoneById("Russian Standard Time", out timeZone))
        {
            return timeZone;
        }

        throw new InvalidOperationException($"Unknown time zone '{timeZoneId}'.");
    }

    #endregion Time zone
}
