using Steward.Monitoring.Implementations;
using Steward.Schedule.Common.Constants;
using System.Globalization;

namespace Steward.Models;

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
    /// Начальное положение ползунка до чтения файла
    /// </summary>
    public const int DEFAULT_INTERVAL = 5;

    /// <summary>
    /// Пауза между сверками, в минутах. Ноль, если в файле нет целого числа
    /// </summary>
    public int IntervalMinutes { get; init; }

    /// <summary>
    /// Текст интервала ровно как в файле
    /// </summary>
    public string IntervalText { get; init; } = string.Empty;

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
    /// <returns>Значения как в файле. Недостающий или нецелый интервал не подменяется</returns>
    public static SyncSettings Load(string path)
    {
        var file = EnvFile.Load(path);
        var intervalText = file.Get(EnviromentalParamsNames.SYNC_INTERVAL_MINUTES).Trim();
        var minutes = int.TryParse(intervalText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed
                        : 0;

        return new SyncSettings
        {
            IntervalText = intervalText,
            IntervalMinutes = minutes,
            ProjectTitle = file.Get(EnviromentalParamsNames.PROJECT_TITLE),
            GroupColumn = file.Get(EnviromentalParamsNames.GROUP_COLUMN),
            TimeZone = file.Get(EnviromentalParamsNames.TIME_ZONE),
            SpreadsheetId = file.Get(EnviromentalParamsNames.SPREADSHEET_ID),
            SheetGid = file.Get(EnviromentalParamsNames.SHEET_GID),
            Token = file.Get(EnviromentalParamsNames.SINGULARITY_TOKEN),
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
        file.Set(EnviromentalParamsNames.SYNC_INTERVAL_MINUTES, IntervalMinutes.ToString(CultureInfo.InvariantCulture));
        file.Set(EnviromentalParamsNames.PROJECT_TITLE, ProjectTitle.Trim());
        file.Set(EnviromentalParamsNames.GROUP_COLUMN, GroupColumn.Trim());
        file.Set(EnviromentalParamsNames.TIME_ZONE, TimeZone.Trim());
        file.Set(EnviromentalParamsNames.SPREADSHEET_ID, SpreadsheetId.Trim());
        file.Set(EnviromentalParamsNames.SHEET_GID, SheetGid.Trim());
        file.Set(EnviromentalParamsNames.SINGULARITY_TOKEN, Token.Trim());
        file.Save();
    }

    #endregion File

    #region Checks

    /// <summary>
    /// Разбирает введённые минуты
    /// </summary>
    /// <param name="text">Текст числового поля</param>
    /// <param name="minutes">Целое число внутри границ</param>
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
    /// <returns>Фраза для пользователя. Пусто, если записывать можно</returns>
    public string? Rejection()
    {
        if (HasLineBreak(ProjectTitle) || HasLineBreak(GroupColumn) || HasLineBreak(TimeZone)
            || HasLineBreak(SpreadsheetId) || HasLineBreak(SheetGid) || HasLineBreak(Token))
            return "В значениях не должно быть переноса строки";

        if (ProjectTitle.Trim().Length == 0 || GroupColumn.Trim().Length == 0 || TimeZone.Trim().Length == 0
            || SpreadsheetId.Trim().Length == 0 || SheetGid.Trim().Length == 0)
            return "Заполните проект, колонку, пояс, таблицу и лист";

        if (Token.Trim().Length == 0)
            return "Введите токен Singularity. Без него сервис не войдёт в аккаунт";

        if (IntervalMinutes < MIN_INTERVAL || IntervalMinutes > MAX_INTERVAL)
            return "Интервал — целое число минут от 1 до 180";

        return null;
    }

    /// <summary>
    /// Сообщает, что в тексте есть перевод строки
    /// </summary>
    /// <param name="value">Поле, которое попадёт в файл настроек</param>
    /// <returns>
    /// <see langword="true"/>, если значение разбило бы файл на несколько строк
    /// </returns>
    private static bool HasLineBreak(string value) => value.Contains('\r') || value.Contains('\n');

    #endregion Checks
}
