namespace Steward.Schedule.Scheduling.Interfaces;

/// <summary>
/// Источник CSV расписания для одного прогона синхронизации
/// </summary>
public interface IScheduleSource
{
    /// <summary>
    /// Скачивает настроенный лист таблицы
    /// </summary>
    /// <param name="cancellationToken">Токен отмены загрузки</param>
    /// <returns>Текст CSV, включая строку заголовков</returns>
    Task<string> ReadCsvAsync(CancellationToken cancellationToken);
}
