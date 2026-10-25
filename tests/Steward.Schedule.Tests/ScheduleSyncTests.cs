using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using Steward.Schedule.Common.Helpers;
using Steward.Schedule.Common.Models;
using Steward.Schedule.Scheduling.Interfaces;
using Steward.Schedule.Singularity.Implementations;
using Steward.Schedule.Singularity.Interfaces;
using Steward.Schedule.Singularity.Models;
using Steward.Schedule.Sync.Implementations;
using Steward.Schedule.Sync.Interfaces;
using Steward.Schedule.Sync.Models;

namespace Steward.Schedule.Tests;

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
    /// Новые ячейки создаются, удалённые отмечаются выполненными, а следующая пара получает пометку
    /// </summary>
    [Fact]
    public async Task Run_creates_updates_archives_and_skips_completed_tasks()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,
            ,,18.50-20.10,"Новый предмет, Иванов"
            ,,20.10-21.30,"Старый предмет, Петров"
            ср,30.09,17.20-18.40,"Выполненный предмет, Сидоров"
            """;

        var source = new FakeSource(csv);
        var client = new FakeClient();
        var kept = SampleLesson("up:rki-2-26:2026-10-01:2010", "Старый предмет", "Преподаватель: Петров");
        client.Tasks.Add(TaskFrom(kept, "T-same", checkState: 0));
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1720", "Убрали", "Убрали, преподаватель"),
            "T-empty",
            checkState: 0));
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-09-30:1720", "Выполненный предмет", "Выполненный предмет, Сидоров"),
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

        Assert.Equal(new SyncReport(1, 0, 0, 0, 1, 1), report);
        Assert.Equal("Новый предмет", client.Created.Single().Title);
        Assert.Contains("Отмена или перенос: Убрали, 01.10 17:20", client.Created.Single().Note, StringComparison.Ordinal);
        Assert.Equal("T-empty", client.Completed.Single());
        Assert.Empty(client.Archived);
        Assert.Empty(client.Updated);
        Assert.Empty(client.Unarchived);
        Assert.Equal("РКИ-2-26", client.ProjectTitle);
    }

    /// <summary>
    /// Удалённая пара того же предмета сегодня закрывается, а оставшаяся задача того дня помечается как перенос
    /// </summary>
    [Fact]
    public async Task Run_completes_a_removed_lesson_and_marks_the_same_subject_today_as_moved()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,
            ,,20.10-21.30,"Предмет, Иванов"
            """;

        var client = new FakeClient();
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1720", "Предмет", "Преподаватель: Иванов"),
            "T-removed",
            checkState: 0));
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:2010", "Предмет", "Преподаватель: Иванов"),
            "T-kept",
            checkState: 0));

        var report = await Sync(new FakeSource(csv), client);

        Assert.Equal(new SyncReport(0, 1, 0, 0, 1, 0), report);
        Assert.Equal("T-removed", client.Completed.Single());
        Assert.Empty(client.Archived);
        Assert.StartsWith("Перенос с 01.10 17:20", client.Updated.Single().Note, StringComparison.Ordinal);
    }

    /// <summary>
    /// Выполненная задача занятия, которое ещё не закончилось, удаляется и создаётся снова
    /// </summary>
    [Fact]
    public async Task Run_deletes_a_completed_task_whose_lesson_has_not_ended()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            пн,19.10,17.20-18.40,"Перенесённый предмет, Иванов"
            """;

        var client = new FakeClient();
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-19:1720", "Перенесённый предмет", "Преподаватель: Иванов"),
            "T-moved",
            checkState: 1));

        var report = await Sync(new FakeSource(csv), client);

        Assert.Equal(new SyncReport(1, 0, 0, 0, 0, 0, 1), report);
        Assert.Equal("T-moved", client.Deleted.Single());
        Assert.Equal("Перенесённый предмет", client.Created.Single().Title);
        Assert.Empty(client.Completed);
    }

    /// <summary>
    /// Уже спрятанная задача опустевшей ячейки возвращается из архива и отмечается выполненной
    /// </summary>
    [Fact]
    public async Task Run_completes_a_removed_lesson_that_was_already_archived()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,
            ,,18.50-20.10,"Другой предмет, Петров"
            """;

        var client = new FakeClient();
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1720", "Убрали", "Преподаватель: Иванов"),
            "T-archived-empty",
            checkState: 0,
            journalDate: "2026-10-07"));

        var report = await Sync(new FakeSource(csv), client);

        Assert.Equal(new SyncReport(1, 0, 0, 1, 1, 0), report);
        Assert.Equal("T-archived-empty", client.Unarchived.Single());
        Assert.Equal("T-archived-empty", client.Completed.Single());
        Assert.Contains("Отмена или перенос: Убрали, 01.10 17:20", client.Created.Single().Note, StringComparison.Ordinal);
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
    /// Занятие, которое уже закончилось, сначала получает текст из таблицы, затем отмечается выполненным. Ещё не начавшееся создаётся
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
            SpreadsheetId = "1NKd10Q4Ld17T2c_g_71cqAHBAuPaVKNN5rPpWsL20ic",
            SheetGid = "197520568",
            GroupColumn = "РКИ-2-26",
            ProjectTitle = "Расписание",
            TimeZoneId = "Europe/Moscow",
            IntervalMinutes = 30,
            RunOnce = true,
        };
        var clock = new FixedClock(Today, new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(3)));
        var sync = new ScheduleSyncer(new FakeSource(csv), client, clock, options, NullLogger<ScheduleSyncer>.Instance);

        var report = await sync.RunAsync(CancellationToken.None);

        Assert.Equal(new SyncReport(1, 1, 0, 0, 1, 0), report);
        Assert.Equal("T-past", client.Completed.Single());
        Assert.Equal("Прошедший предмет", client.Updated.Single().Title);
        Assert.StartsWith("Преподаватель: Иванов", client.Updated.Single().Note, StringComparison.Ordinal);
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
            SpreadsheetId = "1NKd10Q4Ld17T2c_g_71cqAHBAuPaVKNN5rPpWsL20ic",
            SheetGid = "197520568",
            GroupColumn = "РКИ-2-26",
            ProjectTitle = "Расписание",
            TimeZoneId = "Europe/Moscow",
            IntervalMinutes = 30,
            RunOnce = true,
        };
        var clock = new FixedClock(new DateOnly(2026, 10, 8), new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.FromHours(3)));
        var client = new FakeClient();
        var sync = new ScheduleSyncer(new FakeSource(csv), client, clock, options, NullLogger<ScheduleSyncer>.Instance);

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
        const string note = "Преподаватель: Иванов";
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,"Предмет, Иванов"
            """;

        var client = new FakeClient();
        var task = TaskFrom(SampleLesson("up:rki-2-26:2026-10-01:1720", "Предмет", note), "T-1", checkState: 0);
        client.Tasks.Add(task with { Note = """{"ops":[{"insert":"Преподаватель: Иванов\n"}]}""" });

        var report = await Sync(new FakeSource(csv), client);

        Assert.Equal(new SyncReport(0, 0, 0, 0, 0, 0), report);
        Assert.Empty(client.Updated);
    }

    /// <summary>
    /// Уже записанная гиперссылка «Ссылка» не вызывает повторного обновления
    /// </summary>
    [Fact]
    public async Task Run_does_not_update_when_the_stored_note_is_already_a_link()
    {
        const string lessonNote = "Преподаватель: Иванов\n---\nhttps://telemost.360.yandex.ru/j/1";
        var csv = ",Дата,время,РКИ-2-26\nчт,01.10,17.20-18.40,\"Предмет, Иванов\nhttps://telemost.360.yandex.ru/j/1\"\n";
        var client = new FakeClient();
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1720", "Предмет", lessonNote),
            "T-1",
            checkState: 0) with
        { Note = NoteTextHelper.Format(lessonNote) });

        var report = await Sync(new FakeSource(csv), client);

        Assert.Equal(new SyncReport(0, 0, 0, 0, 0, 0), report);
        Assert.Empty(client.Updated);
    }

    /// <summary>
    /// Задача без напоминания за 15 минут обновляется
    /// </summary>
    [Fact]
    public async Task Run_updates_a_task_that_has_no_reminder()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,"Предмет, Иванов"
            """;

        var client = new FakeClient();
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1720", "Предмет", "Предмет, Иванов"),
            "T-1",
            checkState: 0,
            withReminder: false));

        var report = await Sync(new FakeSource(csv), client);

        Assert.Equal(new SyncReport(0, 1, 0, 0, 0, 0), report);
        Assert.Equal("Предмет", client.Updated.Single().Title);
    }

    /// <summary>
    /// Уже выполненная задача опустевшей будущей ячейки остаётся, а пометка на следующей паре пишется снова
    /// </summary>
    [Fact]
    public async Task Run_keeps_a_completed_removed_lesson_so_the_notice_survives()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,
            ,,18.50-20.10,"Другой предмет, Петров"
            """;

        var client = new FakeClient();
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1720", "Убрали", "Преподаватель: Иванов"),
            "T-empty",
            checkState: 1));
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1850", "Другой предмет", "Преподаватель: Петров"),
            "T-next",
            checkState: 0));

        var report = await Sync(new FakeSource(csv), client);

        Assert.Equal(new SyncReport(0, 1, 0, 0, 0, 1), report);
        Assert.Empty(client.Deleted);
        Assert.Contains("Отмена или перенос: Убрали, 01.10 17:20", client.Updated.Single().Note, StringComparison.Ordinal);
    }

    /// <summary>
    /// Пометка о переносе попадает в прошедшую пару до того, как та отмечается выполненной
    /// </summary>
    [Fact]
    public async Task Run_writes_the_notice_before_completing_a_lesson_that_has_ended()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,
            ,,20.10-21.30,"Предмет, Иванов"
            """;

        var options = new SyncOptions
        {
            SingularityToken = "test",
            SpreadsheetId = "1NKd10Q4Ld17T2c_g_71cqAHBAuPaVKNN5rPpWsL20ic",
            SheetGid = "197520568",
            GroupColumn = "РКИ-2-26",
            ProjectTitle = "Расписание",
            TimeZoneId = "Europe/Moscow",
            IntervalMinutes = 30,
            RunOnce = true,
        };
        var clock = new FixedClock(Today, new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(3)));
        var client = new FakeClient();
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1720", "Предмет", "Преподаватель: Иванов"),
            "T-removed",
            checkState: 0));
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:2010", "Предмет", "Преподаватель: Иванов"),
            "T-kept",
            checkState: 0));
        var sync = new ScheduleSyncer(new FakeSource(csv), client, clock, options, NullLogger<ScheduleSyncer>.Instance);

        var report = await sync.RunAsync(CancellationToken.None);

        Assert.Equal(new SyncReport(0, 1, 0, 0, 2, 0), report);
        Assert.StartsWith("Перенос с 01.10 17:20", client.Updated.Single().Note, StringComparison.Ordinal);
        Assert.Equal(["T-removed", "T-kept"], client.Completed);
    }

    /// <summary>
    /// Прошедшая архивная задача сначала возвращается и обновляется, потом отмечается выполненной
    /// </summary>
    [Fact]
    public async Task Run_unarchives_and_updates_a_past_lesson_before_completing_it()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,"Новое название, Иванов"
            """;

        var options = new SyncOptions
        {
            SingularityToken = "test",
            SpreadsheetId = "1NKd10Q4Ld17T2c_g_71cqAHBAuPaVKNN5rPpWsL20ic",
            SheetGid = "197520568",
            GroupColumn = "РКИ-2-26",
            ProjectTitle = "Расписание",
            TimeZoneId = "Europe/Moscow",
            IntervalMinutes = 30,
            RunOnce = true,
        };
        var clock = new FixedClock(Today, new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(3)));
        var client = new FakeClient();
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1720", "Старое название", "Старое название, Иванов"),
            "T-past",
            checkState: 0,
            journalDate: "2026-10-07"));
        var sync = new ScheduleSyncer(new FakeSource(csv), client, clock, options, NullLogger<ScheduleSyncer>.Instance);

        var report = await sync.RunAsync(CancellationToken.None);

        Assert.Equal(new SyncReport(0, 1, 0, 1, 1, 0), report);
        Assert.Equal("T-past", client.Unarchived.Single());
        Assert.Equal("Новое название", client.Updated.Single().Title);
        Assert.Equal("T-past", client.Completed.Single());
    }

    /// <summary>
    /// Отмена пишется в пару, которую этот же прогон удалит и создаст заново
    /// </summary>
    [Fact]
    public async Task Run_puts_the_notice_on_a_completed_lesson_that_will_be_recreated()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,
            ,,18.50-20.10,"Предмет, Иванов"
            ,,20.10-21.30,"Другой предмет, Петров"
            """;

        var client = new FakeClient();
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1720", "Убрали", "Преподаватель: Иванов"),
            "T-empty",
            checkState: 0));
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1850", "Предмет", "Преподаватель: Иванов"),
            "T-mid",
            checkState: 1));
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:2010", "Другой предмет", "Преподаватель: Петров"),
            "T-late",
            checkState: 0));

        var report = await Sync(new FakeSource(csv), client);

        Assert.Equal(new SyncReport(1, 0, 0, 0, 1, 0, 1), report);
        Assert.Equal("T-mid", client.Deleted.Single());
        Assert.Equal("T-empty", client.Completed.Single());
        Assert.Contains("Отмена или перенос: Убрали, 01.10 17:20", client.Created.Single().Note, StringComparison.Ordinal);
        Assert.Empty(client.Updated);
    }

    /// <summary>
    /// Новое занятие получает тег предмета, а у существующего тег добавляется к тем, что поставил пользователь
    /// </summary>
    [Fact]
    public async Task Run_adds_the_subject_tag_and_keeps_tags_set_by_hand()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,"Предмет, Иванов"
            пт,02.10,17.20-18.40,"Другой предмет, Петров"
            """;

        var client = new FakeClient();
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1720", "Предмет", "Преподаватель: Иванов"),
            "T-1",
            checkState: 0) with { Tags = ["A-ручной"] });

        var report = await SyncWithSubjectTags(new FakeSource(csv), client);

        Assert.Equal(new SyncReport(1, 1, 0, 0, 0, 0), report);
        Assert.Equal(["A-ручной", "A-Предмет"], client.Updated.Single().TagIds);
        Assert.Equal(["A-Другой предмет"], client.Created.Single().TagIds);
    }

    /// <summary>
    /// Задача, на которой тег предмета уже стоит, не обновляется
    /// </summary>
    [Fact]
    public async Task Run_does_not_update_when_the_subject_tag_is_already_set()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,"Предмет, Иванов"
            """;

        var client = new FakeClient();
        client.Tasks.Add(TaskFrom(
            SampleLesson("up:rki-2-26:2026-10-01:1720", "Предмет", "Преподаватель: Иванов"),
            "T-1",
            checkState: 0) with { Tags = ["A-Предмет"] });

        var report = await SyncWithSubjectTags(new FakeSource(csv), client);

        Assert.Equal(new SyncReport(0, 0, 0, 0, 0, 0), report);
        Assert.Empty(client.Updated);
    }

    /// <summary>
    /// Если токену не хватает доступа к тегам, прогон идёт дальше без них
    /// </summary>
    [Fact]
    public async Task Run_continues_without_tags_when_the_token_cannot_use_tags()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,"Предмет, Иванов"
            """;

        var client = new FakeClient { FailTags = true };

        var report = await SyncWithSubjectTags(new FakeSource(csv), client);

        Assert.Equal(new SyncReport(1, 0, 0, 0, 0, 0), report);
        Assert.Empty(client.Created.Single().TagIds);
    }

    /// <summary>
    /// Пока флаг выключен, теги не запрашиваются и не ставятся
    /// </summary>
    [Fact]
    public async Task Run_does_not_touch_tags_while_the_flag_is_off()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            чт,01.10,17.20-18.40,"Предмет, Иванов"
            """;

        var client = new FakeClient();

        await Sync(new FakeSource(csv), client);

        Assert.Empty(client.RequestedTags);
        Assert.Empty(client.Created.Single().TagIds);
    }

    #endregion Facts

    #region Helpers

    /// <summary>
    /// Запускает синхронизацию с включёнными тегами предметов
    /// </summary>
    /// <param name="source">Источник CSV</param>
    /// <param name="client">Клиент Singularity, который записывает вызовы</param>
    /// <returns>Отчёт синхронизации</returns>
    private static Task<SyncReport> SyncWithSubjectTags(FakeSource source, FakeClient client)
    {
        var options = new SyncOptions
        {
            SingularityToken = "test",
            SpreadsheetId = "1NKd10Q4Ld17T2c_g_71cqAHBAuPaVKNN5rPpWsL20ic",
            SheetGid = "197520568",
            GroupColumn = "РКИ-2-26",
            ProjectTitle = "РКИ-2-26",
            TimeZoneId = "Europe/Moscow",
            IntervalMinutes = 30,
            RunOnce = true,
            SubjectTags = true,
        };
        var sync = new ScheduleSyncer(source, client, new FixedClock(Today, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(3))), options, NullLogger<ScheduleSyncer>.Instance);
        return sync.RunAsync(CancellationToken.None);
    }


    /// <summary>
    /// Запускает синхронизацию на подделках с фиксированной датой
    /// </summary>
    /// <param name="source">Источник CSV</param>
    /// <param name="client">Клиент Singularity, который записывает вызовы</param>
    /// <returns>Отчёт синхронизации</returns>
    private static Task<SyncReport> Sync(FakeSource source, FakeClient client)
    {
        var options = new SyncOptions
        {
            SingularityToken = "test",
            SpreadsheetId = "1NKd10Q4Ld17T2c_g_71cqAHBAuPaVKNN5rPpWsL20ic",
            SheetGid = "197520568",
            GroupColumn = "РКИ-2-26",
            ProjectTitle = "РКИ-2-26",
            TimeZoneId = "Europe/Moscow",
            IntervalMinutes = 30,
            RunOnce = true,
        };
        var sync = new ScheduleSyncer(source, client, new FixedClock(Today, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(3))), options, NullLogger<ScheduleSyncer>.Instance);
        return sync.RunAsync(CancellationToken.None);
    }

    /// <summary>
    /// Собирает занятие, у которого начало закодировано во внешнем идентификаторе
    /// </summary>
    /// <param name="externalId">
    /// Ключ, который заканчивается на <c>yyyy-MM-dd:HHmm</c>
    /// </param>
    /// <param name="title">Название задачи</param>
    /// <param name="note">Заметка задачи</param>
    /// <returns>Занятие на 80 минут с московским смещением</returns>
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
    /// <param name="lesson">Желаемые значения занятия</param>
    /// <param name="id">Идентификатор задачи Singularity</param>
    /// <param name="checkState">
    /// Состояние отметки. <c>1</c> значит, что задача выполнена
    /// </param>
    /// <param name="journalDate">
    /// Дата архивации или <see langword="null"/>, если задача активна
    /// </param>
    /// <param name="withReminder">
    /// <see langword="false"/>, если у снимка нет напоминания за 15 минут
    /// </param>
    /// <returns>Снимок</returns>
    private static SingularityTask TaskFrom(Lesson lesson, string id, int checkState, string? journalDate = null, bool withReminder = true) =>
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
            false,
            withReminder ? [15] : null);

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
        /// <param name="cancellationToken">Не используется</param>
        /// <returns>CSV, переданный в конструктор</returns>
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
        /// Идентификаторы удалённых задач
        /// </summary>
        public List<string> Deleted { get; } = [];

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
        /// <param name="title">Запрошенное название проекта</param>
        /// <param name="cancellationToken">Не используется</param>
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
        /// <param name="title">Не используется</param>
        /// <param name="cancellationToken">Не используется</param>
        /// <returns>
        /// <see langword="null"/>
        /// </returns>
        public Task<string?> FindProjectIdAsync(string title, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        /// <summary>
        /// Названия тегов, которые запрашивала синхронизация
        /// </summary>
        public List<string> RequestedTags { get; } = [];

        /// <summary>
        /// <see langword="true"/>, если запрос тега должен упасть так же, как при нехватке доступа
        /// </summary>
        public bool FailTags { get; set; }

        /// <summary>
        /// Записывает запрос тега
        /// </summary>
        /// <param name="title">Название тега</param>
        /// <param name="cancellationToken">Не используется</param>
        /// <returns>
        /// Идентификатор вида <c>A-название</c>
        /// </returns>
        public Task<string> GetOrCreateTagIdAsync(string title, CancellationToken cancellationToken)
        {
            if (FailTags)
                throw new InvalidOperationException("Singularity 403 GET /v2/tag: forbidden");

            RequestedTags.Add(title);
            return Task.FromResult($"A-{title}");
        }

        /// <summary>
        /// Возвращает <see cref="Tasks"/>
        /// </summary>
        /// <param name="projectId">Не используется</param>
        /// <param name="cancellationToken">Не используется</param>
        /// <returns>Заранее заданный список задач</returns>
        public Task<IReadOnlyList<SingularityTask>> ListProjectTasksAsync(
            string projectId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SingularityTask>>(Tasks);

        /// <summary>
        /// Записывает создание
        /// </summary>
        /// <param name="projectId">Не используется</param>
        /// <param name="lesson">Занятие, которое было бы создано</param>
        /// <param name="cancellationToken">Не используется</param>
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
        /// <param name="taskId">Не используется</param>
        /// <param name="projectId">Не используется</param>
        /// <param name="lesson">Занятие, которое было бы записано</param>
        /// <param name="cancellationToken">Не используется</param>
        /// <returns>Завершённая задача</returns>
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
        /// Записывает удаление
        /// </summary>
        /// <param name="taskId">Идентификатор удаляемой задачи</param>
        /// <param name="cancellationToken">Не используется</param>
        /// <returns>Завершённая задача</returns>
        public Task DeleteTaskAsync(string taskId, CancellationToken cancellationToken)
        {
            Deleted.Add(taskId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Записывает архивацию
        /// </summary>
        /// <param name="taskId">Идентификатор архивируемой задачи</param>
        /// <param name="journalDate">Не используется</param>
        /// <param name="cancellationToken">Не используется</param>
        /// <returns>Завершённая задача</returns>
        public Task ArchiveTaskAsync(string taskId, DateTimeOffset journalDate, CancellationToken cancellationToken)
        {
            Archived.Add(taskId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Записывает отметку выполнения
        /// </summary>
        /// <param name="taskId">Идентификатор выполняемой задачи</param>
        /// <param name="cancellationToken">Не используется</param>
        /// <returns>Завершённая задача</returns>
        public Task CompleteTaskAsync(string taskId, CancellationToken cancellationToken)
        {
            Completed.Add(taskId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Записывает возврат из архива
        /// </summary>
        /// <param name="taskId">Идентификатор возвращаемой задачи</param>
        /// <param name="cancellationToken">Не используется</param>
        /// <returns>Завершённая задача</returns>
        public Task UnarchiveTaskAsync(string taskId, CancellationToken cancellationToken)
        {
            Unarchived.Add(taskId);
            return Task.CompletedTask;
        }
    }

    #endregion Fakes
}
