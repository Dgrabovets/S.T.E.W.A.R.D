using Steward.Schedule.Scheduling.Models;

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

    /// <summary>
    /// Скачивает вкладки месяцев, которые нужны сегодня. Источник с одним листом возвращает его как единственный
    /// </summary>
    /// <param name="today">Сегодняшняя дата в часовом поясе таблицы: по ней выбираются вкладки месяцев</param>
    /// <param name="cancellationToken">Токен отмены загрузки</param>
    /// <returns>Вкладки от старой к новой, у каждой CSV вместе со строкой заголовков</returns>
    async Task<IReadOnlyList<ScheduleSheet>> ReadSheetsAsync(DateOnly today, CancellationToken cancellationToken) =>
        [new ScheduleSheet(string.Empty, null, await ReadCsvAsync(cancellationToken))];
}
