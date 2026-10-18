using Microsoft.Extensions.Logging;
using System.Globalization;
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
    /// Дата для выбора года и текущего момента
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
    /// Дата для выбора года и текущего момента
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
    /// Читает таблицу и создаёт, обновляет, возвращает из архива, отмечает выполненными или удаляет подходящие задачи
    /// </summary>
    /// <param name="cancellationToken">
    /// Отменяет прогон
    /// </param>
    /// <returns>
    /// Счётчики каждого действия. Уже выполненная задача прошедшего занятия не меняется
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
        var deleted = 0;
        var notices = RemovalNotices(slots, byExternalId, _clock.Now);

        foreach (var slot in slots)
        {
            byExternalId.TryGetValue(slot.ExternalId, out var existing);
            if (slot.Lesson is not null && slot.Lesson.Start.AddMinutes(slot.Lesson.TimeLengthMinutes) <= _clock.Now)
            {
                if (existing is null)
                {
                    if (!IsCurrentWeek(slot.Lesson.Start, _clock.Now))
                        continue;

                    var taskId = await _client.CreateTaskAsync(projectId, WithNotices(slot.Lesson, notices), cancellationToken);
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

                if (existing.IsArchived)
                {
                    await _client.UnarchiveTaskAsync(existing.Id, cancellationToken);
                    unarchived++;
                    _logger.LogInformation("Unarchived past lesson {ExternalId}", slot.ExternalId);
                }

                var pastLesson = WithNotices(slot.Lesson, notices);
                if (!targetTaskIds.Contains(existing.Id) || SingularityClient.NeedsUpdate(existing, pastLesson))
                {
                    await _client.UpdateTaskAsync(existing.Id, projectId, pastLesson, cancellationToken);
                    updated++;
                    _logger.LogInformation("Updated past lesson {ExternalId} {Title}", slot.ExternalId, slot.Lesson.Title);
                }

                await _client.CompleteTaskAsync(existing.Id, cancellationToken);
                completed++;
                _logger.LogInformation("Completed past lesson {ExternalId}", slot.ExternalId);
                continue;
            }

            if (slot.Lesson is null)
            {
                if (existing is null)
                    continue;

                if (existing.IsCompleted)
                {
                    skipped++;
                    _logger.LogInformation("Skipped completed removed lesson {ExternalId}", slot.ExternalId);
                    continue;
                }

                if (existing.IsArchived)
                {
                    await _client.UnarchiveTaskAsync(existing.Id, cancellationToken);
                    unarchived++;
                    _logger.LogInformation("Unarchived removed lesson {ExternalId}", slot.ExternalId);
                }

                await _client.CompleteTaskAsync(existing.Id, cancellationToken);
                completed++;
                _logger.LogInformation("Completed removed lesson {ExternalId}", slot.ExternalId);
                continue;
            }

            if (existing is not null && existing.IsCompleted)
            {
                await _client.DeleteTaskAsync(existing.Id, cancellationToken);
                deleted++;
                _logger.LogInformation("Deleted completed future task {ExternalId}", slot.ExternalId);
                existing = null;
            }

            if (existing is null)
            {
                await _client.CreateTaskAsync(projectId, WithNotices(slot.Lesson, notices), cancellationToken);
                created++;
                _logger.LogInformation("Created {ExternalId} {Title}", slot.ExternalId, slot.Lesson.Title);
                continue;
            }

            if (existing.IsArchived)
            {
                await _client.UnarchiveTaskAsync(existing.Id, cancellationToken);
                unarchived++;
                _logger.LogInformation("Unarchived {ExternalId}", slot.ExternalId);
            }

            var lesson = WithNotices(slot.Lesson, notices);
            if (!targetTaskIds.Contains(existing.Id) || SingularityClient.NeedsUpdate(existing, lesson))
            {
                await _client.UpdateTaskAsync(existing.Id, projectId, lesson, cancellationToken);
                updated++;
                _logger.LogInformation("Updated {ExternalId} {Title}", slot.ExternalId, slot.Lesson.Title);
            }
        }

        var report = new SyncReport(created, updated, archived, unarchived, completed, skipped, deleted);
        _logger.LogInformation(
            "Sync finished: created={Created} updated={Updated} archived={Archived} unarchived={Unarchived} completed={Completed} skipped={Skipped} deleted={Deleted}",
            report.Created,
            report.Updated,
            report.Archived,
            report.Unarchived,
            report.Completed,
            report.Skipped,
            report.Deleted);
        return report;

        #endregion Reconciliation
    }

    #region Removal notices

    /// <summary>
    /// Сопоставляет опустевшей ячейке занятие, в заметку которого нужно дописать перенос или отмену
    /// </summary>
    /// <param name="slots">
    /// Слоты текущего листа
    /// </param>
    /// <param name="byExternalId">
    /// Уже сохранённые задачи группы
    /// </param>
    /// <param name="now">
    /// Текущий момент. По нему видно, какое выполненное занятие этот прогон создаст заново
    /// </param>
    /// <returns>
    /// Строки заметки по внешнему идентификатору занятия, которое их получит
    /// </returns>
    private static Dictionary<string, List<string>> RemovalNotices(
        IReadOnlyList<Scheduling.Models.ScheduleSlot> slots,
        Dictionary<string, Singularity.Models.SingularityTask> byExternalId,
        DateTimeOffset now)
    {
        var live = slots
            .Where(slot => slot.Lesson is not null)
            .Select(slot => slot.Lesson!)
            .OrderBy(lesson => lesson.Start)
            .ToArray();
        var notices = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var slot in slots)
        {
            if (slot.Lesson is not null
                || !byExternalId.TryGetValue(slot.ExternalId, out var existing)
                || string.IsNullOrWhiteSpace(existing.Title)
                || !DateTimeOffset.TryParse(existing.Start, CultureInfo.InvariantCulture, DateTimeStyles.None, out var removedStart))
            {
                continue;
            }

            var target = NoticeTarget(live, byExternalId, existing.Title, removedStart, now);
            if (target is null)
                continue;

            if (!notices.TryGetValue(target.ExternalId, out var lines))
            {
                lines = [];
                notices[target.ExternalId] = lines;
            }

            var sameDay = string.Equals(target.Title, existing.Title, StringComparison.Ordinal)
                && DateOnly.FromDateTime(target.Start.Date) == DateOnly.FromDateTime(removedStart.Date);
            lines.Add(sameDay
                ? $"Перенос с {removedStart:dd.MM HH:mm}"
                : $"Отмена или перенос: {existing.Title}, {removedStart:dd.MM HH:mm}");
        }

        return notices;
    }

    /// <summary>
    /// Ищет открытое занятие того же предмета в этот день, иначе следующую пару
    /// </summary>
    /// <param name="live">
    /// Занятия, которые ещё есть в таблице
    /// </param>
    /// <param name="byExternalId">
    /// Сохранённые задачи. Выполненная задача прошедшего занятия не получает новую пометку
    /// </param>
    /// <param name="title">
    /// Название удалённого занятия
    /// </param>
    /// <param name="removedStart">
    /// Начало удалённого занятия
    /// </param>
    /// <param name="now">
    /// Текущий момент
    /// </param>
    /// <returns>
    /// Занятие для пометки или <see langword="null"/>, если писать некуда
    /// </returns>
    private static Lesson? NoticeTarget(
        IReadOnlyList<Lesson> live,
        Dictionary<string, Singularity.Models.SingularityTask> byExternalId,
        string title,
        DateTimeOffset removedStart,
        DateTimeOffset now)
    {
        var day = DateOnly.FromDateTime(removedStart.Date);
        var sameDay = live
            .Where(lesson => Open(lesson, byExternalId, now)
                && string.Equals(lesson.Title, title, StringComparison.Ordinal)
                && DateOnly.FromDateTime(lesson.Start.Date) == day
                && lesson.Start != removedStart)
            .OrderBy(lesson => Math.Abs((lesson.Start - removedStart).Ticks))
            .FirstOrDefault();
        if (sameDay is not null)
            return sameDay;

        return live.FirstOrDefault(lesson => Open(lesson, byExternalId, now) && lesson.Start > removedStart);
    }

    /// <summary>
    /// Сообщает, что занятие ещё можно дополнить пометкой
    /// </summary>
    /// <param name="lesson">
    /// Занятие из таблицы
    /// </param>
    /// <param name="byExternalId">
    /// Сохранённые задачи
    /// </param>
    /// <param name="now">
    /// Текущий момент. Выполненная задача занятия, которое ещё не закончилось, будет создана заново в этом прогоне
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если задачи нет, она не выполнена или этот прогон создаст её заново
    /// </returns>
    private static bool Open(
        Lesson lesson,
        Dictionary<string, Singularity.Models.SingularityTask> byExternalId,
        DateTimeOffset now) =>
        !byExternalId.TryGetValue(lesson.ExternalId, out var task)
        || !task.IsCompleted
        || lesson.Start.AddMinutes(lesson.TimeLengthMinutes) > now;

    /// <summary>
    /// Добавляет в начало заметки строки об отмене и переносе, не трогая адрес в конце
    /// </summary>
    /// <param name="lesson">
    /// Занятие из таблицы
    /// </param>
    /// <param name="notices">
    /// Строки по внешнему идентификатору
    /// </param>
    /// <returns>
    /// То же занятие, если пометок нет. Иначе копия с дополненной заметкой
    /// </returns>
    private static Lesson WithNotices(Lesson lesson, Dictionary<string, List<string>> notices)
    {
        if (!notices.TryGetValue(lesson.ExternalId, out var lines) || lines.Count == 0)
            return lesson;

        var block = string.Join('\n', lines.Distinct(StringComparer.Ordinal));
        var note = lesson.Note.Length == 0 ? block : block + "\n" + lesson.Note;
        return lesson with { Note = note };
    }

    #endregion Removal notices

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
