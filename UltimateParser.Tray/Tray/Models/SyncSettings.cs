using System.Globalization;
using UltimateParser.Tray.Monitoring.Implementations;

namespace UltimateParser.Tray.Tray.Models;

/// <summary>
/// Параметры сверки, которые лежат в файле настроек
/// </summary>
public sealed class SyncSettings
{
    /// <summary>
    /// Нижняя граница интервала, в минутах
    /// </summary>
    public const int MIN_INTERVAL = 1;

    /// <summary>
    /// Верхняя граница интервала, в минутах
    /// </summary>
    public const int MAX_INTERVAL = 180;

    /// <summary>
    /// Интервал, если в файле его нет
    /// </summary>
    public const int DEFAULT_INTERVAL = 5;

    /// <summary>
    /// Имя параметра с паузой между сверками
    /// </summary>
    private const string SYNC_INTERVAL_MINUTES = "SYNC_INTERVAL_MINUTES";

    /// <summary>
    /// Имя параметра с названием проекта
    /// </summary>
    private const string PROJECT_TITLE = "PROJECT_TITLE";

    /// <summary>
    /// Имя параметра с заголовком колонки группы
    /// </summary>
    private const string GROUP_COLUMN = "GROUP_COLUMN";

    /// <summary>
    /// Имя параметра с часовым поясом таблицы
    /// </summary>
    private const string TIME_ZONE = "TIME_ZONE";

    /// <summary>
    /// Имя параметра с идентификатором таблицы
    /// </summary>
    private const string SPREADSHEET_ID = "SPREADSHEET_ID";

    /// <summary>
    /// Имя параметра с номером листа
    /// </summary>
    private const string SHEET_GID = "SHEET_GID";

    /// <summary>
    /// Имя параметра с ключом доступа
    /// </summary>
    private const string SINGULARITY_TOKEN = "SINGULARITY_TOKEN";

    /// <summary>
    /// Пауза между сверками, в минутах
    /// </summary>
    public int IntervalMinutes { get; init; } = DEFAULT_INTERVAL;

    /// <summary>
    /// Имя проекта в Singularity
    /// </summary>
    public string ProjectTitle { get; init; } = string.Empty;

    /// <summary>
    /// Заголовок колонки группы
    /// </summary>
    public string GroupColumn { get; init; } = string.Empty;

    /// <summary>
    /// Пояс, в котором в таблице указано время
    /// </summary>
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>
    /// Идентификатор Google Таблицы
    /// </summary>
    public string SpreadsheetId { get; init; } = string.Empty;

    /// <summary>
    /// Номер вкладки
    /// </summary>
    public string SheetGid { get; init; } = string.Empty;

    /// <summary>
    /// Ключ доступа к аккаунту
    /// </summary>
    public string Token { get; init; } = string.Empty;

    #region File

    /// <summary>
    /// Читает параметры из файла
    /// </summary>
    /// <param name="path">
    /// Полный путь к <c>.env</c>
    /// </param>
    /// <returns>
    /// Значения как в файле. Интервал приводится к допустимым границам
    /// </returns>
    public static SyncSettings Load(string path)
    {
        var file = EnvFile.Load(path);
        var minutes = int.TryParse(file.Get(SYNC_INTERVAL_MINUTES), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : DEFAULT_INTERVAL;
        return new SyncSettings
        {
            IntervalMinutes = Math.Clamp(minutes, MIN_INTERVAL, MAX_INTERVAL),
            ProjectTitle = file.Get(PROJECT_TITLE),
            GroupColumn = file.Get(GROUP_COLUMN),
            TimeZone = file.Get(TIME_ZONE),
            SpreadsheetId = file.Get(SPREADSHEET_ID),
            SheetGid = file.Get(SHEET_GID),
            Token = file.Get(SINGULARITY_TOKEN),
        };
    }

    /// <summary>
    /// Записывает параметры в файл, сохраняя остальные строки
    /// </summary>
    /// <param name="path">
    /// Полный путь к <c>.env</c>
    /// </param>
    public void Save(string path)
    {
        var file = EnvFile.Load(path);
        file.Set(SYNC_INTERVAL_MINUTES, IntervalMinutes.ToString(CultureInfo.InvariantCulture));
        file.Set(PROJECT_TITLE, ProjectTitle.Trim());
        file.Set(GROUP_COLUMN, GroupColumn.Trim());
        file.Set(TIME_ZONE, TimeZone.Trim());
        file.Set(SPREADSHEET_ID, SpreadsheetId.Trim());
        file.Set(SHEET_GID, SheetGid.Trim());
        file.Set(SINGULARITY_TOKEN, Token.Trim());
        file.Save();
    }

    #endregion File

    #region Checks

    /// <summary>
    /// Разбирает введённые минуты
    /// </summary>
    /// <param name="text">
    /// Текст числового поля
    /// </param>
    /// <param name="minutes">
    /// Целое число внутри границ
    /// </param>
    /// <returns>
    /// <see langword="false"/>, если текст не целое число минут от 1 до 180
    /// </returns>
    public static bool TryInterval(string text, out int minutes)
    {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out minutes))
            return false;

        if (minutes < MIN_INTERVAL || minutes > MAX_INTERVAL)
            return false;

        return true;
    }

    /// <summary>
    /// Проверяет, можно ли записать параметры
    /// </summary>
    /// <returns>
    /// Фраза для пользователя. Пусто, если записывать можно
    /// </returns>
    public string? Rejection()
    {
        if (ProjectTitle.Trim().Length == 0 || GroupColumn.Trim().Length == 0 || TimeZone.Trim().Length == 0
            || SpreadsheetId.Trim().Length == 0 || SheetGid.Trim().Length == 0)
            return "Заполните проект, колонку, пояс, таблицу и лист";

        if (Token.Trim().Length == 0)
            return "Введите токен Singularity. Без него сервис не войдёт в аккаунт";

        if (IntervalMinutes < MIN_INTERVAL || IntervalMinutes > MAX_INTERVAL)
            return "Интервал — целое число минут от 1 до 180";

        return null;
    }

    #endregion Checks
}
