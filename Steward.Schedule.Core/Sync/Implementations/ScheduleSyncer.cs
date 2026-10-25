using Microsoft.Extensions.Logging;
using Steward.Schedule.Common.Constants;
using Steward.Schedule.Common.Helpers;
using Steward.Schedule.Common.Models;
using Steward.Schedule.Scheduling.Implementations;
using Steward.Schedule.Scheduling.Interfaces;
using Steward.Schedule.Scheduling.Models;
using Steward.Schedule.Singularity.Implementations;
using Steward.Schedule.Singularity.Interfaces;
using Steward.Schedule.Singularity.Models;
using Steward.Schedule.Sync.Interfaces;
using Steward.Schedule.Sync.Models;
using System.Globalization;

namespace Steward.Schedule.Sync.Implementations;

/// <summary>
/// Копирует одну колонку группы из таблицы в проект Singularity
/// </summary>
public sealed class ScheduleSyncer
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
    private readonly ILogger<ScheduleSyncer> _logger;

    /// <summary>
    /// Создаёт прогон синхронизации
    /// </summary>
    /// <param name="source">Читает таблицу</param>
    /// <param name="client">Пишет задачи в Singularity</param>
    /// <param name="clock">Дата для выбора года и текущего момента</param>
    /// <param name="options">Колонка, проект и часовой пояс</param>
    /// <param name="logger">Принимает строку на каждое изменение и итоговую сводку</param>
    public ScheduleSyncer(
        IScheduleSource source,
        ISingularityClient client,
        IScheduleClock clock,
        SyncOptions options,
        ILogger<ScheduleSyncer> logger)
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
    /// <param name="cancellationToken">Отменяет прогон</param>
    /// <returns>Счётчики каждого действия. Уже выполненная задача прошедшего занятия не меняется</returns>
    public async Task<SyncReport> RunAsync(CancellationToken cancellationToken)
    {
        var csv = await _source.ReadCsvAsync(cancellationToken);
        var slots = ScheduleParser.Parse(csv, _options.GroupColumn, TimeHelper.ResolveTimeZone(_options.TimeZoneId), _clock.Today);
        _logger.LogInformation(
            "Parsed {Lessons} lessons across {Slots} slots in column {Group}",
            slots.Count(slot => slot.Lesson is not null),
            slots.Count,
            _options.GroupColumn);

        var projectId = await _client.GetOrCreateProjectIdAsync(_options.ProjectTitle, cancellationToken);
        var (byExternalId, targetTaskIds) = await LoadTasksAsync(projectId, cancellationToken);
        var run = new SyncRun(
            projectId,
            targetTaskIds,
            RemovalNotices(slots, byExternalId, _clock.Now),
            _options.SubjectTags ? await ResolveSubjectTagsAsync(slots, cancellationToken) : new Dictionary<string, string>(StringComparer.Ordinal));

        foreach (var slot in slots)
        {
            byExternalId.TryGetValue(slot.ExternalId, out var existing);

            if (slot.Lesson is not { } lesson)
                await ReconcileRemovedAsync(slot, existing, run, cancellationToken);
            else if (lesson.Start.AddMinutes(lesson.TimeLengthMinutes) <= _clock.Now)
                await ReconcilePastAsync(slot, lesson, existing, run, cancellationToken);
            else
                await ReconcileFutureAsync(slot, lesson, existing, run, cancellationToken);
        }

        var report = run.ToReport();
        _logger.LogInformation(
            SyncLogMessages.FINISHED + "created={Created} updated={Updated} unarchived={Unarchived} completed={Completed} skipped={Skipped} deleted={Deleted}",
            report.Created,
            report.Updated,
            report.Unarchived,
            report.Completed,
            report.Skipped,
            report.Deleted);

        return report;
    }

    #region Loading

    /// <summary>
    /// Собирает задачи проекта и, если проект переименовали, задачи из проекта со старым названием
    /// </summary>
    /// <param name="projectId">Проект, в который пишутся задачи</param>
    /// <param name="cancellationToken">Отменяет прогон</param>
    /// <returns>Задачи этой группы по внешнему идентификатору и идентификаторы задач, которые уже лежат в целевом проекте</returns>
    private async Task<(Dictionary<string, SingularityTask> ByExternalId, HashSet<string> TargetTaskIds)> LoadTasksAsync(
        string projectId,
        CancellationToken cancellationToken)
    {
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

        return (byExternalId, targetTaskIds);
    }

    #endregion Loading

    #region Reconciliation

    /// <summary>
    /// Сверяет занятие, которое уже закончилось: создаёт задачу, если она нужна на этой неделе, обновляет текст и отмечает выполненной
    /// </summary>
    /// <param name="slot">Слот таблицы</param>
    /// <param name="lesson">Занятие слота</param>
    /// <param name="existing">Сохранённая задача или <see langword="null"/></param>
    /// <param name="run">Контекст и счётчики прогона</param>
    /// <param name="cancellationToken">Отменяет прогон</param>
    private async Task ReconcilePastAsync(ScheduleSlot slot, Lesson lesson, SingularityTask? existing, SyncRun run, CancellationToken cancellationToken)
    {
        if (existing is null)
        {
            if (!IsCurrentWeek(lesson.Start, _clock.Now))
                return;

            await CompleteAsync(await CreateAsync(slot, lesson, run, cancellationToken), SyncLogMessages.COMPLETED_PAST, slot.ExternalId, run, cancellationToken);

            return;
        }

        if (existing.IsCompleted)
        {
            run.Skipped++;
            _logger.LogInformation(SyncLogMessages.SKIPPED_COMPLETED + "{ExternalId}", slot.ExternalId);

            return;
        }

        await UnarchiveIfNeededAsync(existing, "past lesson ", slot.ExternalId, run, cancellationToken);
        await UpdateIfNeededAsync(existing, slot, lesson, "past lesson ", run, cancellationToken);
        await CompleteAsync(existing.Id, SyncLogMessages.COMPLETED_PAST, slot.ExternalId, run, cancellationToken);
    }

    /// <summary>
    /// Сверяет опустевшую ячейку: задача занятия, которое убрали из таблицы, отмечается выполненной
    /// </summary>
    /// <param name="slot">Слот таблицы без занятия</param>
    /// <param name="existing">Сохранённая задача или <see langword="null"/></param>
    /// <param name="run">Контекст и счётчики прогона</param>
    /// <param name="cancellationToken">Отменяет прогон</param>
    private async Task ReconcileRemovedAsync(ScheduleSlot slot, SingularityTask? existing, SyncRun run, CancellationToken cancellationToken)
    {
        if (existing is null)
            return;

        if (existing.IsCompleted)
        {
            run.Skipped++;
            _logger.LogInformation("Skipped completed removed lesson {ExternalId}", slot.ExternalId);

            return;
        }

        await UnarchiveIfNeededAsync(existing, "removed lesson ", slot.ExternalId, run, cancellationToken);
        await CompleteAsync(existing.Id, SyncLogMessages.COMPLETED_REMOVED, slot.ExternalId, run, cancellationToken);
    }

    /// <summary>
    /// Сверяет занятие, которое ещё не закончилось: создаёт или обновляет задачу. Выполненная задача такого занятия удаляется и создаётся заново
    /// </summary>
    /// <param name="slot">Слот таблицы</param>
    /// <param name="lesson">Занятие слота</param>
    /// <param name="existing">Сохранённая задача или <see langword="null"/></param>
    /// <param name="run">Контекст и счётчики прогона</param>
    /// <param name="cancellationToken">Отменяет прогон</param>
    private async Task ReconcileFutureAsync(ScheduleSlot slot, Lesson lesson, SingularityTask? existing, SyncRun run, CancellationToken cancellationToken)
    {
        if (existing is { IsCompleted: true })
        {
            await _client.DeleteTaskAsync(existing.Id, cancellationToken);
            run.Deleted++;
            _logger.LogInformation(SyncLogMessages.DELETED_FUTURE + "{ExternalId}", slot.ExternalId);
            existing = null;
        }

        if (existing is null)
        {
            await CreateAsync(slot, lesson, run, cancellationToken);

            return;
        }

        await UnarchiveIfNeededAsync(existing, string.Empty, slot.ExternalId, run, cancellationToken);
        await UpdateIfNeededAsync(existing, slot, lesson, string.Empty, run, cancellationToken);
    }

    /// <summary>
    /// Создаёт задачу занятия вместе с пометками и тегом предмета
    /// </summary>
    /// <param name="slot">Слот таблицы</param>
    /// <param name="lesson">Занятие слота</param>
    /// <param name="run">Контекст и счётчики прогона</param>
    /// <param name="cancellationToken">Отменяет прогон</param>
    /// <returns>Идентификатор новой задачи</returns>
    private async Task<string> CreateAsync(ScheduleSlot slot, Lesson lesson, SyncRun run, CancellationToken cancellationToken)
    {
        var taskId = await _client.CreateTaskAsync(run.ProjectId, WithTags(WithNotices(lesson, run.Notices), null, run.SubjectTags), cancellationToken);
        run.Created++;
        _logger.LogInformation(SyncLogMessages.CREATED + "{ExternalId} {Title}", slot.ExternalId, lesson.Title);

        return taskId;
    }

    /// <summary>
    /// Обновляет задачу, если она лежит не в целевом проекте или её поля разошлись с таблицей
    /// </summary>
    /// <param name="existing">Сохранённая задача</param>
    /// <param name="slot">Слот таблицы</param>
    /// <param name="lesson">Занятие слота</param>
    /// <param name="kind">Пояснение для журнала: «past lesson » у прошедшего занятия или пустая строка</param>
    /// <param name="run">Контекст и счётчики прогона</param>
    /// <param name="cancellationToken">Отменяет прогон</param>
    private async Task UpdateIfNeededAsync(SingularityTask existing, ScheduleSlot slot, Lesson lesson, string kind, SyncRun run, CancellationToken cancellationToken)
    {
        var target = WithTags(WithNotices(lesson, run.Notices), existing, run.SubjectTags);
        if (run.TargetTaskIds.Contains(existing.Id) && !SingularityClient.NeedsUpdate(existing, target))
            return;

        await _client.UpdateTaskAsync(existing.Id, run.ProjectId, target, cancellationToken);
        run.Updated++;
        _logger.LogInformation(SyncLogMessages.UPDATED + "{Kind}{ExternalId} {Title}", kind, slot.ExternalId, lesson.Title);
    }

    /// <summary>
    /// Возвращает задачу из архива, если она там лежит
    /// </summary>
    /// <param name="existing">Сохранённая задача</param>
    /// <param name="kind">Пояснение для журнала: «past lesson », «removed lesson » или пустая строка</param>
    /// <param name="externalId">Внешний идентификатор занятия</param>
    /// <param name="run">Контекст и счётчики прогона</param>
    /// <param name="cancellationToken">Отменяет прогон</param>
    private async Task UnarchiveIfNeededAsync(SingularityTask existing, string kind, string externalId, SyncRun run, CancellationToken cancellationToken)
    {
        if (!existing.IsArchived)
            return;

        await _client.UnarchiveTaskAsync(existing.Id, cancellationToken);
        run.Unarchived++;
        _logger.LogInformation(SyncLogMessages.UNARCHIVED + "{Kind}{ExternalId}", kind, externalId);
    }

    /// <summary>
    /// Отмечает задачу выполненной
    /// </summary>
    /// <param name="taskId">Идентификатор задачи</param>
    /// <param name="message">Начало строки журнала из <see cref="SyncLogMessages"/></param>
    /// <param name="externalId">Внешний идентификатор занятия</param>
    /// <param name="run">Контекст и счётчики прогона</param>
    /// <param name="cancellationToken">Отменяет прогон</param>
    private async Task CompleteAsync(string taskId, string message, string externalId, SyncRun run, CancellationToken cancellationToken)
    {
        await _client.CompleteTaskAsync(taskId, cancellationToken);
        run.Completed++;
        _logger.LogInformation("{Message}{ExternalId}", message, externalId);
    }

    #endregion Reconciliation
    #region Removal notices

    /// <summary>
    /// Сопоставляет опустевшей ячейке занятие, в заметку которого нужно дописать перенос или отмену
    /// </summary>
    /// <param name="slots">Слоты текущего листа</param>
    /// <param name="byExternalId">Уже сохранённые задачи группы</param>
    /// <param name="now">Текущий момент. По нему видно, какое выполненное занятие этот прогон создаст заново</param>
    /// <returns>Строки заметки по внешнему идентификатору занятия, которое их получит</returns>
    private static Dictionary<string, List<string>> RemovalNotices(
        IReadOnlyList<ScheduleSlot> slots,
        Dictionary<string, SingularityTask> byExternalId,
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
    /// <param name="live">Занятия, которые ещё есть в таблице</param>
    /// <param name="byExternalId">Сохранённые задачи. Выполненная задача прошедшего занятия не получает новую пометку</param>
    /// <param name="title">Название удалённого занятия</param>
    /// <param name="removedStart">Начало удалённого занятия</param>
    /// <param name="now">Текущий момент</param>
    /// <returns>
    /// Занятие для пометки или <see langword="null"/>, если писать некуда
    /// </returns>
    private static Lesson? NoticeTarget(
        IReadOnlyList<Lesson> live,
        Dictionary<string, SingularityTask> byExternalId,
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
    /// <param name="lesson">Занятие из таблицы</param>
    /// <param name="byExternalId">Сохранённые задачи</param>
    /// <param name="now">Текущий момент. Выполненная задача занятия, которое ещё не закончилось, будет создана заново в этом прогоне</param>
    /// <returns>
    /// <see langword="true"/>, если задачи нет, она не выполнена или этот прогон создаст её заново
    /// </returns>
    private static bool Open(
        Lesson lesson,
        Dictionary<string, SingularityTask> byExternalId,
        DateTimeOffset now) =>
        !byExternalId.TryGetValue(lesson.ExternalId, out var task)
        || !task.IsCompleted
        || lesson.Start.AddMinutes(lesson.TimeLengthMinutes) > now;

    /// <summary>
    /// Добавляет в начало заметки строки об отмене и переносе, не трогая адрес в конце
    /// </summary>
    /// <param name="lesson">Занятие из таблицы</param>
    /// <param name="notices">Строки по внешнему идентификатору</param>
    /// <returns>То же занятие, если пометок нет. Иначе копия с дополненной заметкой</returns>
    private static Lesson WithNotices(Lesson lesson, Dictionary<string, List<string>> notices)
    {
        if (!notices.TryGetValue(lesson.ExternalId, out var lines) || lines.Count == 0)
            return lesson;

        var block = string.Join('\n', lines.Distinct(StringComparer.Ordinal));

        var note = lesson.Note.Length == 0 ? block : block + "\n" + lesson.Note;
        return lesson with { Note = note };
    }

    #endregion Removal notices

    #region Subject tags

    /// <summary>
    /// Находит или создаёт по одному тегу на каждый предмет таблицы
    /// </summary>
    /// <param name="slots">Слоты текущего листа</param>
    /// <param name="cancellationToken">Отменяет прогон</param>
    /// <returns>Идентификаторы тегов по названию предмета. Пусто, если токену не хватает доступа к тегам: прогон тогда идёт без тегов</returns>
    private async Task<Dictionary<string, string>> ResolveSubjectTagsAsync(
        IReadOnlyList<ScheduleSlot> slots,
        CancellationToken cancellationToken)
    {
        var tagIds = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var subject in slots.Where(slot => slot.Lesson is not null).Select(slot => slot.Lesson!.Subject).Distinct(StringComparer.Ordinal))
                tagIds[subject] = await _client.GetOrCreateTagIdAsync(subject, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning("Subject tags skipped for this run: {Reason}", exception.Message);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return tagIds;
    }

    /// <summary>
    /// Добавляет к занятию тег его предмета, сохраняя теги, которые пользователь уже поставил на задачу
    /// </summary>
    /// <param name="lesson">Занятие из таблицы</param>
    /// <param name="existing">
    /// Сохранённая задача или <see langword="null"/>, если задачу ещё создают
    /// </param>
    /// <param name="subjectTags">Идентификаторы тегов по названию предмета</param>
    /// <returns>То же занятие, если тега для предмета нет. Иначе копия с тегами задачи и тегом предмета</returns>
    private static Lesson WithTags(Lesson lesson, SingularityTask? existing, Dictionary<string, string> subjectTags)
    {
        if (!subjectTags.TryGetValue(lesson.Subject, out var tagId))
            return lesson;

        var tags = (existing?.Tags ?? []).Append(tagId).Distinct(StringComparer.Ordinal).ToArray();
        return lesson with { TagIds = tags };
    }

    #endregion Subject tags

    #region Week

    /// <summary>
    /// Сообщает, попадает ли день занятия в текущую календарную неделю
    /// </summary>
    /// <param name="start">Начало занятия в часовом поясе расписания</param>
    /// <param name="now">Текущий момент в том же поясе</param>
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
