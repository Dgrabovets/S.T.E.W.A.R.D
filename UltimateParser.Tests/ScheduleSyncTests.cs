using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using UltimateParser.Common.Models;
using UltimateParser.Scheduling.Interfaces;
using UltimateParser.Singularity.Implementations;
using UltimateParser.Singularity.Interfaces;
using UltimateParser.Singularity.Models;
using UltimateParser.Sync.Implementations;
using UltimateParser.Sync.Interfaces;
using UltimateParser.Sync.Models;

namespace UltimateParser.Tests;

/// <summary>
/// Проверяет решения о создании, обновлении, архивации и пропуске без вызова Singularity
/// </summary>
public class ScheduleSyncTests
{
    /// <summary>
    /// Дата, которую получает разборщик, чтобы октябрьские ячейки остались в 2026 году
    /// </summary>
    private static readonly DateOnly Today = new(2026, 10, 8);

    #region Facts

    /// <summary>
    /// Новые ячейки создаются, пустые архивируются, выполненные остаются, чужие задачи игнорируются
    /// </summary>
    [Fact]
    public async Task Run_creates_updates_archives_and_skips_completed_tasks()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,
            ,,18.50-20.10,"Новый предмет, Иванов"
            ,,20.10-21.30,"Старый предмет, Петров"
            пт,02.10,17.20-18.40,"Выполненный предмет, Сидоров"
            """;

        var source = new FakeSource(csv);
        var client = new FakeClient();
        var kept = SampleLesson("up:rki-2-26:2026-10-01:2010", "Старый предмет", "Старый предмет, Петров");
        client.Tasks.Add(TaskFrom(kept, "T-same", checkState: 0));
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1720", "Убрали", "Убрали, преподаватель"),
            "T-empty",
            checkState: 0));
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-02:1720", "Выполненный предмет", "Выполненный предмет, Сидоров"),
            "T-done",
            checkState: 1));
        client.Tasks.Add(new SingularityTask(
            "T-other-month",
            "up:rki-2-26:2026-09-01:1720",
            "Сентябрь",
            "Сентябрь",
            "2026-09-01T17:20:00+03:00",
            true,
            80,
            0,
            null,
            false));
        client.Tasks.Add(new SingularityTask(
            "T-foreign",
            "manual-task",
            "Своя задача",
            null,
            null,
            false,
            null,
            0,
            null,
            false));

        var report = await Sync(source, client);

        Assert.Equal(new SyncReport(1, 0, 1, 0, 0, 1), report);
        Assert.Equal("Новый предмет", client.Created.Single().Title);
        Assert.Equal("T-empty", client.Archived.Single());
        Assert.Empty(client.Updated);
        Assert.Empty(client.Unarchived);
        Assert.Equal("РКИ-2-26", client.ProjectTitle);
    }

    /// <summary>
    /// Заполненная ячейка возвращает архивную задачу и обновляет изменённое название
    /// </summary>
    [Fact]
    public async Task Run_unarchives_and_updates_when_the_cell_is_filled_again()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,"Новое название, Иванов"
            """;

        var client = new FakeClient();
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1720", "Старое название", "Старое название, Иванов"),
            "T-archived",
            checkState: 0,
            journalDate: "2026-10-07"));

        var report = await Sync(new FakeSource(csv), client);

        Assert.Equal(new SyncReport(0, 1, 0, 1, 0, 0), report);
        Assert.Equal("T-archived", client.Unarchived.Single());
        Assert.Equal("Новое название", client.Updated.Single().Title);
    }

    /// <summary>
    /// Занятие, которое уже закончилось, отмечается выполненным, а ещё не начавшееся создаётся
    /// </summary>
    [Fact]
    public async Task Run_archives_a_lesson_that_has_already_ended()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,"Прошедший предмет, Иванов"
            пт,02.10,17.20-18.40,"Будущий предмет, Петров"
            """;

        var client = new FakeClient();
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1720", "Прошедший предмет", "Прошедший предмет, Иванов"),
            "T-past",
            checkState: 0));

        var options = new SyncOptions
        {
            SingularityToken = "test",
            GroupColumn = "РКИ-2-26",
            ProjectTitle = "Расписание",
            TimeZoneId = "Europe/Moscow",
            RunOnce = true,
        };
        var clock = new FixedClock(Today, new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(3)));
        var sync = new ScheduleSync(new FakeSource(csv), client, clock, options, NullLogger<ScheduleSync>.Instance);

        var report = await sync.RunAsync(CancellationToken.None);

        Assert.Equal(new SyncReport(1, 0, 0, 0, 1, 0), report);
        Assert.Equal("T-past", client.Completed.Single());
        Assert.Empty(client.Archived);
        Assert.Equal("Будущий предмет", client.Created.Single().Title);
        Assert.Equal("Расписание", client.ProjectTitle);
    }

    /// <summary>
    /// Прошедшая пара текущей недели создаётся и закрывается, а пара прошлой недели не создаётся
    /// </summary>
    [Fact]
    public async Task Run_creates_and_completes_a_past_lesson_from_the_current_week()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,"Прошлая неделя, Иванов"
            ср,07.10,17.20-18.40,"Эта неделя, Петров"
            пт,09.10,17.20-18.40,"Будущая пара, Сидоров"
            """;

        var options = new SyncOptions
        {
            SingularityToken = "test",
            GroupColumn = "РКИ-2-26",
            ProjectTitle = "Расписание",
            TimeZoneId = "Europe/Moscow",
            RunOnce = true,
        };
        var clock = new FixedClock(new DateOnly(2026, 10, 8), new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.FromHours(3)));
        var client = new FakeClient();
        var sync = new ScheduleSync(new FakeSource(csv), client, clock, options, NullLogger<ScheduleSync>.Instance);

        var report = await sync.RunAsync(CancellationToken.None);

        Assert.Equal(new SyncReport(2, 0, 0, 0, 1, 0), report);
        Assert.Equal(["Эта неделя", "Будущая пара"], client.Created.Select(lesson => lesson.Title).ToArray());
        Assert.Equal("T-1", client.Completed.Single());
    }

    /// <summary>
    /// Сохранённая дельта Quill с тем же текстом не вызывает повторного обновления
    /// </summary>
    [Fact]
    public async Task Run_does_not_update_when_the_stored_note_is_the_same_delta()
    {
        const string note = "Предмет, Иванов";
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,"Предмет, Иванов"
            """;

        var client = new FakeClient();
        var task = TaskFrom(SampleLesson("up:rki-2-26:2026-10-01:1720", "Предмет", note), "T-1", checkState: 0);
        client.Tasks.Add(task with { Note = """{"ops":[{"insert":"Предмет, Иванов\n"}]}""" });

        var report = await Sync(new FakeSource(csv), client);

        Assert.Equal(new SyncReport(0, 0, 0, 0, 0, 0), report);
        Assert.Empty(client.Updated);
    }

    #endregion Facts

    #region Helpers

    /// <summary>
    /// Запускает синхронизацию на подделках с фиксированной датой
    /// </summary>
    /// <param name="source">
    /// Источник CSV
    /// </param>
    /// <param name="client">
    /// Клиент Singularity, который записывает вызовы
    /// </param>
    /// <returns>
    /// Отчёт синхронизации
    /// </returns>
    private static Task<SyncReport> Sync(FakeSource source, FakeClient client)
    {
        var options = new SyncOptions
        {
            SingularityToken = "test",
            GroupColumn = "РКИ-2-26",
            ProjectTitle = "РКИ-2-26",
            TimeZoneId = "Europe/Moscow",
            RunOnce = true,
        };
        var sync = new ScheduleSync(source, client, new FixedClock(Today, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(3))), options, NullLogger<ScheduleSync>.Instance);
        return sync.RunAsync(CancellationToken.None);
    }

    /// <summary>
    /// Собирает занятие, у которого начало закодировано во внешнем идентификаторе
    /// </summary>
    /// <param name="externalId">
    /// Ключ, который заканчивается на <c>yyyy-MM-dd:HHmm</c>
    /// </param>
    /// <param name="title">
    /// Название задачи
    /// </param>
    /// <param name="note">
    /// Заметка задачи
    /// </param>
    /// <returns>
    /// Занятие на 80 минут с московским смещением
    /// </returns>
    private static Lesson SampleLesson(string externalId, string title, string note)
    {
        var parts = externalId.Split(':');
        var clock = parts[^1];
        var start = DateTimeOffset.ParseExact(
            $"{parts[^2]}T{clock[..2]}:{clock[2..]}:00+03:00",
            "yyyy-MM-dd'T'HH:mm:sszzz",
            CultureInfo.InvariantCulture);
        return new Lesson(externalId, title, note, start, 80);
    }

    /// <summary>
    /// Оборачивает занятие в снимок сохранённой задачи
    /// </summary>
    /// <param name="lesson">
    /// Желаемые значения занятия
    /// </param>
    /// <param name="id">
    /// Идентификатор задачи Singularity
    /// </param>
    /// <param name="checkState">
    /// Состояние отметки. <c>1</c> значит, что задача выполнена
    /// </param>
    /// <param name="journalDate">
    /// Дата архивации или <see langword="null"/>, если задача активна
    /// </param>
    /// <returns>
    /// Снимок
    /// </returns>
    private static SingularityTask TaskFrom(Lesson lesson, string id, int checkState, string? journalDate = null) =>
        new(
            id,
            lesson.ExternalId,
            lesson.Title,
            lesson.Note,
            SingularityClient.FormatStart(lesson.Start),
            true,
            lesson.TimeLengthMinutes,
            checkState,
            journalDate,
            false);

    #endregion Helpers

    #region Fakes

    /// <summary>
    /// Часы, замороженные на одной дате и одном моменте
    /// </summary>
    /// <param name="today">
    /// Дата, которую возвращает <see cref="Today"/>
    /// </param>
    /// <param name="now">
    /// Момент, который возвращает <see cref="Now"/>
    /// </param>
    private sealed class FixedClock(DateOnly today, DateTimeOffset now) : IScheduleClock
    {
        /// <summary>
        /// Дата, переданная в конструктор
        /// </summary>
        public DateOnly Today => today;

        /// <summary>
        /// Момент, переданный в конструктор
        /// </summary>
        public DateTimeOffset Now => now;
    }

    /// <summary>
    /// Источник расписания, который возвращает заранее заданную строку CSV
    /// </summary>
    /// <param name="csv">
    /// CSV, который возвращает <see cref="ReadCsvAsync"/>
    /// </param>
    private sealed class FakeSource(string csv) : IScheduleSource
    {
        /// <summary>
        /// Возвращает заранее заданный CSV
        /// </summary>
        /// <param name="cancellationToken">
        /// Не используется
        /// </param>
        /// <returns>
        /// CSV, переданный в конструктор
        /// </returns>
        public Task<string> ReadCsvAsync(CancellationToken cancellationToken) => Task.FromResult(csv);
    }

    /// <summary>
    /// Клиент Singularity, который записывает вызовы вместо отправки HTTP
    /// </summary>
    private sealed class FakeClient : ISingularityClient
    {
        /// <summary>
        /// Задачи, которые возвращает запрос списка
        /// </summary>
        public List<SingularityTask> Tasks { get; } = [];

        /// <summary>
        /// Занятия, переданные в создание
        /// </summary>
        public List<Lesson> Created { get; } = [];

        /// <summary>
        /// Занятия, переданные в обновление
        /// </summary>
        public List<Lesson> Updated { get; } = [];

        /// <summary>
        /// Идентификаторы задач, переданные в архивацию
        /// </summary>
        public List<string> Archived { get; } = [];

        /// <summary>
        /// Идентификаторы задач, отмеченных выполненными
        /// </summary>
        public List<string> Completed { get; } = [];

        /// <summary>
        /// Идентификаторы задач, переданные в возврат из архива
        /// </summary>
        public List<string> Unarchived { get; } = [];

        /// <summary>
        /// Название, переданное при поиске проекта
        /// </summary>
        public string? ProjectTitle { get; private set; }

        /// <summary>
        /// Записывает название проекта и возвращает фиксированный идентификатор
        /// </summary>
        /// <param name="title">
        /// Запрошенное название проекта
        /// </param>
        /// <param name="cancellationToken">
        /// Не используется
        /// </param>
        /// <returns>
        /// <c>P-1</c>
        /// </returns>
        public Task<string> GetOrCreateProjectIdAsync(string title, CancellationToken cancellationToken)
        {
            ProjectTitle = title;
            return Task.FromResult("P-1");
        }

        /// <summary>
        /// Сообщает, что отдельного проекта группы нет
        /// </summary>
        /// <param name="title">
        /// Не используется
        /// </param>
        /// <param name="cancellationToken">
        /// Не используется
        /// </param>
        /// <returns>
        /// <see langword="null"/>
        /// </returns>
        public Task<string?> FindProjectIdAsync(string title, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        /// <summary>
        /// Возвращает <see cref="Tasks"/>
        /// </summary>
        /// <param name="projectId">
        /// Не используется
        /// </param>
        /// <param name="cancellationToken">
        /// Не используется
        /// </param>
        /// <returns>
        /// Заранее заданный список задач
        /// </returns>
        public Task<IReadOnlyList<SingularityTask>> ListProjectTasksAsync(
            string projectId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SingularityTask>>(Tasks);

        /// <summary>
        /// Записывает создание
        /// </summary>
        /// <param name="projectId">
        /// Не используется
        /// </param>
        /// <param name="lesson">
        /// Занятие, которое было бы создано
        /// </param>
        /// <param name="cancellationToken">
        /// Не используется
        /// </param>
        /// <returns>
        /// Идентификатор вида <c>T-1</c>, <c>T-2</c> и далее по порядку созданий
        /// </returns>
        public Task<string> CreateTaskAsync(string projectId, Lesson lesson, CancellationToken cancellationToken)
        {
            Created.Add(lesson);
            return Task.FromResult($"T-{Created.Count}");
        }

        /// <summary>
        /// Записывает обновление
        /// </summary>
        /// <param name="taskId">
        /// Не используется
        /// </param>
        /// <param name="projectId">
        /// Не используется
        /// </param>
        /// <param name="lesson">
        /// Занятие, которое было бы записано
        /// </param>
        /// <param name="cancellationToken">
        /// Не используется
        /// </param>
        /// <returns>
        /// Завершённая задача
        /// </returns>
        public Task UpdateTaskAsync(
            string taskId,
            string projectId,
            Lesson lesson,
            CancellationToken cancellationToken)
        {
            Updated.Add(lesson);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Записывает архивацию
        /// </summary>
        /// <param name="taskId">
        /// Идентификатор архивируемой задачи
        /// </param>
        /// <param name="journalDate">
        /// Не используется
        /// </param>
        /// <param name="cancellationToken">
        /// Не используется
        /// </param>
        /// <returns>
        /// Завершённая задача
        /// </returns>
        public Task ArchiveTaskAsync(string taskId, DateOnly journalDate, CancellationToken cancellationToken)
        {
            Archived.Add(taskId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Записывает отметку выполнения
        /// </summary>
        /// <param name="taskId">
        /// Идентификатор выполняемой задачи
        /// </param>
        /// <param name="cancellationToken">
        /// Не используется
        /// </param>
        /// <returns>
        /// Завершённая задача
        /// </returns>
        public Task CompleteTaskAsync(string taskId, CancellationToken cancellationToken)
        {
            Completed.Add(taskId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Записывает возврат из архива
        /// </summary>
        /// <param name="taskId">
        /// Идентификатор возвращаемой задачи
        /// </param>
        /// <param name="cancellationToken">
        /// Не используется
        /// </param>
        /// <returns>
        /// Завершённая задача
        /// </returns>
        public Task UnarchiveTaskAsync(string taskId, CancellationToken cancellationToken)
        {
            Unarchived.Add(taskId);
            return Task.CompletedTask;
        }
    }

    #endregion Fakes
}
