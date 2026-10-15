using Microsoft.Extensions.Logging;
using UltimateParser.Common.Helpers;
using UltimateParser.Common.Models;
using UltimateParser.Scheduling.Implementations;
using UltimateParser.Scheduling.Interfaces;
using UltimateParser.Singularity.Implementations;
using UltimateParser.Singularity.Interfaces;
using UltimateParser.Sync.Interfaces;
using UltimateParser.Sync.Models;

namespace UltimateParser.Sync.Implementations;

/// <summary>
/// Копирует одну колонку группы из таблицы в проект Singularity
/// </summary>
public sealed class ScheduleSync
{
    /// <summary>
    /// Читает таблицу
    /// </summary>
    private readonly IScheduleSource _source;

    /// <summary>
    /// Пишет задачи в Singularity
    /// </summary>
    private readonly ISingularityClient _client;

    /// <summary>
    /// Дата для выбора года и отметки архивации
    /// </summary>
    private readonly IScheduleClock _clock;

    /// <summary>
    /// Колонка, проект и часовой пояс
    /// </summary>
    private readonly SyncOptions _options;

    /// <summary>
    /// Принимает строку на каждое изменение и итоговую сводку
    /// </summary>
    private readonly ILogger<ScheduleSync> _logger;

    /// <summary>
    /// Создаёт прогон синхронизации
    /// </summary>
    /// <param name="source">
    /// Читает таблицу
    /// </param>
    /// <param name="client">
    /// Пишет задачи в Singularity
    /// </param>
    /// <param name="clock">
    /// Дата для выбора года и отметки архивации
    /// </param>
    /// <param name="options">
    /// Колонка, проект и часовой пояс
    /// </param>
    /// <param name="logger">
    /// Принимает строку на каждое изменение и итоговую сводку
    /// </param>
    public ScheduleSync(
        IScheduleSource source,
        ISingularityClient client,
        IScheduleClock clock,
        SyncOptions options,
        ILogger<ScheduleSync> logger)
    {
        _source = source;
        _client = client;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Читает таблицу и создаёт, обновляет, архивирует, возвращает из архива или отмечает выполненными подходящие задачи
    /// </summary>
    /// <param name="cancellationToken">
    /// Отменяет прогон
    /// </param>
    /// <returns>
    /// Счётчики каждого действия. Выполненные задачи считаются пропущенными и не меняются
    /// </returns>
    public async Task<SyncReport> RunAsync(CancellationToken cancellationToken)
    {
        #region Load

        var csv = await _source.ReadCsvAsync(cancellationToken);
        var timeZone = TimeHelper.ResolveTimeZone(_options.TimeZoneId);
        var slots = ScheduleParser.Parse(csv, _options.GroupColumn, timeZone, _clock.Today);
        var lessons = slots.Count(slot => slot.Lesson is not null);
        _logger.LogInformation(
            "Parsed {Lessons} lessons across {Slots} slots in column {Group}",
            lessons,
            slots.Count,
            _options.GroupColumn);

        var projectId = await _client.GetOrCreateProjectIdAsync(_options.ProjectTitle, cancellationToken);
        var tasks = await _client.ListProjectTasksAsync(projectId, cancellationToken);
        var targetTaskIds = tasks.Select(task => task.Id).ToHashSet(StringComparer.Ordinal);
        if (!string.Equals(_options.ProjectTitle, _options.GroupColumn, StringComparison.Ordinal))
        {
            var previousProjectId = await _client.FindProjectIdAsync(_options.GroupColumn, cancellationToken);
            if (previousProjectId is not null && previousProjectId != projectId)
            {
                var previousTasks = await _client.ListProjectTasksAsync(previousProjectId, cancellationToken);
                tasks = tasks.Concat(previousTasks.Where(task => !targetTaskIds.Contains(task.Id))).ToArray();
            }
        }

        var prefix = ScheduleParser.ExternalIdPrefix(_options.GroupColumn);
        var byExternalId = tasks
            .Where(task => task.ExternalId is not null
                && task.ExternalId.StartsWith(prefix, StringComparison.Ordinal)
                && !task.Removed)
            .GroupBy(task => task.ExternalId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(task => targetTaskIds.Contains(task.Id) ? 0 : 1)
                    .ThenBy(task => task.IsArchived)
                    .ThenBy(task => task.Id, StringComparer.Ordinal)
                    .First(),
                StringComparer.Ordinal);

        #endregion Load

        #region Reconciliation

        var created = 0;
        var updated = 0;
        var archived = 0;
        var unarchived = 0;
        var completed = 0;
        var skipped = 0;

        foreach (var slot in slots)
        {
            byExternalId.TryGetValue(slot.ExternalId, out var existing);
            if (slot.Lesson is not null && slot.Lesson.Start.AddMinutes(slot.Lesson.TimeLengthMinutes) <= _clock.Now)
            {
                if (existing is null)
                {
                    if (!IsCurrentWeek(slot.Lesson.Start, _clock.Now))
                        continue;

                    var taskId = await _client.CreateTaskAsync(projectId, slot.Lesson, cancellationToken);
                    created++;
                    _logger.LogInformation("Created {ExternalId} {Title}", slot.ExternalId, slot.Lesson.Title);
                    await _client.CompleteTaskAsync(taskId, cancellationToken);
                    completed++;
                    _logger.LogInformation("Completed past lesson {ExternalId}", slot.ExternalId);
                    continue;
                }

                if (existing.IsCompleted)
                {
                    skipped++;
                    _logger.LogInformation("Skipped completed task {ExternalId}", slot.ExternalId);
                    continue;
                }

                await _client.CompleteTaskAsync(existing.Id, cancellationToken);
                completed++;
                _logger.LogInformation("Completed past lesson {ExternalId}", slot.ExternalId);
                continue;
            }

            if (slot.Lesson is null)
            {
                if (existing is null || existing.IsArchived)
                    continue;

                if (existing.IsCompleted)
                {
                    skipped++;
                    _logger.LogInformation("Skipped completed task {ExternalId}", slot.ExternalId);
                    continue;
                }

                await _client.ArchiveTaskAsync(existing.Id, _clock.Today, cancellationToken);
                archived++;
                _logger.LogInformation("Archived {ExternalId}", slot.ExternalId);
                continue;
            }

            if (existing is null)
            {
                await _client.CreateTaskAsync(projectId, slot.Lesson, cancellationToken);
                created++;
                _logger.LogInformation("Created {ExternalId} {Title}", slot.ExternalId, slot.Lesson.Title);
                continue;
            }

            if (existing.IsCompleted)
            {
                skipped++;
                _logger.LogInformation("Skipped completed task {ExternalId}", slot.ExternalId);
                continue;
            }

            if (existing.IsArchived)
            {
                await _client.UnarchiveTaskAsync(existing.Id, cancellationToken);
                unarchived++;
                _logger.LogInformation("Unarchived {ExternalId}", slot.ExternalId);
            }

            if (!targetTaskIds.Contains(existing.Id) || SingularityClient.NeedsUpdate(existing, slot.Lesson))
            {
                await _client.UpdateTaskAsync(existing.Id, projectId, slot.Lesson, cancellationToken);
                updated++;
                _logger.LogInformation("Updated {ExternalId} {Title}", slot.ExternalId, slot.Lesson.Title);
            }
        }

        var report = new SyncReport(created, updated, archived, unarchived, completed, skipped);
        _logger.LogInformation(
            "Sync finished: created={Created} updated={Updated} archived={Archived} unarchived={Unarchived} completed={Completed} skipped={Skipped}",
            report.Created,
            report.Updated,
            report.Archived,
            report.Unarchived,
            report.Completed,
            report.Skipped);
        return report;

        #endregion Reconciliation
    }

    #region Week

    /// <summary>
    /// Сообщает, попадает ли день занятия в текущую календарную неделю
    /// </summary>
    /// <param name="start">
    /// Начало занятия в часовом поясе расписания
    /// </param>
    /// <param name="now">
    /// Текущий момент в том же поясе
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если день занятия лежит между понедельником и воскресеньем этой недели
    /// </returns>
    private static bool IsCurrentWeek(DateTimeOffset start, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.Date);
        var lessonDay = DateOnly.FromDateTime(start.Date);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return lessonDay >= monday && lessonDay <= monday.AddDays(6);
    }

    #endregion Week
}
